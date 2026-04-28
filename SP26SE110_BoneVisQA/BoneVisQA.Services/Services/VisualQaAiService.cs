using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BoneVisQA.Services.Services;

public class VisualQaAiService : IVisualQaAiService
{
    private sealed record PreparedGeminiPipeline(
        string Prompt,
        string GeminiImagePayload,
        string? ConversationHistory,
        bool RagContextAdequate,
        double CalculatedScore,
        List<CitationItemDto> CitationsFromRag);

    private const double MinimumRelevantSimilarity = 0.72d;
    private const string InvalidImageNotXrayToken = "INVALID_IMAGE_NOT_XRAY";
    private const string InvalidBoneXrayUserMessage =
        "The system detected that this is not a valid human bone X-ray image. Please upload a proper medical X-ray image for analysis support.";
    private const string TemporaryVectorSearchUnavailableAnswer =
        "Retrieval service is temporarily unavailable. Please try again later.";
    private const string TemporaryAiGenerationUnavailableAnswer =
        "AI generation service is temporarily unavailable due to high network demand. Please try again later.";
    private const string AiOverloadVietnameseMessage =
        "The AI system is overloaded. Please try again later.";
    private const int RagTopMerged = 5;

    private readonly BoneVisQADbContext _dbContext;
    private readonly IPythonAiConnectorService _pythonAi;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGeminiService _geminiService;

    public VisualQaAiService(
        BoneVisQADbContext dbContext,
        IPythonAiConnectorService pythonAi,
        IHttpClientFactory httpClientFactory,
        IGeminiService geminiService)
    {
        _dbContext = dbContext;
        _pythonAi = pythonAi;
        _httpClientFactory = httpClientFactory;
        _geminiService = geminiService;
    }

    public async Task<VisualQAResponseDto> RunPipelineAsync(VisualQARequestDto request, CancellationToken cancellationToken = default)
    {
        var (earlyExit, prepared) = await TryPrepareGeminiPipelineAsync(request, cancellationToken);
        if (earlyExit != null)
            return AttachVisualQaCaseContext(request, earlyExit);

        VisualQAResponseDto response;
        try
        {
            response = await _geminiService.GenerateMedicalAnswerAsync(
                prepared!.Prompt,
                prepared.GeminiImagePayload,
                prepared.ConversationHistory,
                prepared.RagContextAdequate,
                cancellationToken);
        }
        catch (AiResponseFormatException)
        {
            throw;
        }
        catch
        {
            throw new InvalidOperationException(AiOverloadVietnameseMessage);
        }

        return FinalizeGeminiResponse(request, prepared!, response);
    }

    public async Task<VisualQaStreamingPipelineResult> RunStreamingPipelineAsync(
        VisualQARequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (earlyExit, prepared) = await TryPrepareGeminiPipelineAsync(request, cancellationToken);
        if (earlyExit != null)
        {
            return new VisualQaStreamingPipelineResult
            {
                TextDeltas = EmptyTextDeltas(cancellationToken),
                CompletedResponseAsync = Task.FromResult(AttachVisualQaCaseContext(request, earlyExit))
            };
        }

        var unavailable = _geminiService.TryGetUnavailableFallbackResponse();
        if (unavailable != null)
        {
            return new VisualQaStreamingPipelineResult
            {
                TextDeltas = EmptyTextDeltas(cancellationToken),
                CompletedResponseAsync = Task.FromResult(AttachVisualQaCaseContext(request, unavailable))
            };
        }

        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        var completion = new TaskCompletionSource<VisualQAResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = PumpStreamingGeminiPipelineAsync(channel.Writer, completion, prepared!, request, cancellationToken);

        return new VisualQaStreamingPipelineResult
        {
            TextDeltas = channel.Reader.ReadAllAsync(cancellationToken),
            CompletedResponseAsync = completion.Task
        };
    }

