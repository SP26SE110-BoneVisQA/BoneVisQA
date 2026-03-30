using System.Text;
using System.Text.Json;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

public class EmbeddingService : IEmbeddingService
{
    private const int Dimensions = 768;
    private const string DefaultEmbeddingUrl =
        "https://router.huggingface.co/hf-inference/models/sentence-transformers/paraphrase-multilingual-mpnet-base-v2/pipeline/feature-extraction";

    public const string HttpClientName = "HuggingFace";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<EmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Cannot embed empty or whitespace text.", nameof(text));

        var apiKey = _configuration["HuggingFace:ApiKey"]
                     ?? _configuration["HF_API_KEY"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("HuggingFace API key missing (HuggingFace:ApiKey or HF_API_KEY).");
            throw new EmbeddingFailedException(
                "HuggingFace API key is not configured (HuggingFace:ApiKey or HF_API_KEY).");
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var url = _configuration["HuggingFace:EmbeddingUrl"] ?? DefaultEmbeddingUrl;
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { inputs = text }),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(request, cancellationToken);
            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "HuggingFace embedding failed {Status}: {Body}",
                    response.StatusCode,
                    jsonString.Length > 2000 ? jsonString[..2000] : jsonString);
                throw new EmbeddingFailedException(
                    $"HuggingFace embedding HTTP {(int)response.StatusCode} {response.StatusCode}.");
            }

            float[] parsed;
            try
            {
                parsed = ExtractEmbeddingVectorFromHuggingFaceJson(jsonString);
            }
            catch (JsonException ex)
            {
                throw new EmbeddingFailedException(
                    $"Invalid JSON in HuggingFace embedding response. Raw HF Response: {jsonString}",
                    ex);
            }

            if (parsed.Length != Dimensions)
            {
                throw new EmbeddingFailedException(
                    $"Expected embedding dimension {Dimensions}, received {parsed.Length}. Raw HF Response: {jsonString}");
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
            _logger.LogError(ex, "Embedding API request failed.");
            throw new EmbeddingFailedException("Embedding request failed.", ex);
        }
    }

    /// <summary>
    /// HF feature-extraction may return <c>[float,...]</c>, <c>[[float,...]]</c>, or deeper nesting; drill to the first 1D float array.
    /// </summary>
    private static float[] ExtractEmbeddingVectorFromHuggingFaceJson(string jsonString)
    {
        using var doc = JsonDocument.Parse(jsonString);
        var current = doc.RootElement;

        while (current.ValueKind == JsonValueKind.Array
               && current.GetArrayLength() > 0
               && current[0].ValueKind == JsonValueKind.Array)
        {
            current = current[0];
        }

        if (current.ValueKind != JsonValueKind.Array
            || current.GetArrayLength() == 0
            || current[0].ValueKind != JsonValueKind.Number)
        {
            throw new EmbeddingFailedException(
                $"Failed to extract embedding (expected a nested or flat JSON array of numbers). Raw HF Response: {jsonString}");
        }

        float[]? vector;
        try
        {
            vector = current.Deserialize<float[]>();
        }
        catch (JsonException ex)
        {
            throw new EmbeddingFailedException(
                $"Failed to deserialize embedding float array. Raw HF Response: {jsonString}",
                ex);
        }

        if (vector == null || vector.Length == 0)
        {
            throw new EmbeddingFailedException(
                $"HuggingFace response did not contain a valid embedding array. Raw HF Response: {jsonString}");
        }

        return vector;
    }
}
