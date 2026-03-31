using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoneVisQA.Domain.Settings;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoneVisQA.Services.Services;

public class GeminiService : IGeminiService
{
    public const string HttpClientName = "Gemini";
    private const string MimeTypeJpeg = "image/jpeg";
    private const string FallbackNoReliableInfoAnswer =
        "Xin lỗi, dựa trên cơ sở dữ liệu y khoa cơ xương khớp của chúng tôi, tôi không tìm thấy thông tin đủ tin cậy để trả lời câu hỏi chuyên sâu này của bạn.";
    private const string NoContextAnswer =
        "Dữ liệu y khoa hiện có không chứa thông tin để trả lời câu hỏi này.";
    private const string SystemPrompt =
        // STRICT: non-medical questions must be refused with the exact sentence.
        "BẠT BUỘC PHÂN TÍCH CÂU HỎI (TỪ CHỐI TUYỆT ĐỐI):\n" +
        "Bạn PHẢI phân tích kỹ câu hỏi của người dùng. Nếu câu hỏi KHÔNG liên quan đến y khoa, sức khỏe hoặc chẩn đoán hình ảnh (ví dụ: hỏi giá xăng, thời tiết, chính trị, code lập trình...), BẮT BUỘC phải trả lời chính xác bằng câu này: 'Câu hỏi của bạn không liên quan đến lĩnh vực y khoa cơ xương khớp. Vui lòng đặt câu hỏi chuyên môn hợp lệ.'\n" +
        "Tuyệt đối không được trả lời là 'Cơ sở dữ liệu không có thông tin'.\n" +
        "Trong trường hợp từ chối theo quy tắc này: đặt suggestedDiagnosis và differentialDiagnoses thành null, BỎ QUA MỌI YÊU CẦU KHÁC và không trả citations.\n" +
        "\n" +
        "BẮT BUỘC KIỂM TRA HÌNH ẢNH (NẾU CÓ):\n" +
        "1. Nếu hình ảnh được cung cấp KHÔNG phải là hình ảnh y khoa (ví dụ: ảnh phong cảnh, động vật, con người bình thường, đồ vật...), BẠN PHẢI TỪ CHỐI bằng cách đặt `answerText` là 'Hình ảnh cung cấp không phải là dữ liệu y khoa hợp lệ.' và đặt `suggestedDiagnosis` và `differentialDiagnoses` thành null. Bỏ qua mọi yêu cầu khác.\n" +
        "\n" +
        "Bắt đầu câu trả lời ngay lập tức. KHÔNG chào hỏi. KHÔNG giới thiệu.\n" +
        "KHÔNG trả lời nằm ngoài 3 trường JSON: answerText, suggestedDiagnosis, differentialDiagnoses.\n" +
        "You must return a raw JSON object without any markdown wrapping like ```json.\n" +
        "Khi trả lời hợp lệ về chuyên ngành, trả lời bằng tiếng Việt chuyên ngành y khoa chuẩn xác.\n" +
        "\n" +
        "Chỉ khi hình (nếu có) là dữ liệu y khoa hợp lệ, câu hỏi thuộc y khoa xương khớp và Context đủ: mới được điền suggestedDiagnosis / differentialDiagnoses.\n" +
        "Luôn ưu tiên Context RAG. Nếu Context không đủ, hãy trả answerText đúng: '" + NoContextAnswer + "' và đặt suggestedDiagnosis, differentialDiagnoses là null.\n" +
        "\n" +
        "CHỈ TRẢ VỀ DUY NHẤT 1 ĐỐI TƯỢNG JSON và không chèn thêm bất kỳ nội dung nào khác.\n" +
        "Không thêm các trường phụ như citationChunkIds vào bên trong answerText.";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(
        IOptions<GeminiSettings> options,
        IHttpClientFactory httpClientFactory,
        ILogger<GeminiService> logger)
    {
        _settings = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<VisualQAResponseDto> GenerateMedicalAnswerAsync(
        string prompt,
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must not be empty.", nameof(prompt));

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("Gemini API key missing (Gemini:ApiKey). Returning empty safe response.");
            return new VisualQAResponseDto
            {
                AnswerText = FallbackNoReliableInfoAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                Citations = new List<CitationItemDto>()
            };
        }

        var apiKey = _settings.ApiKey;
        var baseUrl = (_settings.BaseUrl ?? string.Empty).TrimEnd('/');
        var modelId = _settings.ModelId ?? "gemini-1.5-flash";
        // v1 endpoint: {BaseUrl}/models/{ModelId}:generateContent?key={ApiKey}
        var endpoint = $"{baseUrl}/models/{modelId}:generateContent?key={apiKey}";

        string? base64Image = null;
        string? mimeType = null;

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            (base64Image, mimeType) = await ResolveImageToBase64Async(imageUrl, cancellationToken);
            if (base64Image == null)
            {
                return new VisualQAResponseDto
                {
                    AnswerText = "Không thể truy cập hình ảnh y khoa từ bộ lưu trữ.",
                    SuggestedDiagnosis = null,
                    DifferentialDiagnoses = null,
                    Citations = new List<CitationItemDto>()
                };
            }
        }

