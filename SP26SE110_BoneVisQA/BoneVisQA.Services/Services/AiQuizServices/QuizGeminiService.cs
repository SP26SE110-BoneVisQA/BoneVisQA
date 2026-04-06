using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BoneVisQA.Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoneVisQA.Services.Services.AiQuizServices;

public interface IQuizGeminiService
{
    /// <summary>
    /// Gọi Gemini chỉ với prompt (không bọc system prompt Visual QA).
    /// Trả về text thuần từ model (thường là JSON quiz); null nếu thiếu key / lỗi / safety / rỗng.
    /// </summary>
    Task<string?> GenerateQuizAsync(string prompt, string? imageUrl, CancellationToken ct = default);
}

public class QuizGeminiService : IQuizGeminiService
{
    public const string HttpClientName = "QuizGemini";
    private const string MimeTypeJpeg = "image/jpeg";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiSettings _settings;
    private readonly ILogger<QuizGeminiService> _logger;

    public QuizGeminiService(
        IOptions<GeminiSettings> options,
        IHttpClientFactory httpClientFactory,
        ILogger<QuizGeminiService> logger)
    {
        _settings = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> GenerateQuizAsync(
        string prompt,
        string? imageUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must not be empty.", nameof(prompt));

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("QuizGeminiService: Gemini:ApiKey not configured. Skipping.");
            return null;
        }

        var baseUrl = (_settings.BaseUrl ?? string.Empty).TrimEnd('/');
        var modelId = _settings.ModelId ?? "gemini-1.5-flash";
        var endpoint = $"{baseUrl}/models/{modelId}:generateContent?key={_settings.ApiKey}";

        string? base64Image = null;
        string? mimeType = null;
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            (base64Image, mimeType) = await ResolveImageToBase64Async(imageUrl, ct);
            if (base64Image == null)
                _logger.LogWarning("QuizGemini: could not load image, continuing text-only. Url={Url}", imageUrl);
        }

        var retryAttempts = 3;
        for (var attempt = 1; attempt <= retryAttempts; attempt++)
        {
            try
            {
                var payload = BuildPayload(prompt, base64Image, mimeType ?? MimeTypeJpeg);

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient(HttpClientName);
                var resp = await client.SendAsync(req, ct);
                var raw = Encoding.UTF8.GetString(await resp.Content.ReadAsByteArrayAsync(ct));

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("QuizGemini HTTP {Status}. Body: {Body}",
                        resp.StatusCode, raw.Length > 500 ? raw[..500] : raw);
                    if (IsTransient(resp.StatusCode) && attempt < retryAttempts)
                    {
                        await DelayAsync(attempt, ct);
                        continue;
                    }
                    return null;
                }

                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                // Safety check
                if (root.TryGetProperty("promptFeedback", out var pf) &&
                    pf.TryGetProperty("blockReason", out var br) &&
                    !string.IsNullOrWhiteSpace(br.GetString()))
                {
                    _logger.LogWarning("QuizGemini blocked by promptFeedback: {Reason}", br.GetString());
                    return null;
                }

                if (root.TryGetProperty("candidates", out var cands) &&
                    cands.ValueKind == JsonValueKind.Array &&
                    cands.GetArrayLength() > 0 &&
                    cands[0].TryGetProperty("finishReason", out var fr) &&
                    string.Equals(fr.GetString(), "SAFETY", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("QuizGemini blocked by finishReason=SAFETY");
                    return null;
                }

                var text = ExtractText(root);
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < retryAttempts)
            {
                _logger.LogWarning(ex, "QuizGemini attempt {Attempt}/{Max} failed. Retrying...", attempt, retryAttempts);
                await DelayAsync(attempt, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QuizGemini permanently failed.");
                return null;
            }
        }

        return null;
    }

    private static Dictionary<string, object> BuildPayload(string prompt, string? base64Image, string mimeType)
    {
        var parts = new List<object>
        {
            new Dictionary<string, object> { ["text"] = prompt }
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
                new Dictionary<string, object> { ["parts"] = parts }
            }
        };
    }

    private async Task<(string? base64, string? mime)> ResolveImageToBase64Async(string url, CancellationToken ct)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(url, @"^data:(?<mime>[^;]+);base64,(?<data>.+)$", RegexOptions.IgnoreCase);
                if (m.Success) return (m.Groups["data"].Value, m.Groups["mime"].Value);
            }
            return (url, MimeTypeJpeg);
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                return (null, null);
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            return (Convert.ToBase64String(bytes), MimeTypeJpeg);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QuizGemini image download failed: {Url}", url);
            return (null, null);
        }
    }

    private static string ExtractText(JsonElement root)
    {
        if (root.TryGetProperty("candidates", out var cands) &&
            cands.ValueKind == JsonValueKind.Array &&
            cands.GetArrayLength() > 0 &&
            cands[0].TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array &&
            parts.GetArrayLength() > 0 &&
            parts[0].TryGetProperty("text", out var text))
            return text.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static bool IsTransient(System.Net.HttpStatusCode code)
    {
        var c = (int)code;
        return code == System.Net.HttpStatusCode.TooManyRequests ||
               code == System.Net.HttpStatusCode.RequestTimeout ||
               (c >= 500 && c <= 599);
    }

    private static Task DelayAsync(int attempt, CancellationToken ct)
        => Task.Delay(Math.Min(5000, 250 * (int)Math.Pow(2, attempt - 1)) + (attempt * 137) % 250, ct);
}
