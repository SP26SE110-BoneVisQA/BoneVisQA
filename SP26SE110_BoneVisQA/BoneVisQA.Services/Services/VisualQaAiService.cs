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

namespace BoneVisQA.Services.Services;

public class VisualQaAiService : IVisualQaAiService
{
    private sealed record CaseOntologyPrompt(
        string Modality,
        string AnatomySite,
        string? PathologyGroup,
        string? Laterality,
        string? ViewPosition,
        string? Difficulty,
        string? SourceType,
        double? QualityScore);

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

        var hybrid = await ResolveHybridCaseContextAsync(request, cancellationToken);
        var dicomMetadata = await ResolveDicomMetadataAsync(request, cancellationToken);
        var dicomClinicalContext = DicomClinicalContextHelper.BuildPromptBlock(dicomMetadata);
        var caseMediaId = await ResolveCaseMediaIdAsync(request, cancellationToken);

        var rag = await _pythonAi.AskRagAsync(
            ragQueryText,
            hybrid.Modality,
            hybrid.Anatomy,
            hybrid.PathologyGroup,
            request.CaseId,
            caseMediaId,
            imageEmbedding: null,
            dicomClinicalContext,
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

        var citationsFromRag = await BuildCitationsFromRagContextAsync(rag.Context, cancellationToken);

        var (conversationHistory, existingUserTurns) = await BuildConversationHistoryAsync(request.SessionId, cancellationToken);
        var currentTurnNumber = existingUserTurns + 1;
        var prompt = BuildGeminiPrompt(
            request,
            pythonHybridPrompt,
            ragContextAdequate,
            predefinedCase,
            currentTurnNumber,
            hybrid.Ontology,
            dicomClinicalContext);

        return (null, new PreparedGeminiPipeline(
            prompt,
            imageB64 ?? string.Empty,
            conversationHistory,
            ragContextAdequate,
            calculatedScore,
            citationsFromRag));
    }

    private async Task<(string Modality, string Anatomy, string? PathologyGroup, CaseOntologyPrompt? Ontology)> ResolveHybridCaseContextAsync(
        VisualQARequestDto request,
        CancellationToken cancellationToken)
    {
        const string defaultMod = "X-Ray";
        const string defaultAna = "Lower Limb";
        if (request.CaseId is not { } cid || cid == Guid.Empty)
            return (defaultMod, defaultAna, null, null);

        var meta = await _dbContext.CaseMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CaseId == cid, cancellationToken);

        if (meta == null)
            return (defaultMod, defaultAna, null, null);

        var mod = string.IsNullOrWhiteSpace(meta.Modality) ? defaultMod : meta.Modality;
        var ana = !string.IsNullOrWhiteSpace(meta.AnatomySite)
            ? meta.AnatomySite.Trim()
            : (string.IsNullOrWhiteSpace(meta.Anatomy) ? defaultAna : meta.Anatomy);
        var pg = string.IsNullOrWhiteSpace(meta.PathologyGroup) ? null : meta.PathologyGroup;

        var ontology = new CaseOntologyPrompt(
            mod,
            ana,
            pg,
            meta.Laterality,
            meta.ViewPosition,
            meta.Difficulty,
            meta.SourceType,
            meta.QualityScore);