        var retryAttempts = 3;
        for (var attempt = 1; attempt <= retryAttempts; attempt++)
        {
            try
            {
                var payload = BuildVisionPayload(prompt, base64Image, mimeType ?? MimeTypeJpeg);

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var client = _httpClientFactory.CreateClient(HttpClientName);
                var resp = await client.SendAsync(req, cancellationToken);
                var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
                var raw = Encoding.UTF8.GetString(bytes);

                _logger.LogInformation(
                    "Gemini raw response (first 2000 chars): {Raw}",
                    raw.Length > 2000 ? raw[..2000] : raw);

                if (!resp.IsSuccessStatusCode)
                {
                    if (IsTransient(resp.StatusCode) && attempt < retryAttempts)
                    {
                        await DelayBeforeRetryAsync(attempt, cancellationToken);
                        continue;
                    }

                    _logger.LogWarning(
                        "Gemini HTTP {Status}. Returning safe empty response.",
                        resp.StatusCode);

                    return new VisualQAResponseDto
                    {
                        AnswerText = FallbackNoReliableInfoAnswer,
                        SuggestedDiagnosis = null,
                        DifferentialDiagnoses = null,
                        Citations = new List<CitationItemDto>()
                    };
                }

                try
                {
                    return ParseGeminiResponse(raw);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Gemini response JSON. Returning raw text truncated.");
                    return new VisualQAResponseDto
                    {
                        AnswerText = FallbackNoReliableInfoAnswer,
                        SuggestedDiagnosis = null,
                        DifferentialDiagnoses = null,
                        Citations = new List<CitationItemDto>()
                    };
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < retryAttempts)
            {
                // Retry transient failures (network, timeouts, etc.)
                _logger.LogWarning(ex, "Gemini call failed (attempt {Attempt}/{Max}). Retrying...", attempt, retryAttempts);
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini call failed permanently. Returning safe empty response.");
                return new VisualQAResponseDto
                {
                    AnswerText = FallbackNoReliableInfoAnswer,
                    SuggestedDiagnosis = null,
                    DifferentialDiagnoses = null,
                    Citations = new List<CitationItemDto>()
                };
            }
        }

        // Should not reach here, but keeps behavior explicit.
        return new VisualQAResponseDto
        {
            AnswerText = FallbackNoReliableInfoAnswer,
            SuggestedDiagnosis = null,
            DifferentialDiagnoses = null,
            Citations = new List<CitationItemDto>()
        };
    }

