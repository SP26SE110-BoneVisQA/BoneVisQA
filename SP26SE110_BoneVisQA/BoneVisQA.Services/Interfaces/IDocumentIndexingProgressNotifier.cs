namespace BoneVisQA.Services.Interfaces;

public interface IDocumentIndexingProgressNotifier
{
    Task NotifyProgressAsync(
        Guid documentId,
        int totalPages,
        int totalChunks,
        int currentPageIndexing,
        int progressPercentage,
        string operation,
        CancellationToken cancellationToken = default,
        int indexingPhase = 0,
        int chunksProcessed = 0,
        string? phaseLabel = null);

    /// <summary>Emitted after a successful atomic swap so clients can refresh without polling.</summary>
    Task NotifyIndexingCompletedAsync(
        Guid documentId,
        string status,
        string version,
        DateTime lastUpdatedUtc,
        CancellationToken cancellationToken = default);

    Task NotifyIndexingFailedAsync(
        Guid documentId,
        string status,
        string errorMessage,
        int totalPages,
        int totalChunks,
        int currentPageIndexing,
        CancellationToken cancellationToken = default);
}
