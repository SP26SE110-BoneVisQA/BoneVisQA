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
        CancellationToken cancellationToken = default);

    /// <summary>Emitted after a successful atomic swap so clients can refresh without polling.</summary>
    Task NotifyIndexingCompletedAsync(
        Guid documentId,
        string status,
        string version,
        DateTime lastUpdatedUtc,
        CancellationToken cancellationToken = default);
}