    private static Dictionary<string, object> BuildVisionPayload(string prompt, string? base64Image, string mimeType)
    {
        // Required Gemini multimodal structure for v1:
        // { "contents": [ { "parts": [ { "text": "..." }, { "inlineData": { "mimeType": "image/jpeg", "data": "..." } } ] } ] }
        // For prompt persona enforcement in v1, we inline the SystemPrompt into the text portion.
        var parts = new List<object>
        {
            new Dictionary<string, object>
            {
                ["text"] = $"{SystemPrompt}\n\n{prompt}"
            }
        };

        if (!string.IsNullOrWhiteSpace(base64Image))
        {
            parts.Add(new Dictionary<string, object>
            {
                ["inlineData"] = new Dictionary<string, object>
                {
                    ["mimeType"] = mimeType,
                    ["data"] = base64Image
                }
            });
        }

        return new Dictionary<string, object>
        {
            ["generationConfig"] = new Dictionary<string, object>
            {
                ["responseMimeType"] = "application/json"
            },
            ["contents"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["parts"] = parts
                }
            }
        };
    }

    private async Task<(string? base64, string? mimeType)> ResolveImageToBase64Async(string imageUrlOrBase64, CancellationToken ct)
    {
        // Accept raw base64 (our ImageProcessingService output) OR an HTTP(s) URL.
        if (imageUrlOrBase64.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageUrlOrBase64.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var req = new HttpRequestMessage(HttpMethod.Get, imageUrlOrBase64);
                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

                if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _logger.LogWarning(
                        "GeminiService image download failed. imageUrl={ImageUrl}, statusCode={StatusCode}",
                        imageUrlOrBase64,
                        resp.StatusCode);
                    return (null, null);
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                return (Convert.ToBase64String(bytes), MimeTypeJpeg);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to download image from URL (no Gemini call). imageUrl={ImageUrl}",
                    imageUrlOrBase64);
                return (null, null);
            }
        }

        // Allow a data URL input like data:image/jpeg;base64,AAAA...
        if (imageUrlOrBase64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(imageUrlOrBase64, @"^data:(?<mime>[^;]+);base64,(?<data>.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (match.Groups["data"].Value, match.Groups["mime"].Value);
            }
        }

        // Treat as base64 directly.
        return (imageUrlOrBase64, MimeTypeJpeg);
    }

    private VisualQAResponseDto ParseGeminiResponse(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        // Safety filter / blocked prompts: Gemini returns promptFeedback.blockReason.
        if (root.TryGetProperty("promptFeedback", out var promptFeedback) &&
            promptFeedback.TryGetProperty("blockReason", out var blockReason) &&
            !string.IsNullOrWhiteSpace(blockReason.GetString()))
        {
            _logger.LogWarning("Gemini safety filter blocked the request: {BlockReason}", blockReason.GetString());
            return new VisualQAResponseDto
            {
                AnswerText = FallbackNoReliableInfoAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                Citations = new List<CitationItemDto>()
            };
        }

        // Some Gemini variants set finishReason="SAFETY" on the candidate.
        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array &&
            candidates.GetArrayLength() > 0)
        {
            var cand0 = candidates[0];
            if (cand0.TryGetProperty("finishReason", out var finishReason) &&
                string.Equals(finishReason.GetString(), "SAFETY", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Gemini safety filter blocked via finishReason=SAFETY.");
                return new VisualQAResponseDto
                {
                    AnswerText = FallbackNoReliableInfoAnswer,
                    SuggestedDiagnosis = null,
                    DifferentialDiagnoses = null,
                    Citations = new List<CitationItemDto>()
                };
            }
        }

        var text = ExtractCandidateText(root);
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Gemini returned empty text.");
            return new VisualQAResponseDto
            {
                AnswerText = FallbackNoReliableInfoAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                Citations = new List<CitationItemDto>()
            };
        }

        using var parsed = JsonDocument.Parse(text);
        var result = parsed.RootElement;

        var answerText = result.TryGetProperty("answerText", out var a) ? a.GetString() : null;
        var suggestedDiagnosis = result.TryGetProperty("suggestedDiagnosis", out var s) && s.ValueKind != JsonValueKind.Null
            ? s.GetString()
            : null;
        var differentialDiagnoses = result.TryGetProperty("differentialDiagnoses", out var d) && d.ValueKind != JsonValueKind.Null
            ? d.GetString()
            : null;

        if (ShouldNullifyDiagnosisFields(answerText))
        {
            suggestedDiagnosis = null;
            differentialDiagnoses = null;
        }

        var citations = new List<CitationItemDto>();
        if (result.TryGetProperty("citationChunkIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in ids.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String && Guid.TryParse(el.GetString(), out var id))
                {
                    citations.Add(new CitationItemDto
                    {
                        ChunkId = id,
                        SimilarityScore = 0,
                        SourceText = null
                    });
                }
            }
        }

        return new VisualQAResponseDto
        {
            AnswerText = !string.IsNullOrWhiteSpace(answerText) ? answerText : FallbackNoReliableInfoAnswer,
            SuggestedDiagnosis = suggestedDiagnosis,
            DifferentialDiagnoses = differentialDiagnoses,
            Citations = citations
        };
    }

    /// <summary>
    /// Defense-in-depth: if the model returned a rejection / no-context message in answerText,
    /// strip any stray imaging diagnoses (mixed-intent JSON hallucination).
    /// </summary>
    private static bool ShouldNullifyDiagnosisFields(string? answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText))
            return true;

        var t = answerText.Trim();
        // Prefer prefix / known-template matching to avoid stripping real clinical answers that quote these phrases.
        if (t.StartsWith(NoContextAnswer, StringComparison.Ordinal)
            || string.Equals(t, NoContextAnswer, StringComparison.Ordinal))
            return true;
        if (t.StartsWith(FallbackNoReliableInfoAnswer, StringComparison.Ordinal)
            || string.Equals(t, FallbackNoReliableInfoAnswer, StringComparison.Ordinal))
            return true;
        if (t.Contains("không tìm thấy thông tin đủ tin cậy", StringComparison.OrdinalIgnoreCase) && t.Length < 400)
            return true;
        if (t.Contains("không chứa thông tin để trả lời câu hỏi này", StringComparison.OrdinalIgnoreCase) && t.Length < 400)
            return true;
        if (t.StartsWith("Hình ảnh bạn gửi không phải là phim X-quang", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(t, "Hình ảnh cung cấp không phải là dữ liệu y khoa hợp lệ.", StringComparison.Ordinal))
            return true;
        if (t.Contains("không liên quan đến lĩnh vực y khoa", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Contains("Tôi chỉ hỗ trợ phân tích các vấn đề về hệ vận động", StringComparison.OrdinalIgnoreCase) && t.Length < 400)
            return true;

        return false;
    }

    private static string ExtractCandidateText(JsonElement root)
    {
        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array &&
            candidates.GetArrayLength() > 0)
        {
            var cand0 = candidates[0];
            if (cand0.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array &&
                parts.GetArrayLength() > 0)
            {
                var part0 = parts[0];
                if (part0.TryGetProperty("text", out var text))
                    return text.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        // Retry on rate limit, timeouts and most 5xx.
        return statusCode == (System.Net.HttpStatusCode)429 ||
               statusCode == (System.Net.HttpStatusCode)408 ||
               (code >= 500 && code <= 599);
    }

    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        // Exponential backoff with a small deterministic jitter.
        var backoffMs = Math.Min(5000, 250 * (int)Math.Pow(2, attempt - 1));
        var jitterMs = (attempt * 137) % 250;
        return Task.Delay(backoffMs + jitterMs, cancellationToken);
    }
}

