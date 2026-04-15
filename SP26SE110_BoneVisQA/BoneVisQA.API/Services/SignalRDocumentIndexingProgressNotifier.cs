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
                progressPercentage,
                operation
            },
            cancellationToken);
    }
}