    private async Task<(VisualQAResponseDto? earlyExit, PreparedGeminiPipeline? prepared)> TryPrepareGeminiPipelineAsync(
        VisualQARequestDto request,
        CancellationToken cancellationToken)
    {
        string? imageB64 = null;
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            imageB64 = await TryDownloadImageAsBase64Async(request.ImageUrl, cancellationToken);

        var ragQueryText = BuildRagEmbeddingQuery(request);

        MedicalCase? predefinedCase = null;
        if (request.CaseId.HasValue && request.CaseId.Value != Guid.Empty)
        {
            predefinedCase = await _dbContext.MedicalCases
                .AsNoTracking()
                .Include(mc => mc.CaseTags)
                    .ThenInclude(ct => ct.Tag)
                .FirstOrDefaultAsync(mc => mc.Id == request.CaseId.Value, cancellationToken);
        }

        var filters = await ResolveHybridMetadataFiltersAsync(request, cancellationToken);

        var rag = await _pythonAi.AskRagAsync(
            ragQueryText,
            filters.Modality,
            filters.Anatomy,
            filters.PathologyGroup,
            cancellationToken);

        if (!rag.Success || string.IsNullOrWhiteSpace(rag.Prompt))
        {
            return (
                new VisualQAResponseDto
                {
                    AnswerText = TemporaryVectorSearchUnavailableAnswer,
                    AiConfidenceScore = null,
                    ClientRequestId = request.ClientRequestId,
                    ResponseKind = "error",
                    Citations = new List<CitationItemDto>()
                },
                null);
        }

        var pythonHybridPrompt = rag.Prompt;
        var retrievalCount = rag.RetrievalCount;

        var similarities = new List<double>();
        foreach (var item in rag.Context)
        {
            var d = item.Distance;
            var sim = Math.Clamp(1.0d - d / 2.0d, 0d, 1d);
            similarities.Add(sim);
        }

        var maxSimilarity = similarities.Count > 0 ? similarities.Max() : 0d;
        var calculatedScore = similarities.Count > 0 ? similarities.Average() : 0.5d;
        var ragContextAdequate = retrievalCount > 0 && maxSimilarity >= MinimumRelevantSimilarity;

        var citationsFromRag = BuildCitationsFromRagContext(rag.Context);

        var (conversationHistory, existingUserTurns) = await BuildConversationHistoryAsync(request.SessionId, cancellationToken);
        var currentTurnNumber = existingUserTurns + 1;
        var prompt = BuildGeminiPrompt(request, pythonHybridPrompt, ragContextAdequate, predefinedCase, currentTurnNumber);

        return (null, new PreparedGeminiPipeline(
            prompt,
            imageB64 ?? string.Empty,
            conversationHistory,
            ragContextAdequate,
            calculatedScore,
            citationsFromRag));
    }

    private async Task<(string Modality, string Anatomy, string? PathologyGroup)> ResolveHybridMetadataFiltersAsync(
        VisualQARequestDto request,
        CancellationToken cancellationToken)
    {
        const string defaultMod = "X-Ray";
        const string defaultAna = "Lower Limb";
        if (request.CaseId is not { } cid || cid == Guid.Empty)
            return (defaultMod, defaultAna, null);

        var conn = _dbContext.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            await conn.OpenAsync(cancellationToken);

        try
        {
            if (conn is not NpgsqlConnection nconn)
                return (defaultMod, defaultAna, null);

            await using var cmd = new NpgsqlCommand(
                """
                SELECT modality, anatomy, pathology_group
                FROM case_metadata
                WHERE case_id = @cid
                LIMIT 1
                """,
                nconn);
            cmd.Parameters.AddWithValue("cid", cid);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return (defaultMod, defaultAna, null);

            var mod = reader.IsDBNull(0) ? defaultMod : reader.GetString(0);
            var ana = reader.IsDBNull(1) ? defaultAna : reader.GetString(1);
            string? pg = reader.IsDBNull(2) ? null : reader.GetString(2);
            return (mod, ana, pg);
        }
        catch (Exception)
        {
            return (defaultMod, defaultAna, null);
        }
        finally
        {
            if (!wasOpen)
                await conn.CloseAsync();
        }
    }

