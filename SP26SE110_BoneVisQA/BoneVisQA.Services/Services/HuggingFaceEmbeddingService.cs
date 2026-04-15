using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BoneVisQA.Domain.Settings;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoneVisQA.Services.Services;

public sealed class HuggingFaceEmbeddingService : IEmbeddingService
{
    public const string HttpClientName = "HuggingFaceEmbedding";
    private const int Dimensions = 768;
    private const int DefaultBatchSize = 50;
    private const int MaxEmbedCharacters = 8000;
    private const int InterRequestDelayMs = 1200;
    private const int ModelLoadingRetryDelayMs = 5000;
    private const int ModelLoadingMaxRetries = 3;
    private static readonly SemaphoreSlim EmbedRateGate = new(1, 1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HuggingFaceSettings _settings;
    private readonly ILogger<HuggingFaceEmbeddingService> _logger;
    private readonly int _maxBatchSize;

    public HuggingFaceEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IOptions<HuggingFaceSettings> options,
        ILogger<HuggingFaceEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
        _maxBatchSize = _settings.BatchSize > 0
            ? _settings.BatchSize
            : DefaultBatchSize;
    }

    public async Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var vectors = await BatchEmbedContentsAsync(new[] { text }, cancellationToken);
        return vectors[0];
    }

    public async Task<IReadOnlyList<float[]>> BatchEmbedContentsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var source = texts?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>();
        if (source.Count == 0)
            return Array.Empty<float[]>();
        if (source.Count > _maxBatchSize)
            throw new InvalidOperationException(
                $"HuggingFace batch size exceeded provider limit. Received {source.Count}, configured maximum is {_maxBatchSize} (HuggingFace:BatchSize).");

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new EmbeddingFailedException("HuggingFace API key is not configured (HuggingFace:ApiKey or HF_API_KEY).");
        if (string.IsNullOrWhiteSpace(_settings.EmbeddingUrl))
            throw new EmbeddingFailedException("HuggingFace embedding URL is not configured (HuggingFace:EmbeddingUrl).");

        var normalizedInputs = source
            .Select(t => t.Length > MaxEmbedCharacters ? t[..MaxEmbedCharacters] : t)
            .ToList();
        var endpoint = _settings.EmbeddingUrl.Trim();

        await EmbedRateGate.WaitAsync(cancellationToken);
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var body = JsonSerializer.Serialize(new
            {
                inputs = normalizedInputs,
                options = new { wait_for_model = true }
            });

            for (var attempt = 1; attempt <= ModelLoadingMaxRetries; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                string raw;
                try
                {
                    response = await client.SendAsync(request, cancellationToken);
                    raw = await response.Content.ReadAsStringAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    throw new EmbeddingFailedException("HuggingFace request failed.", ex);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && attempt < ModelLoadingMaxRetries)
                {
                    _logger.LogWarning(
                        "[HuggingFace] Model is loading (HTTP 503). Retry {Attempt}/{MaxRetries} after {DelayMs}ms.",
                        attempt,
                        ModelLoadingMaxRetries,
                        ModelLoadingRetryDelayMs);
                    await Task.Delay(ModelLoadingRetryDelayMs, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new EmbeddingFailedException(
                        $"HuggingFace embedding HTTP {(int)response.StatusCode} {response.StatusCode}.");
                }

                try
                {
                    var vectors = ParseVectors(raw, normalizedInputs.Count);
                    return vectors;
                }
                catch (Exception ex)
                {
                    throw new EmbeddingFailedException("HuggingFace returned invalid embedding payload.", ex);
                }
            }

            throw new EmbeddingFailedException("HuggingFace model loading retries exhausted.");
        }
        finally
        {
            try
            {
                await Task.Delay(InterRequestDelayMs, cancellationToken);
            }
            finally
            {
                EmbedRateGate.Release();
            }
        }
    }

    private static IReadOnlyList<float[]> ParseVectors(string raw, int expectedCount)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            throw new EmbeddingFailedException("HuggingFace embedding response is not a valid numeric array.");

        var vectors = new List<float[]>();
        var first = root[0];
        if (first.ValueKind == JsonValueKind.Number)
        {
            var single = ParseVector(root);
            vectors.Add(single);
        }
        else
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Array)
                    throw new EmbeddingFailedException("HuggingFace embedding response contains non-array vector items.");
                vectors.Add(ParseVector(item));
            }
        }

        if (vectors.Count != expectedCount)
            throw new EmbeddingFailedException($"HuggingFace embedding count mismatch. Expected {expectedCount}, got {vectors.Count}.");

        return vectors;
    }

    private static float[] ParseVector(JsonElement array)
    {
        var n = array.GetArrayLength();
        if (n != Dimensions)
            throw new EmbeddingFailedException($"Expected embedding dimension {Dimensions}, received {n}.");

        var vector = new float[n];
        var i = 0;
        foreach (var el in array.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Number)
                throw new EmbeddingFailedException("HuggingFace embedding values must be numeric.");
            vector[i++] = el.GetSingle();
        }

        return vector;
    }

}
