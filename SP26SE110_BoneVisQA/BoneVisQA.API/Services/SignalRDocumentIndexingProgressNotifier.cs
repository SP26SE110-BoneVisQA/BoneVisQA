using BoneVisQA.API.Hubs;
using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BoneVisQA.API.Services;

public sealed class SignalRDocumentIndexingProgressNotifier : IDocumentIndexingProgressNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRDocumentIndexingProgressNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyProgressAsync(
        Guid documentId,
        int totalPages,
        int totalChunks,
        int currentPageIndexing,
        int progressPercentage,
        string operation,
        CancellationToken cancellationToken = default,
        int indexingPhase = 0,
        int chunksProcessed = 0,
        string? phaseLabel = null)
    {
        return _hubContext.Clients.All.SendAsync(
            "DocumentIndexingProgressUpdated",
            new
            {
                documentId,
                totalPages,
                totalChunks,
                currentPageIndexing,
                progressPercentage,
                operation,
                indexingPhase,
                chunksProcessed,
                phaseLabel
            },
            cancellationToken);
    }

    public Task NotifyIndexingCompletedAsync(
        Guid documentId,
        string status,
        string version,
        DateTime lastUpdatedUtc,
        CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "DocumentIndexingCompleted",
            new
            {
                documentId,
                status,
                version,
                lastUpdated = lastUpdatedUtc,
                progressPercentage = 100,
                operation = (string?)null,
                errorMessage = (string?)null
            },
            cancellationToken);
    }

    public Task NotifyIndexingFailedAsync(
        Guid documentId,
        string status,
        string errorMessage,
        int totalPages,
        int totalChunks,
        int currentPageIndexing,
        CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "DocumentIndexingProgressUpdated",
            new
            {
                documentId,
                totalPages,
                totalChunks,
                currentPageIndexing,
                progressPercentage = 100,
                status,
                operation = "Failed.",
                errorMessage
            },
            cancellationToken);
    }
}