    private async Task<string?> TryDownloadImageAsBase64Async(string imageUrl, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("VisualQaImageFetch");
            var bytes = await client.GetByteArrayAsync(new Uri(imageUrl), cancellationToken);
            return bytes.Length == 0 ? null : Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static List<CitationItemDto> BuildCitationsFromRagContext(IReadOnlyList<RagContextItemDto> blocks)
    {
        var list = new List<CitationItemDto>();
        foreach (var item in blocks)
        {
            var source = item.Source;
            var refId = item.RefId;
            var excerpt = item.Excerpt;

            if (string.IsNullOrWhiteSpace(refId))
                continue;

            var citation = new CitationItemDto
            {
                SourceText = excerpt,
                Snippet = excerpt,
                Kind = source ?? "doc"
            };

            if (string.Equals(source, "doc_chunk", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(refId, out var chunkId))
            {
                citation.ChunkId = chunkId;
            }
            else if (string.Equals(source, "case_text", StringComparison.OrdinalIgnoreCase)
                     && Guid.TryParse(refId, out var caseId))
            {
                citation.MedicalCaseId = caseId;
            }

            list.Add(citation);
        }

        return list;
    }

    private VisualQAResponseDto FinalizeGeminiResponse(
        VisualQARequestDto request,
        PreparedGeminiPipeline prepared,
        VisualQAResponseDto response)
    {
        if (IsInvalidImageNotXrayResponse(response.AnswerText))
        {
            return AttachVisualQaCaseContext(request, new VisualQAResponseDto
            {
                AnswerText = InvalidBoneXrayUserMessage,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = prepared.CalculatedScore,
                ResponseKind = "refusal",
                PolicyReason = "invalid_image",
                ClientRequestId = request.ClientRequestId,
                Citations = new List<CitationItemDto>()
            });
        }

        var isNonMedicalRefusal = IsNonMedicalRefusalAnswer(response.AnswerText);

        if (isNonMedicalRefusal)
        {
            response.Citations = new List<CitationItemDto>();
            response.SuggestedDiagnosis = null;
            response.DifferentialDiagnoses = null;
            response.KeyImagingFindings = null;
            response.ReflectiveQuestions = null;
            response.ResponseKind = "refusal";
            response.PolicyReason = "off_topic";
        }
        else
        {
            if (response.Citations == null || response.Citations.Count == 0)
            {
                response.Citations = prepared.CitationsFromRag
                    .Take(RagTopMerged)
                    .ToList();
            }
            else
            {
                response.Citations = FilterCitationsAgainstContext(response.Citations, prepared.CitationsFromRag);
                var metaByChunkId = prepared.CitationsFromRag
                    .Where(c => c.MedicalCaseId == null && c.ChunkId != Guid.Empty)
                    .ToDictionary(c => c.ChunkId, c => c);
                var metaByCaseId = prepared.CitationsFromRag
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

                response.Citations = response.Citations
                    .Take(RagTopMerged)
                    .ToList();
            }
        }

        response.AiConfidenceScore = prepared.CalculatedScore;
        response.ClientRequestId = request.ClientRequestId;
        response.ResponseKind = string.IsNullOrWhiteSpace(response.ResponseKind) ? "analysis" : response.ResponseKind;
        response.PolicyReason ??= "medical_intent";

        return AttachVisualQaCaseContext(request, response);
    }

    private static VisualQAResponseDto AttachVisualQaCaseContext(VisualQARequestDto request, VisualQAResponseDto response)
    {
        response.CaseId = NormalizeVisualQaCaseId(request.CaseId);
        return response;
    }

    private static Guid? NormalizeVisualQaCaseId(Guid? caseId) =>
        caseId.HasValue && caseId.Value != Guid.Empty ? caseId : null;

    private async Task PumpStreamingGeminiPipelineAsync(
        ChannelWriter<string> writer,
        TaskCompletionSource<VisualQAResponseDto> completion,
        PreparedGeminiPipeline prepared,
        VisualQARequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var sb = new StringBuilder();
            await foreach (var delta in _geminiService.StreamMedicalAnswerRawAsync(
                               prepared.Prompt,
                               prepared.GeminiImagePayload,
                               prepared.ConversationHistory,
                               prepared.RagContextAdequate,
                               cancellationToken))
            {
                sb.Append(delta);
                await writer.WriteAsync(delta, cancellationToken).ConfigureAwait(false);
            }

            writer.TryComplete();

            var raw = sb.ToString();
            VisualQAResponseDto parsed;
            try
            {
                parsed = _geminiService.ParseMedicalAnswerFromRawResponse(raw);
            }
            catch (AiResponseFormatException ex)
            {
                completion.TrySetException(ex);
                return;
            }

            completion.TrySetResult(FinalizeGeminiResponse(request, prepared, parsed));
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
            completion.TrySetException(ex);
        }
    }

    private static async IAsyncEnumerable<string> EmptyTextDeltas([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        _ = cancellationToken;
        yield break;
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

    /// <summary>Dynamic response language for Gemini (controlled by client locale / Accept-Language).</summary>
    private static void AppendResponseLanguageInstruction(StringBuilder sb, string instructionLanguageName)
    {
        sb.Append("You must reason, explain, and respond strictly in ");
        sb.Append(instructionLanguageName);
        sb.AppendLine(". However, you are permitted and encouraged to retain standard Latin medical terminology and specific bone disease names without forcing translation if it compromises accuracy.");
    }

    private static string BuildGeminiPrompt(
        VisualQARequestDto request,
        string pythonHybridRagPrompt,
        bool ragContextAdequate,
        MedicalCase? predefinedCase,
        int currentTurnNumber)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a Senior Radiologist.");
        sb.AppendLine("Answer questions based on the provided medical image (if any) and the hybrid-filtered retrieval context from BoneVisQA.AI.");
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
                var (ymin, xmin, ymax, xmax) = roiBox.ToGeminiSpatialBox1000();
                sb.AppendLine(
                    $"An image URL is provided. Normalized ROI box on 0–1000 scale: [{ymin}, {xmin}, {ymax}, {xmax}]. Focus on structures within this region when interpreting the image.");
            }
            else if (!string.IsNullOrWhiteSpace(request.Coordinates))
            {
                sb.AppendLine("An image is provided with ROI metadata that could not be parsed as a normalized bounding box; treat coordinates only as hints.");
            }
            else
            {
                sb.AppendLine("An image URL is provided without ROI coordinates. Analyze the full image together with retrieved context.");
            }
        }
        else
        {
            sb.AppendLine("No image is provided. Answer using retrieved context and the user question (do not rely on visual findings).");
        }
        sb.AppendLine();

        sb.AppendLine("## Hybrid RAG prompt (Python BoneVisQA.AI)");
        sb.AppendLine(string.IsNullOrWhiteSpace(pythonHybridRagPrompt)
            ? "(empty — answer from general principles and state uncertainty explicitly.)"
            : pythonHybridRagPrompt);
        sb.AppendLine();

        if (!ragContextAdequate)
        {
            sb.AppendLine("Note: Retrieved reference similarity is below the system's relevance threshold or chunks are missing. Follow general-knowledge mode.");
            sb.AppendLine();
        }

        sb.AppendLine("If the question is explicitly binary and the evidence is decisive, you may begin with a concise yes/no conclusion. Otherwise explain the uncertainty instead of forcing a yes/no answer.");
        sb.AppendLine("Never change left/right laterality unless the image, ROI, retrieved context, or previous conversation explicitly justifies the change.");
        sb.AppendLine("For follow-up questions that verify/compare with previous answers, preserve prior conclusions unless new evidence in image/ROI/context clearly contradicts them.");
        sb.AppendLine("If the user question is social/off-topic and not medical, do not analyze image content and return a refusal according to system policy.");

        var instructionLang = VisualQaPromptLanguage.GetInstructionLanguageName(request.Language);
        AppendResponseLanguageInstruction(sb, instructionLang);

        sb.AppendLine();
        sb.AppendLine("Format: Strict JSON only with fields: diagnosis, findings, differential_diagnoses, reflective_questions, citations.");
        sb.AppendLine("Use this exact structure (use null for optional fields when refusing or when not applicable):");
        sb.AppendLine("{");
        sb.Append("  \"diagnosis\": \"Primary diagnosis in ").Append(instructionLang)
            .AppendLine(". Use a binary lead-in only when the question is truly binary and the evidence is decisive.\",");
        sb.AppendLine("  \"findings\": [\"Key imaging finding 1\", \"Key imaging finding 2\"],");
        sb.AppendLine("  \"differential_diagnoses\": [\"Differential 1\", \"Differential 2\"],");
        sb.AppendLine("  \"reflective_questions\": [\"Question 1\", \"Question 2\"],");
        sb.AppendLine("  \"citations\": [");
        sb.AppendLine("    { \"kind\": \"Doc\", \"id\": \"00000000-0000-0000-0000-000000000000\" },");
        sb.AppendLine("    { \"kind\": \"Case\", \"id\": \"00000000-0000-0000-0000-000000000000\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
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
        var normalizedBody = (content ?? string.Empty).Trim();
        var normalizedDiagnosis = (diagnosis ?? string.Empty).Trim();
        var distinctDiagnosis = !string.IsNullOrWhiteSpace(normalizedDiagnosis)
            && !string.Equals(normalizedDiagnosis, normalizedBody, StringComparison.Ordinal);
        if (distinctDiagnosis)
            parts.Add($"Diagnosis: {normalizedDiagnosis}");
        if (!string.IsNullOrWhiteSpace(normalizedBody))
            parts.Add(distinctDiagnosis ? $"Answer: {normalizedBody}" : normalizedBody);

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

}
