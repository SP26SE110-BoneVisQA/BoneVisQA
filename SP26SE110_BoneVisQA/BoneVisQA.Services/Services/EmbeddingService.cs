using System.Text;
using System.Text.Json;
using BoneVisQA.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

public class EmbeddingService : IEmbeddingService
{
    private const int Dimensions = 768;
    private const string HfModelPath = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2";
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
            return CreateMockEmbedding(string.Empty);

        var apiKey = _configuration["HuggingFace:ApiKey"]
                     ?? _configuration["HF_API_KEY"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("HuggingFace embedding API key missing (HuggingFace:ApiKey or HF_API_KEY). Using deterministic mock embeddings (768-d).");
            return CreateMockEmbedding(text);
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            // Canonical router URL format: model path + pipeline.
            var url = "https://router.huggingface.co/hf-inference/models/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/pipeline/feature-extraction";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { inputs = text }),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HuggingFace embedding failed {Status}: {Body}. Mock fallback.", response.StatusCode, body);
                return CreateMockEmbedding(text);
            }

            var parsed = ParseHuggingFaceEmbedding(body);
            if (parsed is { Length: Dimensions })
                return parsed;

            return CreateMockEmbedding(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding API error; mock fallback.");
            return CreateMockEmbedding(text);
        }
    }

    private static float[]? ParseHuggingFaceEmbedding(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement vec = root;
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            var first = root[0];
            vec = first.ValueKind == JsonValueKind.Array ? first : root;
        }

        if (vec.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<float>();
        foreach (var item in vec.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number)
                list.Add(item.GetSingle());
        }

        if (list.Count == Dimensions)
            return list.ToArray();
        return list.Count > 0 ? PadOrTruncate(list) : null;
    }

    private static float[] PadOrTruncate(List<float> list)
    {
        var arr = new float[Dimensions];
        for (var i = 0; i < Dimensions; i++)
            arr[i] = i < list.Count ? list[i] : 0f;
        return arr;
    }

    private static float[] CreateMockEmbedding(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        var rng = new Random(BitConverter.ToInt32(hash, 0));
        var v = new float[Dimensions];
        for (var i = 0; i < Dimensions; i++)
            v[i] = (float)(rng.NextDouble() * 2 - 1);
        var norm = Math.Sqrt(v.Sum(x => x * x));
        if (norm > 1e-6)
            for (var i = 0; i < Dimensions; i++)
                v[i] /= (float)norm;
        return v;
    }
}
