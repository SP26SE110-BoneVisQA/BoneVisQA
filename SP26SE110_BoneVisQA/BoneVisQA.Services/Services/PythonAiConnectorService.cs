using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoneVisQA.Services.Helpers;
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

    public async Task<IngestResultDto> TriggerIngestAsync(

        string mediaPath,

        string diagnosis,

        string ingestPurpose = "library",

        Guid? ownerUserId = null,

        CancellationToken cancellationToken = default)

    {

        if (string.IsNullOrWhiteSpace(mediaPath))

        {

            _logger.LogWarning("TriggerIngestAsync: mediaPath is empty.");

            return IngestFail(400, "mediaPath is required.");

        }



        var purpose = string.IsNullOrWhiteSpace(ingestPurpose) ? "library" : ingestPurpose.Trim().ToLowerInvariant();



        try

        {

            using var resp = await _httpClient.PostAsJsonAsync(

                "ingest",

                new IngestPayload(

                    mediaPath.Trim(),

                    NullIfWhiteSpace(diagnosis),

                    purpose,

                    ownerUserId),

                SerializerSnakeWrite,

                cancellationToken);



            var body = await resp.Content.ReadAsStringAsync(cancellationToken);



            if (!resp.IsSuccessStatusCode)

            {

                _logger.LogWarning(

                    "Python AI ingest failed: {Status} {Body}",

                    (int)resp.StatusCode,

                    body.Length > 500 ? body[..500] + "…" : body);

                return IngestFail((int)resp.StatusCode, body);

            }



            IngestApiBody? parsed = null;

            try

            {

                parsed = JsonSerializer.Deserialize<IngestApiBody>(body, SerializerSnakeRead);

            }

            catch (JsonException jex)

            {

                _logger.LogWarning(jex, "Python AI ingest JSON parse failed.");

            }



            JsonElement? dicomMetadata = null;
            if (parsed?.DicomMetadata is { } dm)
                dicomMetadata = dm.Clone();

            return new IngestResultDto(
                Success: true,
                StatusCode: (int)resp.StatusCode,
                ErrorMessage: null,
                CaseId: Guid.TryParse(parsed?.CaseId, out var caseId) ? caseId : null,
                MediaId: Guid.TryParse(parsed?.MediaId, out var mediaId) ? mediaId : null,
                CatalogImageId: Guid.TryParse(parsed?.CatalogImageId, out var imageId) ? imageId : null,
                PreviewImageUrl: parsed?.PreviewImageUrl,
                DicomMetadata: dicomMetadata,
                RawJson: body);

        }

        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)

        {

            _logger.LogWarning(ex, "Python AI ingest timed out.");

            return IngestFail(0, "Request timed out.");

        }

        catch (Exception ex)

        {

            _logger.LogError(ex, "Python AI ingest request failed.");

            return IngestFail(0, ex.Message);

        }

    }



    /// <inheritdoc />

    public async Task<RagResponseDto> AskRagAsync(
        string question,
        string modality,
        string anatomy,
        string? pathologyGroup = null,
        Guid? caseId = null,
        Guid? caseMediaId = null,
        IReadOnlyList<float>? imageEmbedding = null,
        string? dicomClinicalContext = null,
        JsonElement? dicomMetadata = null,
        CancellationToken cancellationToken = default)

    {

        if (string.IsNullOrWhiteSpace(question))

        {

            _logger.LogWarning("AskRagAsync: question is empty.");

            return RagFail(400, "Question is required.");

        }

        if (dicomMetadata is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            _logger.LogWarning(
                "AskRagAsync: dicomMetadata is null for caseId={CaseId}, caseMediaId={CaseMediaId}. RAG routing may be less accurate.",
                caseId,
                caseMediaId);
        }
        else
        {
            var metaModality = CaseMediaDicomMetadataHelper.TryExtractModality(dicomMetadata);
            var metaAnatomy = CaseMediaDicomMetadataHelper.TryExtractAnatomy(dicomMetadata);
            var metaFindings = CaseMediaDicomMetadataHelper.TryExtractFindings(dicomMetadata);

            if (string.IsNullOrWhiteSpace(modality) && !string.IsNullOrWhiteSpace(metaModality))
                modality = metaModality;
            if (string.IsNullOrWhiteSpace(anatomy) && !string.IsNullOrWhiteSpace(metaAnatomy))
                anatomy = metaAnatomy;
            if (string.IsNullOrWhiteSpace(pathologyGroup) && !string.IsNullOrWhiteSpace(metaFindings))
                pathologyGroup = metaFindings;

            if (string.IsNullOrWhiteSpace(dicomClinicalContext))
                dicomClinicalContext = DicomClinicalContextHelper.BuildPromptBlock(dicomMetadata);

            if (string.IsNullOrWhiteSpace(metaModality) || string.IsNullOrWhiteSpace(metaAnatomy))
            {
                _logger.LogWarning(
                    "AskRagAsync: dicomMetadata missing Modality and/or Anatomy (modality={Modality}, anatomy={Anatomy}, caseId={CaseId}).",
                    metaModality ?? "(null)",
                    metaAnatomy ?? "(null)",
                    caseId);
            }
        }

        modality = string.IsNullOrWhiteSpace(modality) ? "X-Ray" : modality.Trim();
        anatomy = string.IsNullOrWhiteSpace(anatomy) ? "Other" : anatomy.Trim();



        try

        {

            using var resp = await _httpClient.PostAsJsonAsync(

                "api/v1/qa/ask",

                new RagPayload(
                    question.Trim(),
                    modality,
                    anatomy,
                    NullIfWhiteSpace(pathologyGroup),
                    imageEmbedding,
                    caseId,
                    caseMediaId,
                    NullIfWhiteSpace(dicomClinicalContext),
                    dicomMetadata is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
                        ? dicomMetadata
                        : null),

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



    /// <inheritdoc />
    public async Task<DocumentChunkEnrichmentResultDto> EnrichDocumentChunksAsync(
        Guid documentId,
        DocumentEnrichPhase phase = DocumentEnrichPhase.All,
        bool onlyMissingEmbedding = false,
        CancellationToken cancellationToken = default,
        Func<int, int, CancellationToken, Task>? onBatchProgressAsync = null)
    {
        if (documentId == Guid.Empty)
            return EnrichFail(documentId, 400, "documentId is required.");

        var enrichPhase = phase switch
        {
            DocumentEnrichPhase.Metadata => "metadata",
            DocumentEnrichPhase.Embeddings => "embeddings",
            _ => "all"
        };

        var batchSize = phase == DocumentEnrichPhase.Metadata
            ? Math.Clamp(_configuration.GetValue("AiMicroservice:EnrichMetadataBatchSize", 64), 1, 64)
            : Math.Clamp(_configuration.GetValue("AiMicroservice:EnrichBatchSize", 40), 1, 64);

        if (phase == DocumentEnrichPhase.Embeddings)
            onlyMissingEmbedding = true;

        var afterChunkOrder = -1;
        string? sectionAnatomy = null;
        string? sectionPathology = null;
        var totalProcessed = 0;
        string? embeddingModel = null;
        var anatomyDistribution = new Dictionary<string, int>(StringComparer.Ordinal);
        var pathologyDistribution = new Dictionary<string, int>(StringComparer.Ordinal);
        var nullRemaining = 0;

        try
        {
            while (true)
            {
                var batch = await PostEnrichBatchAsync(
                    documentId,
                    enrichPhase,
                    onlyMissingEmbedding,
                    batchSize,
                    afterChunkOrder,
                    sectionAnatomy,
                    sectionPathology,
                    cancellationToken);

                if (!batch.Success)
                {
                    if (totalProcessed > 0)
                    {
                        return new DocumentChunkEnrichmentResultDto(
                            false,
                            batch.StatusCode,
                            batch.ErrorMessage,
                            documentId,
                            totalProcessed,
                            embeddingModel,
                            anatomyDistribution,
                            pathologyDistribution,
                            nullRemaining);
                    }

                    return batch;
                }

                totalProcessed += batch.ChunksProcessed;
                embeddingModel = batch.EmbeddingModel ?? embeddingModel;
                nullRemaining = batch.NullEmbeddingRemaining;
                MergeCountDictionary(anatomyDistribution, batch.AnatomyDistribution);
                MergeCountDictionary(pathologyDistribution, batch.PathologyDistribution);

                if (batch.LastChunkOrder > afterChunkOrder)
                    afterChunkOrder = batch.LastChunkOrder;
                sectionAnatomy = batch.SectionAnatomy ?? sectionAnatomy;
                sectionPathology = batch.SectionPathology ?? sectionPathology;

                if (onBatchProgressAsync != null)
                    await onBatchProgressAsync(totalProcessed, nullRemaining, cancellationToken);

                if (!batch.HasMore)
                    break;
            }

            if (phase is DocumentEnrichPhase.Embeddings or DocumentEnrichPhase.All && nullRemaining > 0)
            {
                return EnrichFail(
                    documentId,
                    500,
                    $"{nullRemaining} chunk(s) still missing embeddings after batched enrichment.");
            }

            return new DocumentChunkEnrichmentResultDto(
                Success: true,
                StatusCode: 200,
                ErrorMessage: null,
                DocumentId: documentId,
                ChunksProcessed: totalProcessed,
                EmbeddingModel: embeddingModel,
                AnatomyDistribution: anatomyDistribution,
                PathologyDistribution: pathologyDistribution,
                NullEmbeddingRemaining: 0);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Python document enrich timed out.");
            return EnrichFail(documentId, 0, "Request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Python document enrich request failed.");
            return EnrichFail(documentId, 0, ex.Message);
        }
    }

    private async Task<DocumentChunkEnrichmentResultDto> PostEnrichBatchAsync(
        Guid documentId,
        string enrichPhase,
        bool onlyMissingEmbedding,
        int batchSize,
        int afterChunkOrder,
        string? sectionAnatomy,
        string? sectionPathology,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 4;
        static bool IsRetryable(int statusCode) =>
            statusCode is 502 or 503 or 504;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var resp = await _httpClient.PostAsJsonAsync(
                "api/v1/documents/enrich-chunks",
                new EnrichChunksPayload(
                    documentId,
                    enrichPhase,
                    onlyMissingEmbedding,
                    batchSize,
                    afterChunkOrder,
                    sectionAnatomy,
                    sectionPathology),
                SerializerSnakeWrite,
                cancellationToken);

            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Python document enrich batch failed (attempt {Attempt}/{MaxAttempts}): {Status} {Body}",
                    attempt,
                    maxAttempts,
                    (int)resp.StatusCode,
                    body.Length > 800 ? body[..800] + "…" : body);

                if (attempt < maxAttempts && IsRetryable((int)resp.StatusCode))
                {
                    await Task.Delay(TimeSpan.FromSeconds(4 * attempt), cancellationToken);
                    continue;
                }

                var detail = TryExtractFastApiErrorDetail(body);
                return EnrichFail(
                    documentId,
                    (int)resp.StatusCode,
                    string.IsNullOrWhiteSpace(detail)
                        ? $"HTTP {(int)resp.StatusCode}"
                        : detail);
            }

            EnrichApiBody? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<EnrichApiBody>(body, SerializerSnakeRead);
            }
            catch (JsonException jex)
            {
                _logger.LogWarning(jex, "Python document enrich JSON parse failed.");
                return EnrichFail(documentId, (int)resp.StatusCode, "Invalid JSON from AI service.");
            }

            if (parsed == null)
                return EnrichFail(documentId, (int)resp.StatusCode, "Empty enrich response.");

            return new DocumentChunkEnrichmentResultDto(
                Success: true,
                StatusCode: (int)resp.StatusCode,
                ErrorMessage: null,
                DocumentId: documentId,
                ChunksProcessed: parsed.ChunksProcessed,
                EmbeddingModel: parsed.EmbeddingModel,
                AnatomyDistribution: parsed.AnatomyDistribution,
                PathologyDistribution: parsed.PathologyDistribution,
                NullEmbeddingRemaining: parsed.NullEmbeddingRemaining,
                LastChunkOrder: parsed.LastChunkOrder,
                SectionAnatomy: parsed.SectionAnatomy,
                SectionPathology: parsed.SectionPathology,
                HasMore: parsed.HasMore);
        }

        return EnrichFail(documentId, 502, "HTTP 502");
    }

    private static void MergeCountDictionary(
        Dictionary<string, int> target,
        IReadOnlyDictionary<string, int>? source)
    {
        if (source == null)
            return;

        foreach (var (key, value) in source)
        {
            if (target.TryGetValue(key, out var existing))
                target[key] = existing + value;
            else
                target[key] = value;
        }
    }



    private static string? NullIfWhiteSpace(string? s) =>

        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Parse FastAPI <c>{"detail": "..."}</c> or validation error arrays from error bodies.</summary>
    private static string? TryExtractFastApiErrorDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("detail", out var detailEl))
                return null;

            if (detailEl.ValueKind == JsonValueKind.String)
            {
                var text = detailEl.GetString()?.Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }

            if (detailEl.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var item in detailEl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;
                    if (item.TryGetProperty("msg", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
                    {
                        var msg = msgEl.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(msg))
                            parts.Add(msg);
                    }
                }

                return parts.Count == 0 ? null : string.Join("; ", parts);
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }



    private static IngestResultDto IngestFail(int statusCode, string message) =>
        new(false, statusCode, message, null, null, null, null, null, null);



    private RagResponseDto RagFail(int statusCode, string message) =>

        new(false, statusCode, message, string.Empty, Array.Empty<RagContextItemDto>(), 0);



    private static DocumentChunkEnrichmentResultDto EnrichFail(Guid documentId, int statusCode, string message) =>
        new(false, statusCode, message, documentId, 0, null, null, null, 0, -1, null, null, false);



    private sealed record EnrichChunksPayload(
        Guid DocId,
        string EnrichPhase,
        bool OnlyMissingEmbedding,
        int BatchSize,
        int AfterChunkOrder,
        string? SectionAnatomy,
        string? SectionPathology);



    private sealed class EnrichApiBody

    {

        public string? DocId { get; set; }

        public int ChunksProcessed { get; set; }

        public string? EmbeddingModel { get; set; }

        public Dictionary<string, int>? AnatomyDistribution { get; set; }

        public Dictionary<string, int>? PathologyDistribution { get; set; }

        public int NullEmbeddingRemaining { get; set; }

        public int LastChunkOrder { get; set; }

        public string? SectionAnatomy { get; set; }

        public string? SectionPathology { get; set; }

        public bool HasMore { get; set; }

    }



    private sealed record IngestPayload(

        string DicomPath,

        string? DiagnosisText,

        string IngestPurpose,

        Guid? OwnerUserId);



    private sealed record RagPayload(
        string UserQuestion,
        string Modality,
        string Anatomy,
        string? PathologyGroup,
        IReadOnlyList<float>? ImageEmbedding,
        Guid? CaseId,
        Guid? CaseMediaId,
        string? DicomClinicalContext,
        JsonElement? DicomMetadata);



    private sealed class IngestApiBody

    {

        public string? CaseId { get; set; }

        public string? MediaId { get; set; }

        public string? CatalogImageId { get; set; }

        public string? PreviewImageUrl { get; set; }

        [JsonPropertyName("dicom_metadata")]
        public JsonElement? DicomMetadata { get; set; }
    }



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


