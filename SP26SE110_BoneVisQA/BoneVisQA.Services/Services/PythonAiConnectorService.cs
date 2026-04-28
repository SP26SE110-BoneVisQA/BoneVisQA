using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoneVisQA.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

/// <summary>
/// HTTP client for the Python AI microservice. JSON uses snake_case to match FastAPI models.
/// </summary>
public sealed class PythonAiConnectorService : IPythonAiConnectorService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PythonAiConnectorService> _logger;

    private static readonly JsonSerializerOptions SerializerSnakeWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions SerializerSnakeRead = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public PythonAiConnectorService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PythonAiConnectorService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_configuration["AiMicroservice:BaseUrl"]))
            _logger.LogTrace("AiMicroservice:BaseUrl not set; HttpClient BaseAddress is configured in Program.cs.");
    }

    /// <inheritdoc />
    public async Task<bool> TriggerIngestAsync(
        string dicomUrl,
        string patientId,
        string diagnosis,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dicomUrl))
        {
            _logger.LogWarning("TriggerIngestAsync: dicomUrl is empty.");
            return false;
        }

        // Python IngestBody: dicom_path, diagnosis_text, chandoan_path. patient_id is read from the DICOM on the server.
        if (!string.IsNullOrWhiteSpace(patientId))
            _logger.LogDebug("TriggerIngestAsync: patientId={PatientId} (not sent in body; server reads from DICOM).", patientId);

        try
        {
            using var resp = await _httpClient.PostAsJsonAsync(
                "ingest",
                new IngestPayload(dicomUrl.Trim(), NullIfWhiteSpace(diagnosis), ChandoanPath: null),
                SerializerSnakeWrite,
                cancellationToken);

            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Python AI ingest failed: {Status} {Body}",
                    (int)resp.StatusCode,
                    body.Length > 500 ? body[..500] + "…" : body);
                return false;
            }

            return true;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Python AI ingest timed out.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Python AI ingest request failed.");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<RagResponseDto> AskRagAsync(
        string question,
        string modality,
        string anatomy,
        string? pathologyGroup = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            _logger.LogWarning("AskRagAsync: question is empty.");
            return RagFail(400, "Question is required.");
        }

        try
        {
            using var resp = await _httpClient.PostAsJsonAsync(
                "api/v1/qa/ask",
                new RagPayload(
                    question.Trim(),
                    modality.Trim(),
                    anatomy.Trim(),
                    NullIfWhiteSpace(pathologyGroup),
                    ImageEmbedding: null),
                SerializerSnakeWrite,
                cancellationToken);

            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Python AI RAG failed: {Status} {Body}",
                    (int)resp.StatusCode,
                    body.Length > 800 ? body[..800] + "…" : body);
                return RagFail((int)resp.StatusCode, $"HTTP {(int)resp.StatusCode}");
            }

            RagApiBody? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<RagApiBody>(body, SerializerSnakeRead);
            }
            catch (JsonException jex)
            {
                _logger.LogWarning(jex, "Python AI RAG JSON parse failed.");
                return RagFail((int)resp.StatusCode, "Invalid JSON from AI service.");
            }

            if (parsed?.Prompt is null)
            {
                _logger.LogWarning("Python AI RAG missing 'prompt' in body.");
                return RagFail((int)resp.StatusCode, "Missing prompt in response.");
            }

            var items = new List<RagContextItemDto>();
            if (parsed.Context is { Count: > 0 })
            {
                foreach (var b in parsed.Context)
                {
                    items.Add(new RagContextItemDto(
                        b.Rank,
                        b.Source,
                        b.RefId,
                        b.PathologyGroup,
                        b.Distance,
                        b.Excerpt));
                }
            }

            var count = parsed.RetrievalCount;
            if (count <= 0 && items.Count > 0)
                count = items.Count;

            return new RagResponseDto(
                Success: true,
                StatusCode: (int)resp.StatusCode,
                ErrorMessage: null,
                Prompt: parsed.Prompt,
                Context: items,
                RetrievalCount: count);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Python AI RAG timed out.");
            return RagFail(0, "Request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Python AI RAG request failed.");
            return RagFail(0, ex.Message);
        }
    }

    private static string? NullIfWhiteSpace(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private RagResponseDto RagFail(int statusCode, string message) =>
        new(false, statusCode, message, string.Empty, Array.Empty<RagContextItemDto>(), 0);

    private sealed record IngestPayload(string DicomPath, string? DiagnosisText, string? ChandoanPath);

    private sealed record RagPayload(
        string UserQuestion,
        string Modality,
        string Anatomy,
        string? PathologyGroup,
        IReadOnlyList<float>? ImageEmbedding);

    private sealed class RagApiBody
    {
        public string? Prompt { get; set; }
        public List<RagContextJson>? Context { get; set; }
        public int RetrievalCount { get; set; }
    }

    private sealed class RagContextJson
    {
        public int Rank { get; set; }
        public string? Source { get; set; }
        public string? RefId { get; set; }
        public string? PathologyGroup { get; set; }
        public double Distance { get; set; }
        public string? Excerpt { get; set; }
    }
}
