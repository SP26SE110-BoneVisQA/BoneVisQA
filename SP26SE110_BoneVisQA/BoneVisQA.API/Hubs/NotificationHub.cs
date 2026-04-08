using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BoneVisQA.API.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "NotificationHub connected. ConnectionId={ConnectionId}, User={User}",
            Context.ConnectionId,
            Context.UserIdentifier ?? "unknown");

        await base.OnConnectedAsync();
    }
}
