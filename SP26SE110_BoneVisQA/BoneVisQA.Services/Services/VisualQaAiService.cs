using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Models;
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
        "Hệ thống phát hiện đây không phải là ảnh X-quang xương người hoặc ảnh không hợp lệ. Vui lòng tải lên đúng hình ảnh X-quang y tế để được hỗ trợ phân tích.";
    private const string RagGeneralKnowledgeDisclaimer =
        "(Lưu ý: Phân tích này dựa trên kiến thức AI tổng quát vì không tìm thấy tài liệu tham chiếu trực tiếp trong thư viện của hệ thống).";
    private const string TemporaryVectorSearchUnavailableAnswer =
        "Vector search is temporarily unavailable due to high network demand. Please try again later.";
    private const string TemporaryAiGenerationUnavailableAnswer =
        "AI generation service is temporarily unavailable due to high network demand. Please try again later.";
    private const string AiOverloadVietnameseMessage =
        "Hệ thống AI đang quá tải. Vui lòng thử lại sau.";

    private const int RagChunkFetch = 12;
    private const int RagMaxCasesToEmbed = 40;
    private const int RagTopMerged = 8;

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
                    }
                    else if (metaByChunkId.TryGetValue(citation.ChunkId, out var meta))
                    {
                        citation.SourceText = meta.SourceText;
                        citation.ReferenceUrl = meta.ReferenceUrl;
                        citation.PageNumber = meta.PageNumber;
                    }
                }
            }
        }

        response.AiConfidenceScore = calculatedScore;

        return response;
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

        var caseRows = await _dbContext.MedicalCases
            .AsNoTracking()
            .Where(mc => mc.IsApproved == true && mc.IsActive == true)
            .Where(mc => caseCategoryId == null || mc.CategoryId == caseCategoryId)
            .Where(mc => excludeCaseId == null || mc.Id != excludeCaseId.Value)
            .OrderByDescending(mc => mc.UpdatedAt)
            .Take(RagMaxCasesToEmbed)
            .ToListAsync(cancellationToken);

        var caseItems = new List<RagContextItem>();
        using var semaphore = new SemaphoreSlim(8);
        try
        {
            var tasks = caseRows.Select(async mc =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var text = BuildMedicalCaseRagText(mc);
                    if (string.IsNullOrWhiteSpace(text))
                        return null;

                    var emb = await _embeddingService.EmbedTextAsync(text, cancellationToken).ConfigureAwait(false);
                    var sim = CalculateCosineSimilarity(queryEmbedding, emb);
                    return new RagContextItem(sim, null, mc);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            foreach (var t in tasks)
            {
                var item = await t.ConfigureAwait(false);
                if (item != null)
                    caseItems.Add(item);
            }
        }
        catch
        {
            throw new InvalidOperationException(AiOverloadVietnameseMessage);
        }

        var merged = chunkItems
            .Concat(caseItems)
            .OrderByDescending(x => x.Similarity)
            .Take(RagTopMerged)
            .ToList();

        var citations = merged.Select(r =>
        {
            if (r.Chunk != null)
            {
                return new CitationItemDto
                {
                    ChunkId = r.Chunk.Id,
                    MedicalCaseId = null,
                    ReferenceUrl = BuildCitationUrl(r.Chunk.Doc?.FilePath, r.Chunk.ChunkOrder),
                    PageNumber = r.Chunk.ChunkOrder + 1,
                    SourceText = r.Chunk.Content
                };
            }

            return new CitationItemDto
            {
                ChunkId = Guid.Empty,
                MedicalCaseId = r.Case!.Id,
                ReferenceUrl = null,
                PageNumber = null,
                SourceText = BuildMedicalCaseRagText(r.Case)
            };
        }).ToList();

        return (merged, citations);
    }

    private static string BuildMedicalCaseRagText(MedicalCase mc)
    {
        var desc = mc.Description?.Trim() ?? string.Empty;
        var dx = mc.SuggestedDiagnosis?.Trim() ?? string.Empty;
        var title = mc.Title?.Trim() ?? string.Empty;
        return $"{title}\n{desc}\n{dx}".Trim();
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
                "[Ngữ cảnh truy vấn RAG: có vùng ROI (hình chữ nhật chuẩn hóa) trên ảnh; ưu tiên tài liệu liên quan chẩn đoán hình ảnh cơ xương khớp tại vùng được đánh dấu.]";
            return string.IsNullOrEmpty(boxHint)
                ? $"{q}\n\n{roiLine}"
                : $"{q}\n\n{roiLine}\n{boxHint}";
        }

        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return $"{q}\n\n[Ngữ cảnh truy vấn RAG: câu hỏi kèm hình ảnh y khoa.]";
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

        return answerText.Contains("không phải dữ liệu y khoa hợp lệ", StringComparison.OrdinalIgnoreCase)
               || answerText.Contains("không liên quan đến lĩnh vực y khoa", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildGeminiPrompt(
        VisualQARequestDto request,
        IReadOnlyList<RagContextItem> ragItems,
        bool ragContextAdequate,
        MedicalCase? predefinedCase,
        int currentTurnNumber)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an Expert Radiologist assisting medical students.");
        sb.AppendLine();

        if (predefinedCase != null)
        {
            var tagText = predefinedCase.CaseTags
                .Where(ct => ct.Tag != null && !string.IsNullOrWhiteSpace(ct.Tag.Name))
                .Select(ct => ct.Tag.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .DefaultIfEmpty("N/A");

            sb.AppendLine("Bạn là một Gia sư Y khoa (Medical Tutor).");
            sb.AppendLine("Dưới đây là thông tin TỔNG QUAN của ca bệnh này:");
            sb.AppendLine($"- Độ khó: {(string.IsNullOrWhiteSpace(predefinedCase.Difficulty) ? "N/A" : predefinedCase.Difficulty)}");
            sb.AppendLine($"- Nhãn: {string.Join(", ", tagText)}");
            if (!string.IsNullOrWhiteSpace(predefinedCase.Description))
                sb.AppendLine($"- Description: {predefinedCase.Description}");
            if (!string.IsNullOrWhiteSpace(predefinedCase.SuggestedDiagnosis))
                sb.AppendLine($"- Chẩn đoán: {predefinedCase.SuggestedDiagnosis}");
            if (!string.IsNullOrWhiteSpace(predefinedCase.KeyFindings))
                sb.AppendLine($"- Dấu hiệu chính: {predefinedCase.KeyFindings}");
            if (!string.IsNullOrWhiteSpace(predefinedCase.ReflectiveQuestions))
                sb.AppendLine($"- Câu hỏi phản tư: {predefinedCase.ReflectiveQuestions}");
            sb.AppendLine("LƯU Ý QUAN TRỌNG TỐI CAO: KHÔNG ĐƯỢC trả lời thẳng đáp án (Diagnosis) ngay lập tức cho sinh viên.");
            sb.AppendLine("Hãy dùng phương pháp Socratic, đặt câu hỏi ngược lại dựa trên 'ReflectiveQuestions' và 'KeyFindings' để dẫn dắt sinh viên tự suy nghĩ.");
            sb.AppendLine($"Lượt hiện tại của sinh viên trong phiên này: {currentTurnNumber}.");
            sb.AppendLine("Chỉ đưa ra đáp án chốt khi sinh viên đã hỏi đến lượt thứ 3 hoặc bế tắc.");
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
            sb.AppendLine("Dữ liệu tham khảo (Context) dưới đây có thể không liên quan. Nếu thấy không liên quan đến câu hỏi, hãy bỏ qua nó.");
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
                    sb.Append("(source: document_chunk, chunkId: ").Append(chunk.Id).Append(", similarity≈")
                        .Append(item.Similarity.ToString("0.###")).Append(") ");
                    sb.AppendLine(chunk.Content ?? string.Empty);
                }
                else if (item.Case != null)
                {
                    var mc = item.Case;
                    sb.Append("(source: medical_case, caseId: ").Append(mc.Id).Append(", similarity≈")
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

        // Hard requirement: Vietnamese only, inserted immediately before JSON requirement.
        sb.AppendLine("Bạn bắt buộc phải suy luận, giải thích và trả lời hoàn toàn bằng Tiếng Việt (Vietnamese) theo đúng chuẩn Y khoa.");

        sb.AppendLine();
        sb.AppendLine("You must respond with a valid JSON object only, no other text. Use this exact structure (use null for optional fields when refusing or when not applicable):");
        sb.AppendLine(@"
{
  ""answerText"": ""Your full educational answer here."",
  ""suggestedDiagnosis"": ""Primary suggested diagnosis or null."",
  ""differentialDiagnoses"": ""Other differential diagnoses or null."",
  ""keyImagingFindings"": ""Key imaging signs to focus on, or null."",
  ""reflectiveQuestions"": ""1-3 reflective questions for the student, or null.""
}
");

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
            .Select(m => new { m.Role, m.Content })
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return (null, 0);

        var userTurns = messages.Count(m => string.Equals(m.Role, "User", StringComparison.OrdinalIgnoreCase));

        var sb = new StringBuilder();
        sb.AppendLine("Previous Conversation:");
        foreach (var msg in messages)
        {
            var role = string.Equals(msg.Role, "Assistant", StringComparison.OrdinalIgnoreCase)
                ? "Assistant"
                : "User";
            var content = (msg.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(content))
                continue;
            sb.Append(role).Append(": ").AppendLine(content);
        }

        return (sb.Length == 0 ? null : sb.ToString().Trim(), userTurns);
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

    private static string? BuildCitationUrl(string? filePath, int chunkOrder)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var pageNumber = chunkOrder + 1;
        return $"{filePath}#page={pageNumber}";
    }
}
