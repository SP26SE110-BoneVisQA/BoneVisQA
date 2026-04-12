using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace BoneVisQA.Services.Services;

public class VisualQaAiService : IVisualQaAiService
{
    private const double MinimumRelevantSimilarity = 0.72d;
    private const string InvalidMedicalImageAnswer = "Hình ảnh cung cấp không phải là dữ liệu y khoa hợp lệ.";
    private const string MissingMedicalKnowledgeAnswer =
        "Xin lỗi, dựa trên cơ sở dữ liệu y khoa cơ xương khớp của chúng tôi, tôi không tìm thấy thông tin đủ tin cậy để trả lời câu hỏi chuyên sâu này của bạn.";
    private const string TemporaryVectorSearchUnavailableAnswer =
        "Vector search is temporarily unavailable due to high network demand. Please try again later.";
    private const string TemporaryAiGenerationUnavailableAnswer =
        "AI generation service is temporarily unavailable due to high network demand. Please try again later.";
    private const string AiOverloadVietnameseMessage =
        "Hệ thống AI đang quá tải. Vui lòng thử lại sau.";

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
            return new VisualQAResponseDto
            {
                AnswerText = AiOverloadVietnameseMessage,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = AiOverloadVietnameseMessage,
                Citations = new List<CitationItemDto>()
            };
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

        // Filter RAG context to the same document category as the provided CaseId (if available).
        // Place WHERE before ORDERBY(CosineDistance) to improve performance and accuracy.
        var topChunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Include(c => c.Doc)
            .Where(c => c.Embedding != null && (caseCategoryId == null || c.Doc.CategoryId == caseCategoryId))
            .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
            .Take(5)
            .ToListAsync(cancellationToken);

        // Guardrail: if retrieved context is not semantically close enough, do not call Gemini.
        // Similarity is computed purely in C# to avoid EF/Pgvector client-side evaluation.
        var citationsFromChunks = topChunks
            .Where(c => c.Embedding != null)
            .Select(c => new CitationItemDto
            {
                ChunkId = c.Id,
                ReferenceUrl = BuildCitationUrl(c.Doc.FilePath, c.ChunkOrder),
                PageNumber = c.ChunkOrder + 1,
                SourceText = c.Content
            })
            .ToList();

        var maxSimilarity = topChunks.Count > 0
            ? topChunks
                .Where(c => c.Embedding != null)
                .Select(c => CalculateCosineSimilarity(queryEmbedding, c.Embedding!.ToArray()))
                .DefaultIfEmpty(0d)
                .Max()
            : 0d;

        if (maxSimilarity < MinimumRelevantSimilarity)
        {
            return new VisualQAResponseDto
            {
                AnswerText = MissingMedicalKnowledgeAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = maxSimilarity,
                Citations = new List<CitationItemDto>()
            };
        }

        var prompt = BuildGeminiPrompt(request, topChunks);

        // GeminiService expects "imageUrl" input, but we pass Base64 for our inlineData workflow.
        VisualQAResponseDto response;
        try
        {
            response = await _geminiService.GenerateMedicalAnswerAsync(
                prompt,
                imageB64 ?? string.Empty,
                cancellationToken);
        }
        catch
        {
            // Do not attach RAG similarity here: generation failed and the answer must stay on the triage queue.
            return new VisualQAResponseDto
            {
                AnswerText = AiOverloadVietnameseMessage,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = AiOverloadVietnameseMessage,
                Citations = new List<CitationItemDto>()
            };
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
                response.Citations = citationsFromChunks;
            }
            else
            {
                var metaByChunkId = citationsFromChunks.ToDictionary(c => c.ChunkId, c => c);
                foreach (var citation in response.Citations)
                {
                    if (metaByChunkId.TryGetValue(citation.ChunkId, out var meta))
                    {
                        citation.SourceText = meta.SourceText;
                        citation.ReferenceUrl = meta.ReferenceUrl;
                        citation.PageNumber = meta.PageNumber;
                    }
                }
            }
        }

        response.AiConfidenceScore = maxSimilarity;

        return response;
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

    private static bool IsNonMedicalRefusalAnswer(string? answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText))
            return false;

        return answerText.Contains("không phải dữ liệu y khoa hợp lệ", StringComparison.OrdinalIgnoreCase)
               || answerText.Contains("không liên quan đến lĩnh vực y khoa", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildGeminiPrompt(
        VisualQARequestDto request,
        IReadOnlyList<Repositories.Models.DocumentChunk> chunks)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an Expert Radiologist assisting medical students.");
        sb.AppendLine();

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

        if (chunks.Count > 0)
        {
            sb.AppendLine("Dữ liệu tham khảo (Context) dưới đây có thể không liên quan. Nếu thấy không liên quan đến câu hỏi, hãy bỏ qua nó.");
            sb.AppendLine();
            sb.AppendLine("## Retrieved reference context (use only to support your answer):");
            sb.AppendLine();

            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                // Include both [n] ordering and stable chunkId so citationChunkIds can reference retrieved ids.
                sb.Append('[').Append(i + 1).Append("] ");
                sb.Append("(chunkId: ").Append(chunk.Id).Append(") ");
                sb.AppendLine(chunk.Content ?? string.Empty);
                sb.AppendLine();
            }
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
