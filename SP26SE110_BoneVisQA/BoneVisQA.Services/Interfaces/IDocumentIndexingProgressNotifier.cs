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
}
