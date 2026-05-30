using System.Text.Json;

namespace BoneVisQA.Services.Interfaces;

/// <summary>
/// Gateway client for the BoneVisQA.AI FastAPI service (<c>POST /ingest</c>, <c>POST /api/v1/qa/ask</c>).
/// </summary>
public interface IPythonAiConnectorService
{
    /// <summary>
    /// POST <c>/ingest</c>. <paramref name="mediaPath"/> is sent as <c>dicom_path</c> (local archive path).
    /// </summary>
    Task<IngestResultDto> TriggerIngestAsync(
        string mediaPath,
        string diagnosis,
        string ingestPurpose = "library",
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST <c>/api/v1/qa/ask</c>; parses hybrid RAG payload into <see cref="RagResponseDto"/>.
    /// </summary>
    Task<RagResponseDto> AskRagAsync(
        string question,
        string modality,
        string anatomy,
        string? pathologyGroup = null,
        Guid? caseId = null,
        Guid? caseMediaId = null,
        IReadOnlyList<float>? imageEmbedding = null,
        string? dicomClinicalContext = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Parsed successful response from Python hybrid RAG; on failure <see cref="Success"/> is false.</summary>
public sealed record RagResponseDto(
    bool Success,
    int StatusCode,
    string? ErrorMessage,
    string Prompt,
    IReadOnlyList<RagContextItemDto> Context,
    int RetrievalCount);

public sealed record RagContextItemDto(
    int Rank,
    string? Source,
    string? RefId,
    string? PathologyGroup,
    double Distance,
    string? Excerpt);

/// <summary>Parsed response from Python <c>POST /ingest</c>.</summary>
public sealed record IngestResultDto(
    bool Success,
    int StatusCode,
    string? ErrorMessage,
    Guid? CaseId,
    Guid? MediaId,
    Guid? CatalogImageId,
    string? PreviewImageUrl,
    JsonElement? DicomMetadata,
    string? RawJson);
