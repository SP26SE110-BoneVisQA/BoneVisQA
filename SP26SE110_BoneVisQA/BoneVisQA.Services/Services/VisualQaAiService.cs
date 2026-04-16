using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using System.Text;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace BoneVisQA.Services.Services;

public class VisualQaAiService : IVisualQaAiService
{
    private sealed record RagContextItem(double Similarity, DocumentChunk? Chunk, MedicalCase? Case);

    private const double MinimumRelevantSimilarity = 0.72d;
    private const string InvalidImageNotXrayToken = "INVALID_IMAGE_NOT_XRAY";
    private const string InvalidBoneXrayUserMessage =
        "The system detected that this is not a valid human bone X-ray image. Please upload a proper medical X-ray image for analysis support.";
    private const string RagGeneralKnowledgeDisclaimer =
        "(Note: This analysis is based on general AI knowledge because no direct reference documents were found in the system library).";
    private const string TemporaryVectorSearchUnavailableAnswer =
        "Vector search is temporarily unavailable due to high network demand. Please try again later.";
    private const string TemporaryAiGenerationUnavailableAnswer =
        "AI generation service is temporarily unavailable due to high network demand. Please try again later.";
    private const string AiOverloadVietnameseMessage =
        "The AI system is overloaded. Please try again later.";

    private const int RagChunkFetch = 12;
    private const int RagCaseFetch = 12;
    private const int RagTopMerged = 5;

    private readonly BoneVisQADbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IGeminiService _geminiService;