        return (mod, ana, pg, ontology);
    }

    private async Task<JsonElement?> ResolveDicomMetadataAsync(
        VisualQARequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.DicomMetadata is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined })
            return request.DicomMetadata;

        if (request.CaseId is not { } caseId || caseId == Guid.Empty)
            return null;

        var json = await _dbContext.CaseMedia
            .AsNoTracking()
            .Where(m => m.CaseId == caseId && m.DicomMetadata != null)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => m.DicomMetadata)
            .FirstOrDefaultAsync(cancellationToken);

        return DicomClinicalContextHelper.TryParseJson(json);
    }

    private async Task<Guid?> ResolveCaseMediaIdAsync(
        VisualQARequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.CaseId is not { } caseId || caseId == Guid.Empty)
            return null;

        return await _dbContext.CaseMedia
            .AsNoTracking()
            .Where(m => m.CaseId == caseId)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => m.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string?> TryDownloadImageAsBase64Async(string imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var trimmed = imageUrl.Trim();

        try
        {
            if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(trimmed);
                var localPath = uri.LocalPath;
                if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                    return null;
                var fileBytes = await File.ReadAllBytesAsync(localPath, cancellationToken);
                return fileBytes.Length == 0 ? null : Convert.ToBase64String(fileBytes);
            }

            if (LooksLikeLocalFilesystemPath(trimmed))
            {
                var expanded = Environment.ExpandEnvironmentVariables(trimmed);
                if (!File.Exists(expanded))
                    return null;
                var fileBytes = await File.ReadAllBytesAsync(expanded, cancellationToken);
                return fileBytes.Length == 0 ? null : Convert.ToBase64String(fileBytes);
            }

            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var client = _httpClientFactory.CreateClient("VisualQaImageFetch");
                var bytes = await client.GetByteArrayAsync(new Uri(trimmed), cancellationToken);
                return bytes.Length == 0 ? null : Convert.ToBase64String(bytes);
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var abs)
                && (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            {
                var client = _httpClientFactory.CreateClient("VisualQaImageFetch");
                var bytes = await client.GetByteArrayAsync(abs, cancellationToken);
                return bytes.Length == 0 ? null : Convert.ToBase64String(bytes);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool LooksLikeLocalFilesystemPath(string path)
    {
        if (path.Length < 2)
            return false;

        if (path[0] == '/' && !path.StartsWith("//", StringComparison.Ordinal))
            return true;

        if (path.Length >= 3
            && char.IsLetter(path[0])
            && path[1] == ':'
            && (path[2] == '\\' || path[2] == '/'))
            return true;

        return path.StartsWith("\\\\", StringComparison.Ordinal);
    }

    private async Task<List<CitationItemDto>> BuildCitationsFromRagContextAsync(
        IReadOnlyList<RagContextItemDto> blocks,
        CancellationToken cancellationToken)
    {
        var list = new List<CitationItemDto>();
        foreach (var item in blocks)
        {
            var source = item.Source?.Trim();
            var refId = item.RefId?.Trim();
            var excerpt = item.Excerpt;

            if (string.IsNullOrWhiteSpace(refId) || !Guid.TryParse(refId, out var refGuid) || refGuid == Guid.Empty)
                continue;

            if (string.Equals(source, "doc_chunk", StringComparison.OrdinalIgnoreCase))
            {
                var chunk = await _dbContext.DocumentChunks
                    .AsNoTracking()
                    .Include(c => c.Doc)
                    .FirstOrDefaultAsync(c => c.Id == refGuid, cancellationToken);
                if (chunk == null)
                    continue;

                var citation = VisualQaCitationMetadataBuilder.FromDocumentChunk(chunk);
                if (!string.IsNullOrWhiteSpace(excerpt))
                {
                    citation.SourceText = excerpt;
                    citation.Snippet = VisualQaCitationMetadataBuilder.BuildSnippet(excerpt);
                }

                list.Add(citation);
                continue;
            }

            if (string.Equals(source, "case_text", StringComparison.OrdinalIgnoreCase))
            {
                var medicalCase = await _dbContext.MedicalCases
                    .AsNoTracking()
                    .FirstOrDefaultAsync(mc => mc.Id == refGuid, cancellationToken);
                if (medicalCase == null)
                    continue;

                list.Add(VisualQaCitationMetadataBuilder.FromMedicalCase(medicalCase, excerpt));
            }
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
            var modelCitations = response.Citations ?? new List<CitationItemDto>();
            var filteredModelCitations = modelCitations.Count > 0
                ? FilterCitationsAgainstContext(modelCitations, prepared.CitationsFromRag)
                : new List<CitationItemDto>();
            response.Citations = (filteredModelCitations.Count > 0 ? filteredModelCitations : prepared.CitationsFromRag)
                .Take(RagTopMerged)
                .ToList();
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
        int currentTurnNumber,
        CaseOntologyPrompt? caseOntology,
        string? dicomClinicalContext)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an expert musculoskeletal radiologist.");
        sb.AppendLine("Answer questions based on the provided medical image (if any) and the hybrid-filtered retrieval context from BoneVisQA.AI.");
        sb.AppendLine();
        sb.AppendLine("## Persona and JSON field boundaries (STRICT)");
        sb.AppendLine("- The `diagnosis` field MUST contain a declarative clinical statement or your best musculoskeletal assessment.");
        sb.AppendLine("- DO NOT ask questions in `diagnosis`. Never write interrogative sentences, prompts to the student, or Socratic questions there.");
        sb.AppendLine("- Put ALL student-facing questions ONLY in `reflective_questions` (array of strings).");
        sb.AppendLine("- Use `findings` for objective imaging observations and `differential_diagnoses` for ranked alternatives.");
        sb.AppendLine("- Example WRONG diagnosis: \"What is the exact diagnosis of this case?\" — that belongs in `reflective_questions`, not `diagnosis`.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(dicomClinicalContext))
        {
            sb.AppendLine("## DICOM-derived clinical context");
            sb.AppendLine(dicomClinicalContext.Trim());
            sb.AppendLine();
        }

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
            sb.AppendLine("Socratic / guiding questions MUST go in the JSON `reflective_questions` array — NEVER in `diagnosis`.");
            sb.AppendLine("Even when withholding the full answer, `diagnosis` must still be a short declarative clinical impression for this turn (not a question).");
            sb.AppendLine($"Current student turn in this session: {currentTurnNumber}.");
            sb.AppendLine("Only provide the final answer when the student reaches turn 3 or gets stuck.");
            sb.AppendLine();
        }

        if (caseOntology != null)
        {
            sb.AppendLine("## Structured case_metadata (clinical ontology axes)");
            sb.AppendLine($"- Modality: {caseOntology.Modality}");
            sb.AppendLine($"- Anatomy site: {caseOntology.AnatomySite}");
            sb.AppendLine($"- Pathology group: {caseOntology.PathologyGroup ?? "N/A"}");
            sb.AppendLine($"- Laterality: {caseOntology.Laterality ?? "N/A"}");
            sb.AppendLine($"- View / position: {caseOntology.ViewPosition ?? "N/A"}");
            sb.AppendLine($"- Case difficulty: {caseOntology.Difficulty ?? "N/A"}");
            sb.AppendLine($"- Source type: {caseOntology.SourceType ?? "N/A"}");
            sb.AppendLine($"- Quality score: {(caseOntology.QualityScore?.ToString("0.###", CultureInfo.InvariantCulture) ?? "N/A")}");
            sb.AppendLine("Use these axes to structure differential reasoning and to avoid modality/anatomy contradictions.");
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
            sb.AppendLine("## Library retrieval status");
            sb.AppendLine(
                "No sufficiently similar approved cases or knowledge-base documents were retrieved from the BoneVisQA library for this question. " +
                "State this clearly to the student in Vietnamese, then answer using the image (if any) and general musculoskeletal reasoning. " +
                "Do not invent [Doc:...] or [Case:...] citations; return \"citations\": [] only when no real UUIDs appear in the hybrid RAG block above.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("## Citation requirement (MANDATORY when library context is provided)");
            sb.AppendLine(
                "Medical context from the BoneVisQA database is included in the Hybrid RAG block above. " +
                "You MUST populate the `citations` array with every [Doc:UUID] and [Case:UUID] reference you relied on from that block. " +
                "Do NOT return \"citations\": [] when retrieval context lists document or case UUIDs that support your answer. " +
                "If no retrieved chunk directly matches the question, cite any partially relevant Doc/Case UUIDs from the block; " +
                "if none apply, state clearly in `diagnosis` that you are relying on standard musculoskeletal medical knowledge and return \"citations\": []. " +
                "Each citation object must use { \"kind\": \"Doc\"|\"Case\", \"id\": \"<uuid>\" } matching markers in the context.");
            sb.AppendLine();
        }

        sb.AppendLine("If the question is explicitly binary and the evidence is decisive, you may begin with a concise yes/no conclusion. Otherwise explain the uncertainty instead of forcing a yes/no answer.");
        sb.AppendLine("Never change left/right laterality unless the image, ROI, retrieved context, or previous conversation explicitly justifies the change.");
        sb.AppendLine("For follow-up questions that verify/compare with previous answers, preserve prior conclusions unless new evidence in image/ROI/context clearly contradicts them.");
        sb.AppendLine("If the user question is social/off-topic and not medical, do not analyze image content and return a refusal according to system policy.");

        var instructionLang = VisualQaPromptLanguage.GetInstructionLanguageName(request.ResolvedResponseLanguage);
        AppendResponseLanguageInstruction(sb, instructionLang);

        sb.AppendLine();
        sb.AppendLine("The frontend parser requires EXACTLY five JSON fields in this exact order:");
        sb.AppendLine("1. diagnosis");
        sb.AppendLine("2. differential_diagnoses");
        sb.AppendLine("3. findings");
        sb.AppendLine("4. reflective_questions");
        sb.AppendLine("5. citations");
        sb.AppendLine("Return RAW JSON ONLY. Do not return Markdown, headings, prose wrappers, leading commentary, code fences, or ```json.");
        sb.AppendLine("Use this exact structure (use null for optional fields when refusing or when not applicable):");
        sb.AppendLine("{");
        sb.Append("  \"diagnosis\": \"Main diagnosis in ").Append(instructionLang)
            .AppendLine(". Must be a declarative clinical statement — never a question.\",");
        sb.AppendLine("  \"differential_diagnoses\": [\"Differential 1\", \"Differential 2\"],");
        sb.AppendLine("  \"findings\": [\"Key sign 1\", \"Key sign 2\"],");
        sb.AppendLine("  \"reflective_questions\": [\"Question 1\", \"Question 2\"],");
        sb.AppendLine("  \"citations\": [");
        sb.AppendLine("    { \"kind\": \"Doc\", \"id\": \"00000000-0000-0000-0000-000000000000\" },");
        sb.AppendLine("    { \"kind\": \"Case\", \"id\": \"00000000-0000-0000-0000-000000000000\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("Every non-refusal answer must include all five keys.");
        if (ragContextAdequate)
        {
            sb.AppendLine("Because library retrieval context was provided, `citations` MUST list the Doc/Case UUIDs you used — do not leave it empty when sources exist.");
        }
        else
        {
            sb.AppendLine("When no library UUIDs were supplied in context, you MUST return \"citations\": [] (empty array, not null and not omitted).");
        }
        sb.AppendLine("The \"citations\" array must list every [Doc:...] and [Case:...] marker you relied on (kind is Doc or Case; id is the UUID).");

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
