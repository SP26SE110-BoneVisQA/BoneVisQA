namespace BoneVisQA.Services.Interfaces;

/// <summary>
/// Gateway client for the BoneVisQA.AI FastAPI service (<c>POST /ingest</c>, <c>POST /api/v1/qa/ask</c>).
/// </summary>
public interface IPythonAiConnectorService
{
    /// <summary>
    /// POST <c>/ingest</c>. <paramref name="dicomUrl"/> is sent as <c>dicom_path</c> (local path or URI string per Python deployment).
    /// </summary>
    Task<bool> TriggerIngestAsync(string dicomUrl, string patientId, string diagnosis, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST <c>/api/v1/qa/ask</c>; parses hybrid RAG payload into <see cref="RagResponseDto"/>.
    /// </summary>
    Task<RagResponseDto> AskRagAsync(string question, string modality, string anatomy, string? pathologyGroup = null, CancellationToken cancellationToken = default);
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
