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
        // STRICT: non-medical image/question requests must be refused with no citations.
        "BẮT BUỘC PHÂN TÍCH CÂU HỎI VÀ HÌNH ẢNH (TỪ CHỐI TUYỆT ĐỐI):\n" +
        "Nếu câu hỏi KHÔNG liên quan đến y khoa cơ xương khớp, sức khỏe hoặc chẩn đoán hình ảnh cơ xương khớp (ví dụ: hỏi giá xăng, thời tiết, chính trị, code lập trình...), BẮT BUỘC phải trả lời chính xác bằng câu này: 'Câu hỏi của bạn không liên quan đến lĩnh vực y khoa cơ xương khớp. Vui lòng đặt câu hỏi chuyên môn hợp lệ.'\n" +
        "Tuyệt đối không được trả lời là 'Cơ sở dữ liệu không có thông tin'.\n" +
        "Trong trường hợp từ chối theo quy tắc này: đặt suggestedDiagnosis, differentialDiagnoses, keyImagingFindings, reflectiveQuestions thành null, BỎ QUA MỌI YÊU CẦU KHÁC và KHÔNG được trả citations.\n" +
        "\n" +
        "BẮT BUỘC KIỂM TRA HÌNH ẢNH (NẾU CÓ):\n" +
        "1. Nếu hình ảnh được cung cấp KHÔNG phải là hình ảnh y khoa liên quan đến cơ xương khớp (ví dụ: ảnh phong cảnh, động vật, con người bình thường, đồ vật, ảnh không thuộc lĩnh vực cơ xương khớp...), BẠN PHẢI TỪ CHỐI bằng cách đặt `answerText` là 'Hình ảnh cung cấp không phải là dữ liệu y khoa hợp lệ.' và đặt suggestedDiagnosis, differentialDiagnoses, keyImagingFindings, reflectiveQuestions thành null. KHÔNG được trả citations. Bỏ qua mọi yêu cầu khác.\n" +
        "\n" +
        "Bắt đầu câu trả lời ngay lập tức. KHÔNG chào hỏi. KHÔNG giới thiệu.\n" +
        "KHÔNG trả lời nằm ngoài các trường JSON: answerText, suggestedDiagnosis, differentialDiagnoses, keyImagingFindings, reflectiveQuestions.\n" +
        "You must return a raw JSON object without any markdown wrapping like ```json.\n" +
        "Khi trả lời hợp lệ về chuyên ngành, trả lời bằng tiếng Việt chuyên ngành y khoa chuẩn xác.\n" +
        "\n" +
        "Chỉ khi hình (nếu có) là dữ liệu y khoa hợp lệ, câu hỏi thuộc y khoa xương khớp và Context đủ: mới được điền suggestedDiagnosis, differentialDiagnoses, keyImagingFindings, reflectiveQuestions.\n" +
        "Luôn ưu tiên Context RAG. Nếu Context không đủ, hãy trả answerText đúng: '" + NoContextAnswer + "' và đặt suggestedDiagnosis, differentialDiagnoses, keyImagingFindings, reflectiveQuestions là null.\n" +
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
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = FallbackNoReliableInfoAnswer,
                Citations = new List<CitationItemDto>()
            };
        }

        var modelIds = _settings.GetResolvedModelIds();
        if (modelIds.Count == 0)
        {
            _logger.LogWarning(
                "Gemini model ids missing (configure Gemini:Models or Gemini:ModelId). Returning empty safe response.");
            return new VisualQAResponseDto
            {
                AnswerText = FallbackNoReliableInfoAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = FallbackNoReliableInfoAnswer,
                Citations = new List<CitationItemDto>()
            };
        }

        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _logger.LogWarning("Gemini base URL missing (Gemini:BaseUrl). Returning empty safe response.");
            return new VisualQAResponseDto
            {
                AnswerText = FallbackNoReliableInfoAnswer,
                SuggestedDiagnosis = null,
                DifferentialDiagnoses = null,
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = FallbackNoReliableInfoAnswer,
                Citations = new List<CitationItemDto>()
            };
        }

        var apiKey = _settings.ApiKey;
        var baseUrl = _settings.BaseUrl.TrimEnd('/');

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
                    KeyImagingFindings = null,
                    ReflectiveQuestions = null,
                    AiConfidenceScore = null,
                    ErrorMessage = "Không thể truy cập hình ảnh y khoa từ bộ lưu trữ.",
                    Citations = new List<CitationItemDto>()
                };
            }
        }

        var failureSummaries = new List<string>();
        var totalModels = modelIds.Count;
        var attemptIndex = 0;

        foreach (var modelId in modelIds)
        {
            attemptIndex++;
            _logger.LogInformation(
                "[GeminiService] generateContent attempt {AttemptIndex}/{TotalModels} using model id \"{ModelId}\"",
                attemptIndex,
                totalModels,
                modelId);
            var endpoint = $"{baseUrl}/models/{modelId}:generateContent?key={apiKey}";

            try
            {
                var payload = BuildVisionPayload(prompt, base64Image, mimeType ?? MimeTypeJpeg);

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var client = _httpClientFactory.CreateClient(HttpClientName);
                _logger.LogDebug("[GeminiService] POST {BaseUrl}/models/{ModelId}:generateContent", baseUrl, modelId);
                var resp = await client.SendAsync(req, cancellationToken);
                var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
                var raw = Encoding.UTF8.GetString(bytes);

                _logger.LogInformation(
                    "[GeminiService] Model id \"{ModelId}\": HTTP {StatusCode}; raw response (first 2000 chars): {Raw}",
                    modelId,
                    (int)resp.StatusCode,
                    raw.Length > 2000 ? raw[..2000] : raw);

                if (!resp.IsSuccessStatusCode)
                {
                    var bodySnippet = raw.Length > 500 ? raw[..500] : raw;
                    failureSummaries.Add($"{modelId}: HTTP {(int)resp.StatusCode} — {bodySnippet}");

                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning(
                            "[GeminiService] Model id \"{ModelId}\" returned HTTP 404 (not available for this key/project). No retry on this id — advancing to next configured model.",
                            modelId);
                        continue;
                    }

                    if (IsTransient(resp.StatusCode))
                    {
                        _logger.LogWarning(
                            "[GeminiService] Model id \"{ModelId}\" reached quota or transient limit (HTTP {StatusCode}). Switching to next available model...",
                            modelId,
                            (int)resp.StatusCode);
                        await Task.Delay(1000, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[GeminiService] Model id \"{ModelId}\": HTTP {StatusCode}. Trying next configured model.",
                            modelId,
                            (int)resp.StatusCode);
                    }

                    continue;
                }

                _logger.LogInformation(
                    "[GeminiService] Model id \"{ModelId}\" returned success (HTTP {StatusCode}); parsing response body.",
                    modelId,
                    (int)resp.StatusCode);

                try
                {
                    var parsed = ParseGeminiResponse(raw);
                    _logger.LogInformation(
                        "[GeminiService] Completed successfully using model id \"{ModelId}\" (attempt {AttemptIndex}/{TotalModels}).",
                        modelId,
                        attemptIndex,
                        totalModels);
                    return parsed;
                }
                catch (Exception ex)
                {
                    failureSummaries.Add($"{modelId}: response parse error — {ex.Message}");
                    _logger.LogWarning(
                        ex,
                        "[GeminiService] Failed to parse Gemini JSON for model id \"{ModelId}\". Trying next configured model.",
                        modelId);
                    continue;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failureSummaries.Add($"{modelId}: {ex.GetType().Name} — {ex.Message}");
                _logger.LogWarning(
                    ex,
                    "[GeminiService] Model id \"{ModelId}\" request failed. Switching to next available model...",
                    modelId);
                await Task.Delay(1000, cancellationToken);
                continue;
            }
        }

        throw new InvalidOperationException(
            $"All configured Gemini models failed ({modelIds.Count} model(s)). Summary: {string.Join(" | ", failureSummaries)}");
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
            // Medical X-rays often false-trigger default safety filters; relax for legitimate clinical education use.
            ["safetySettings"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["category"] = "HARM_CATEGORY_HARASSMENT",
                    ["threshold"] = "BLOCK_NONE"
                },
                new Dictionary<string, object>
                {
                    ["category"] = "HARM_CATEGORY_HATE_SPEECH",
                    ["threshold"] = "BLOCK_NONE"
                },
                new Dictionary<string, object>
                {
                    ["category"] = "HARM_CATEGORY_SEXUALLY_EXPLICIT",
                    ["threshold"] = "BLOCK_NONE"
                },
                new Dictionary<string, object>
                {
                    ["category"] = "HARM_CATEGORY_DANGEROUS_CONTENT",
                    ["threshold"] = "BLOCK_NONE"
                }
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
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = FallbackNoReliableInfoAnswer,
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
                    KeyImagingFindings = null,
                    ReflectiveQuestions = null,
                    AiConfidenceScore = null,
                    ErrorMessage = FallbackNoReliableInfoAnswer,
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
                KeyImagingFindings = null,
                ReflectiveQuestions = null,
                AiConfidenceScore = null,
                ErrorMessage = FallbackNoReliableInfoAnswer,
                Citations = new List<CitationItemDto>()
            };
        }

        using var parsed = JsonDocument.Parse(text);
        var result = parsed.RootElement;

        var answerText = result.TryGetProperty("answerText", out var a) ? a.GetString() : null;
        var suggestedDiagnosis = result.TryGetProperty("suggestedDiagnosis", out var s)
            ? ReadStringOrJoinedArray(s)
            : null;
        var differentialDiagnoses = result.TryGetProperty("differentialDiagnoses", out var d)
            ? ReadStringOrJoinedArray(d)
            : null;
        var keyImagingFindings = result.TryGetProperty("keyImagingFindings", out var kfi)
            ? ReadStringOrJoinedArray(kfi)
            : null;
        var reflectiveQuestions = result.TryGetProperty("reflectiveQuestions", out var rq)
            ? ReadStringOrJoinedArray(rq)
            : null;

        if (ShouldNullifyDiagnosisFields(answerText))
        {
            suggestedDiagnosis = null;
            differentialDiagnoses = null;
            keyImagingFindings = null;
            reflectiveQuestions = null;
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
            KeyImagingFindings = keyImagingFindings,
            ReflectiveQuestions = reflectiveQuestions,
            AiConfidenceScore = null,
            ErrorMessage = null,
            Citations = citations
        };
    }

    /// <summary>
    /// Gemini sometimes returns list-valued fields as JSON arrays instead of a single string.
    /// Accept <see cref="JsonValueKind.String"/>, <see cref="JsonValueKind.Null"/>, or <see cref="JsonValueKind.Array"/> (joined with newline + bullet).
    /// </summary>
    private static string? ReadStringOrJoinedArray(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.Array:
            {
                var segments = new List<string>();
                foreach (var item in el.EnumerateArray())
                {
                    var s = JsonElementToPlainSegment(item);
                    if (!string.IsNullOrWhiteSpace(s))
                        segments.Add(s.Trim());
                }

                return segments.Count == 0 ? null : string.Join("\n- ", segments);
            }
            default:
                return el.GetRawText();
        }
    }

    private static string JsonElementToPlainSegment(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? string.Empty,
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => el.GetRawText()
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
}

