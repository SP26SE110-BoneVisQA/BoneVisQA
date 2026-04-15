using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BoneVisQA.Domain.Settings;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoneVisQA.Services.Services;

/// <summary>
/// 768-dimensional embeddings via Google Generative Language API <c>text-embedding-004</c>.
/// </summary>
public sealed class GeminiEmbeddingService : IEmbeddingService
{
    public const string HttpClientName = "GeminiEmbedding";
    private const int Dimensions = 768;
    private const string DefaultModelId = "text-embedding-004";
    /// <summary>Generative Language API embed limit is token-based; stay under a safe character budget.</summary>
    private const int MaxEmbedCharacters = 8000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public GeminiEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiSettings> options,
        ILogger<GeminiEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Cannot embed empty or whitespace text.", nameof(text));

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogError("Gemini API key missing (Gemini:ApiKey).");
            throw new EmbeddingFailedException("Gemini API key is not configured (Gemini:ApiKey).");
        }

        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _logger.LogError("Gemini BaseUrl missing (Gemini:BaseUrl).");
            throw new EmbeddingFailedException("Gemini BaseUrl is not configured.");
        }

        var modelId = string.IsNullOrWhiteSpace(_settings.EmbeddingModelId)
            ? DefaultModelId
            : _settings.EmbeddingModelId.Trim();

        var payloadText = text.Length > MaxEmbedCharacters
            ? text[..MaxEmbedCharacters]
            : text;

        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        var url =
            $"{baseUrl}/models/{modelId}:embedContent?key={Uri.EscapeDataString(_settings.ApiKey)}";

        var body = new Dictionary<string, object?>
        {
            ["model"] = $"models/{modelId}",
            ["content"] = new Dictionary<string, object?>
            {
                ["parts"] = new[] { new Dictionary<string, string> { ["text"] = payloadText } }
            }
        };

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(request, cancellationToken);
            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Gemini embed failed {Status}: {Body}",
                    response.StatusCode,
                    jsonString.Length > 2000 ? jsonString[..2000] : jsonString);
                throw new EmbeddingFailedException(
                    $"Gemini embedding HTTP {(int)response.StatusCode} {response.StatusCode}.");
            }

            var parsed = ParseEmbeddingVector(jsonString);
            if (parsed.Length != Dimensions)
            {
                throw new EmbeddingFailedException(
                    $"Expected embedding dimension {Dimensions}, received {parsed.Length}.");
            }

            return parsed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EmbeddingFailedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini embedding request failed.");
            throw new EmbeddingFailedException("Gemini embedding request failed.", ex);
        }
    }

    private static float[] ParseEmbeddingVector(string jsonString)
    {
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.TryGetProperty("embedding", out var emb))
        {
            if (emb.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
                return ParseFloatArray(values);

            if (emb.ValueKind == JsonValueKind.Array)
                return ParseFloatArray(emb);
        }

        throw new EmbeddingFailedException(
            $"Unexpected Gemini embed JSON (no embedding.values). Raw: {jsonString[..Math.Min(jsonString.Length, 500)]}");
    }

    private static float[] ParseFloatArray(JsonElement array)
    {
        var n = array.GetArrayLength();
        var result = new float[n];
        var i = 0;
        foreach (var el in array.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Number)
                throw new EmbeddingFailedException("Embedding values must be numbers.");
            result[i++] = el.GetSingle();
        }

        return result;
    }
}