    public VisualQaAiService(
        BoneVisQADbContext dbContext,
        IEmbeddingService embeddingService,
        IImageProcessingService imageProcessingService,
        IGeminiService geminiService)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _imageProcessingService = imageProcessingService;
        _geminiService = geminiService;
    }

    public async Task<VisualQAResponseDto> RunPipelineAsync(VisualQARequestDto request, CancellationToken cancellationToken = default)
    {
        // Bounding-box ROI overlay when coordinates are present (single image fetch for the generation call).
        string? imageB64 = null;
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            imageB64 = await _imageProcessingService.DrawAnnotationOverlayAsBase64JpegAsync(
                request.ImageUrl,
                request.Coordinates,
                cancellationToken);
        }

        // SEPS: combine question with ROI/image context for retrieval (text embedding; vision model handles image in Gemini).
        var ragQueryText = BuildRagEmbeddingQuery(request);
        float[] embedding;
        try
        {
            embedding = await _embeddingService.EmbedTextAsync(ragQueryText, cancellationToken);
        }
        catch
        {
            throw new InvalidOperationException(AiOverloadVietnameseMessage);
        }
        var queryEmbedding = embedding;
        var queryVector = new Vector(queryEmbedding);

        Guid? caseCategoryId = null;
        if (request.CaseId.HasValue && request.CaseId.Value != Guid.Empty)
        {
            caseCategoryId = await _dbContext.MedicalCases
                .AsNoTracking()
                .Where(mc => mc.Id == request.CaseId.Value)
                .Select(mc => mc.CategoryId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        Guid? excludeCaseId = null;
        if (request.CaseId.HasValue && request.CaseId.Value != Guid.Empty)
            excludeCaseId = request.CaseId.Value;

        var (ragItems, citationsFromRag) = await BuildDualSourceRagAsync(
            queryEmbedding,
            queryVector,
            caseCategoryId,
            excludeCaseId,
            cancellationToken);

        var similarities = ragItems.Select(r => r.Similarity).ToList();

        var maxSimilarity = similarities.Count > 0
            ? similarities.Max()
            : 0d;

        var calculatedScore = similarities.Count > 0
            ? similarities.Average()
            : 0.5d;

        var ragContextAdequate = similarities.Count > 0 && maxSimilarity >= MinimumRelevantSimilarity;

        MedicalCase? predefinedCase = null;
        if (request.CaseId.HasValue && request.CaseId.Value != Guid.Empty)
        {
            predefinedCase = await _dbContext.MedicalCases
                .AsNoTracking()
                .Include(mc => mc.CaseTags)
                    .ThenInclude(ct => ct.Tag)
                .FirstOrDefaultAsync(mc => mc.Id == request.CaseId.Value, cancellationToken);
        }

        var (conversationHistory, existingUserTurns) = await BuildConversationHistoryAsync(request.SessionId, cancellationToken);
        var currentTurnNumber = existingUserTurns + 1;
        var prompt = BuildGeminiPrompt(request, ragItems, ragContextAdequate, predefinedCase, currentTurnNumber);

        // GeminiService expects "imageUrl" input, but we pass Base64 for our inlineData workflow.
        VisualQAResponseDto response;
        try
        {
            response = await _geminiService.GenerateMedicalAnswerAsync(
                prompt,
                imageB64 ?? string.Empty,
                conversationHistory,
                ragContextAdequate,
                cancellationToken);
        }
        catch (AiResponseFormatException)
        {
            throw;
        }
        catch
        {
            // Do not persist user/assistant messages when generation fails.
            throw new InvalidOperationException(AiOverloadVietnameseMessage);
        }

        if (IsInvalidImageNotXrayResponse(response.AnswerText))
        {
            return new VisualQAResponseDto
            {
                AnswerText = InvalidBoneXrayUserMessage,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = calculatedScore,
                ResponseKind = "refusal",
                ClientRequestId = request.ClientRequestId,
                Citations = new List<CitationItemDto>()
            };
        }

        if (!ragContextAdequate && !string.IsNullOrWhiteSpace(response.AnswerText)
            && !response.AnswerText.Contains(RagGeneralKnowledgeDisclaimer, StringComparison.Ordinal))
        {
            response.AnswerText = response.AnswerText.TrimEnd() + "\n\n" + RagGeneralKnowledgeDisclaimer;
        }

        var isNonMedicalRefusal = IsNonMedicalRefusalAnswer(response.AnswerText);

        if (isNonMedicalRefusal)
        {
            // We cannot provide medical citations for rejected/invalid queries.
            response.Citations = new List<CitationItemDto>();
            response.SuggestedDiagnosis = null;
            response.DifferentialDiagnoses = null;
            response.KeyImagingFindings = null;
            response.ReflectiveQuestions = null;
            response.ResponseKind = "refusal";
        }
        else
        {
            // If Gemini provided citationChunkIds, we enrich them; otherwise, we fall back to top retrieved chunks.
            if (response.Citations == null || response.Citations.Count == 0)
            {
                response.Citations = citationsFromRag;
            }
            else
            {
                response.Citations = FilterCitationsAgainstContext(response.Citations, citationsFromRag);
                var metaByChunkId = citationsFromRag
                    .Where(c => c.MedicalCaseId == null && c.ChunkId != Guid.Empty)
                    .ToDictionary(c => c.ChunkId, c => c);
                var metaByCaseId = citationsFromRag
                    .Where(c => c.MedicalCaseId != null)
                    .ToDictionary(c => c.MedicalCaseId!.Value, c => c);
                foreach (var citation in response.Citations)
                {
                    if (citation.MedicalCaseId.HasValue
                        && metaByCaseId.TryGetValue(citation.MedicalCaseId.Value, out var metaCase))
                    {
                        citation.SourceText = metaCase.SourceText;
                        citation.ReferenceUrl = metaCase.ReferenceUrl;
                        citation.PageNumber = metaCase.PageNumber;
                        citation.StartPage = metaCase.StartPage;
                        citation.EndPage = metaCase.EndPage;
                    }
                    else if (metaByChunkId.TryGetValue(citation.ChunkId, out var meta))
                    {
                        citation.SourceText = meta.SourceText;
                        citation.ReferenceUrl = meta.ReferenceUrl;
                        citation.PageNumber = meta.PageNumber;
                        citation.StartPage = meta.StartPage;
                        citation.EndPage = meta.EndPage;
                    }
                }
            }
        }

        response.AiConfidenceScore = calculatedScore;
        response.ClientRequestId = request.ClientRequestId;
        response.ResponseKind = string.IsNullOrWhiteSpace(response.ResponseKind) ? "analysis" : response.ResponseKind;

        return response;
    }

    private static List<CitationItemDto> FilterCitationsAgainstContext(
        IReadOnlyCollection<CitationItemDto> modelCitations,
        IReadOnlyCollection<CitationItemDto> retrievedContextCitations)
    {
        var allowedChunkIds = retrievedContextCitations
            .Where(c => c.MedicalCaseId == null && c.ChunkId != Guid.Empty)
            .Select(c => c.ChunkId)
            .ToHashSet();
        var allowedCaseIds = retrievedContextCitations
            .Where(c => c.MedicalCaseId.HasValue)
            .Select(c => c.MedicalCaseId!.Value)
            .ToHashSet();

        return modelCitations
            .Where(c =>
                (c.MedicalCaseId.HasValue && allowedCaseIds.Contains(c.MedicalCaseId.Value)) ||
                (!c.MedicalCaseId.HasValue && c.ChunkId != Guid.Empty && allowedChunkIds.Contains(c.ChunkId)))
            .ToList();
    }

    private async Task<(List<RagContextItem> Items, List<CitationItemDto> Citations)> BuildDualSourceRagAsync(
        float[] queryEmbedding,
        Vector queryVector,
        Guid? caseCategoryId,
        Guid? excludeCaseId,
        CancellationToken cancellationToken)
    {
        var topChunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Include(c => c.Doc)
            .Where(c => c.Embedding != null && (caseCategoryId == null || c.Doc.CategoryId == caseCategoryId))
            .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
            .Take(RagChunkFetch)
            .ToListAsync(cancellationToken);

        var chunkItems = topChunks
            .Where(c => c.Embedding != null)
            .Select(c => new RagContextItem(
                CalculateCosineSimilarity(queryEmbedding, c.Embedding!.ToArray()),
                c,
                null))
            .ToList();

        var topCases = await _dbContext.MedicalCases
            .AsNoTracking()
            .Where(mc => mc.IsApproved == true && mc.IsActive == true)
            .Where(mc => mc.Embedding != null)
            .Where(mc => mc.IndexingStatus == DocumentIndexingStatuses.Completed)
            .Where(mc => caseCategoryId == null || mc.CategoryId == caseCategoryId)
            .Where(mc => excludeCaseId == null || mc.Id != excludeCaseId.Value)
            .OrderBy(mc => mc.Embedding!.CosineDistance(queryVector))
            .Take(RagCaseFetch)
            .ToListAsync(cancellationToken);

        var caseItems = topCases
            .Where(mc => mc.Embedding != null)
            .Select(mc => new RagContextItem(
                CalculateCosineSimilarity(queryEmbedding, mc.Embedding!.ToArray()),
                null,
                mc))
            .ToList();

        var merged = chunkItems
            .Concat(caseItems)
            .OrderByDescending(x => x.Similarity)
            .Take(RagTopMerged)
            .ToList();

        var citations = merged.Select(r =>
        {
            if (r.Chunk != null)
            {
                return VisualQaCitationMetadataBuilder.FromDocumentChunk(r.Chunk);
            }

            return VisualQaCitationMetadataBuilder.FromMedicalCase(r.Case!, BuildMedicalCaseRagText(r.Case));
        }).ToList();

        return (merged, citations);
    }

    private static string BuildMedicalCaseRagText(MedicalCase mc) => MedicalCaseIndexingProcessor.BuildIndexingText(mc);

    /// <summary>
    /// Enriches the text used for vector retrieval so ROI and image-backed questions bias toward relevant chunks (SEPS Image + RAG).
    /// </summary>
    private static string BuildRagEmbeddingQuery(VisualQARequestDto request)
    {
        var q = request.QuestionText?.Trim() ?? string.Empty;
        if (HasRoiAnnotation(request))
        {
            var boxHint = TryFormatBboxRagHint(request);
            var roiLine =
                "[RAG query context: there is an ROI region (normalized rectangle) on the image; prioritize documents related to musculoskeletal imaging diagnosis at the marked area.]";
            return string.IsNullOrEmpty(boxHint)
                ? $"{q}\n\n{roiLine}"
                : $"{q}\n\n{roiLine}\n{boxHint}";
        }

        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return $"{q}\n\n[RAG query context: question includes a medical image.]";
        }

        return q;
    }

    private static bool HasRoiAnnotation(VisualQARequestDto request)
    {
        return BoundingBoxParser.TryParseFromJson(request.Coordinates) != null;
    }

    /// <summary>Short hint for the embedding query using Gemini-style 0–1000 box <c>[ymin, xmin, ymax, xmax]</c>.</summary>
    private static string? TryFormatBboxRagHint(VisualQARequestDto request)
    {
        var box = BoundingBoxParser.TryParseFromJson(request.Coordinates);
        if (box == null)
            return null;

        var (ymin, xmin, ymax, xmax) = box.Value.ToGeminiSpatialBox1000();
        return $"[ROI bounding box [ymin, xmin, ymax, xmax] = [{ymin}, {xmin}, {ymax}, {xmax}] on 0–1000 scale.]";
    }

    private static bool IsInvalidImageNotXrayResponse(string? answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText))
            return false;
        var t = answerText.Trim();
        return string.Equals(t, InvalidImageNotXrayToken, StringComparison.Ordinal)
               || t.Contains(InvalidImageNotXrayToken, StringComparison.Ordinal);
    }

    private static bool IsNonMedicalRefusalAnswer(string? answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText))
            return false;

        return answerText.Contains("not valid medical data", StringComparison.OrdinalIgnoreCase)
               || answerText.Contains("not related to the medical domain", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildGeminiPrompt(
        VisualQARequestDto request,
        IReadOnlyList<RagContextItem> ragItems,
        bool ragContextAdequate,
        MedicalCase? predefinedCase,
        int currentTurnNumber)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a Senior Radiologist.");
        sb.AppendLine("Answer questions based on the provided X-ray ROI and Knowledge Base.");
        sb.AppendLine();

        if (predefinedCase != null)
        {
            var tagText = predefinedCase.CaseTags
                .Where(ct => ct.Tag != null && !string.IsNullOrWhiteSpace(ct.Tag.Name))
                .Select(ct => ct.Tag.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .DefaultIfEmpty("N/A");

            sb.AppendLine("You are a Medical Tutor.");
            sb.AppendLine("Below is the OVERVIEW information for this case:");
            sb.AppendLine($"- Difficulty: {(string.IsNullOrWhiteSpace(predefinedCase.Difficulty) ? "N/A" : predefinedCase.Difficulty)}");
            sb.AppendLine($"- Tags: {string.Join(", ", tagText)}");
            if (!string.IsNullOrWhiteSpace(predefinedCase.Description))
                sb.AppendLine($"- Description: {predefinedCase.Description}");
            if (!string.IsNullOrWhiteSpace(predefinedCase.SuggestedDiagnosis))
                sb.AppendLine($"- Diagnosis: {predefinedCase.SuggestedDiagnosis}");
            if (!string.IsNullOrWhiteSpace(predefinedCase.KeyFindings))
                sb.AppendLine($"- Key findings: {predefinedCase.KeyFindings}");
            if (!string.IsNullOrWhiteSpace(predefinedCase.ReflectiveQuestions))
                sb.AppendLine($"- Reflective questions: {predefinedCase.ReflectiveQuestions}");
            sb.AppendLine("CRITICAL NOTE: DO NOT provide the diagnosis directly to the student immediately.");
            sb.AppendLine("Use the Socratic method; ask guiding questions based on 'ReflectiveQuestions' and 'KeyFindings' to lead the student to think independently.");
            sb.AppendLine($"Current student turn in this session: {currentTurnNumber}.");
            sb.AppendLine("Only provide the final answer when the student reaches turn 3 or gets stuck.");
            sb.AppendLine();
        }

        var hasImage = !string.IsNullOrWhiteSpace(request.ImageUrl);
        if (hasImage)
        {
            if (BoundingBoxParser.TryParseFromJson(request.Coordinates) is { } roiBox)
            {
                sb.AppendLine("An image is provided. A bright green rectangle outlines the region of interest on the image.");
                var (ymin, xmin, ymax, xmax) = roiBox.ToGeminiSpatialBox1000();
                sb.AppendLine(
                    $"The user has highlighted a region of interest using a bounding box [{ymin}, {xmin}, {ymax}, {xmax}]. Focus your clinical analysis strictly on the structures within this box.");
            }
            else if (!string.IsNullOrWhiteSpace(request.Coordinates))
            {
                sb.AppendLine("An image is provided with ROI metadata that could not be parsed as a normalized bounding box; use the green overlay (if visible) and the coordinate text only as hints.");
            }
            else
            {
                sb.AppendLine("An image is provided, but no ROI coordinates were given. Analyze the image carefully and answer using both the image and the retrieved context.");
            }
        }
        else
        {
            sb.AppendLine("No image is provided. Answer using only the retrieved context and the user question (do not rely on visual findings).");
        }
        sb.AppendLine();

        if (ragItems.Count > 0)
        {
            sb.AppendLine("The reference context below may be irrelevant. If it is unrelated to the question, ignore it.");
            sb.AppendLine();
            sb.AppendLine("## Retrieved reference context (documents + medical case library; use only to support your answer):");
            sb.AppendLine();

            for (var i = 0; i < ragItems.Count; i++)
            {
                var item = ragItems[i];
                sb.Append('[').Append(i + 1).Append("] ");
                if (item.Chunk != null)
                {
                    var chunk = item.Chunk;
                    sb.Append("(cite as [Doc:").Append(chunk.Id).Append("] in your answer; source: document_chunk, similarity≈")
                        .Append(item.Similarity.ToString("0.###")).Append(") ");
                    sb.AppendLine(chunk.Content ?? string.Empty);
                }
                else if (item.Case != null)
                {
                    var mc = item.Case;
                    sb.Append("(cite as [Case:").Append(mc.Id).Append("] in your answer; source: medical_case, similarity≈")
                        .Append(item.Similarity.ToString("0.###")).Append(") ");
                    sb.AppendLine(BuildMedicalCaseRagText(mc));
                }

                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("## Retrieved reference context");
            sb.AppendLine("No relevant document chunks or medical cases were retrieved from the system library for this query.");
            sb.AppendLine();
        }

        if (!ragContextAdequate)
        {
            sb.AppendLine("Note: Retrieved reference similarity is below the system's relevance threshold or chunks are missing. Follow the system instructions for general-knowledge mode.");
            sb.AppendLine();
        }

        sb.AppendLine("## User question");
        sb.AppendLine(request.QuestionText);
        sb.AppendLine();

        sb.AppendLine("If the question is explicitly binary and the evidence is decisive, you may begin with a concise yes/no conclusion. Otherwise explain the uncertainty instead of forcing a yes/no answer.");
        sb.AppendLine("Never change left/right laterality unless the image, ROI, retrieved context, or previous conversation explicitly justifies the change.");
        sb.AppendLine("You must reason, explain, and respond entirely in professional medical Vietnamese.");

        sb.AppendLine();
        sb.AppendLine("Format: Strict JSON only with fields: diagnosis, findings, differential_diagnoses, reflective_questions, citations.");
        sb.AppendLine("Use this exact structure (use null for optional fields when refusing or when not applicable):");
        sb.AppendLine(
            """
            {
              "diagnosis": "Primary diagnosis in Vietnamese. Use a binary lead-in only when the question is truly binary and the evidence is decisive.",
              "findings": ["Key imaging finding 1", "Key imaging finding 2"],
              "differential_diagnoses": ["Differential 1", "Differential 2"],
              "reflective_questions": ["Question 1", "Question 2"],
              "citations": [
                { "kind": "Doc", "id": "00000000-0000-0000-0000-000000000000" },
                { "kind": "Case", "id": "00000000-0000-0000-0000-000000000000" }
              ]
            }
            """);
        sb.AppendLine("The \"citations\" array must list every [Doc:...] and [Case:...] marker you relied on (kind is Doc or Case; id is the UUID). Use an empty array [] when no library sources were used.");

        return sb.ToString();
    }

    private async Task<(string? history, int userTurns)> BuildConversationHistoryAsync(Guid? sessionId, CancellationToken cancellationToken)
    {
        if (!sessionId.HasValue || sessionId.Value == Guid.Empty)
            return (null, 0);

        var messages = await _dbContext.QaMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId.Value)
            .Where(m => m.Role != "Lecturer" && m.Role != "Expert")
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => new
            {
                m.Role,
                m.Content,
                m.SuggestedDiagnosis,
                m.KeyImagingFindings,
                m.DifferentialDiagnoses,
                m.ReflectiveQuestions
            })
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return (null, 0);

        var userTurns = messages.Count(m => string.Equals(m.Role, "User", StringComparison.OrdinalIgnoreCase));

        var recentMessages = messages
            .TakeLast(6)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Previous Conversation:");
        foreach (var msg in recentMessages)
        {
            var role = string.Equals(msg.Role, "Assistant", StringComparison.OrdinalIgnoreCase)
                ? "Assistant"
                : "User";
            var content = role == "Assistant"
                ? BuildAssistantHistorySummary(
                    msg.Content,
                    msg.SuggestedDiagnosis,
                    msg.KeyImagingFindings,
                    msg.DifferentialDiagnoses,
                    msg.ReflectiveQuestions)
                : (msg.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(content))
                continue;
            sb.Append(role).Append(": ").AppendLine(content);
        }

        return (sb.Length == 0 ? null : sb.ToString().Trim(), userTurns);
    }

    private static string BuildAssistantHistorySummary(
        string? content,
        string? diagnosis,
        string? findings,
        string? differentialDiagnosesJson,
        string? reflectiveQuestions)
    {
        var parts = new List<string>();
        var normalizedDiagnosis = (diagnosis ?? content ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalizedDiagnosis))
            parts.Add($"Diagnosis: {normalizedDiagnosis}");

        var normalizedFindings = NormalizeMultiline(findings);
        if (!string.IsNullOrWhiteSpace(normalizedFindings))
            parts.Add($"Findings: {normalizedFindings}");

        var normalizedDifferentials = NormalizeJsonArray(differentialDiagnosesJson);
        if (!string.IsNullOrWhiteSpace(normalizedDifferentials))
            parts.Add($"Differentials: {normalizedDifferentials}");

        var normalizedReflective = NormalizeMultiline(reflectiveQuestions);
        if (!string.IsNullOrWhiteSpace(normalizedReflective))
            parts.Add($"ReflectiveQuestions: {normalizedReflective}");

        return string.Join(" | ", parts);
    }

    private static string? NormalizeMultiline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return string.Join("; ",
            value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().TrimStart('-', '*').Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string? NormalizeJsonArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(value);
            return parsed == null
                ? null
                : string.Join("; ", parsed.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        }
        catch
        {
            return NormalizeMultiline(value);
        }
    }

    private static double CalculateCosineSimilarity(float[] v1, float[] v2)
    {
        if (v1.Length == 0 || v2.Length == 0) return 0d;

        // Embeddings should normally be the same dimensionality (e.g., 768).
        // Still guard defensively in case of any upstream mismatch.
        var len = Math.Min(v1.Length, v2.Length);

        double dot = 0d;
        double mag1 = 0d;
        double mag2 = 0d;

        for (var i = 0; i < len; i++)
        {
            var a = v1[i];
            var b = v2[i];
            dot += (double)a * b;
            mag1 += (double)a * a;
            mag2 += (double)b * b;
        }

        var denom = Math.Sqrt(mag1) * Math.Sqrt(mag2);
        if (denom < 1e-12) return 0d;

        var cos = dot / denom;
        if (double.IsNaN(cos) || double.IsInfinity(cos)) return 0d;

        return cos;
    }

}
