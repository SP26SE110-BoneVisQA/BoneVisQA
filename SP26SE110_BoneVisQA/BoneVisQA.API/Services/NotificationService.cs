using BoneVisQA.API.Hubs;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Notification;
using Microsoft.AspNetCore.SignalR;

namespace BoneVisQA.API.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(IUnitOfWork unitOfWork, IHubContext<NotificationHub> hubContext)
    {
        _unitOfWork = unitOfWork;
        _hubContext = hubContext;
    }

    public async Task<NotificationDto> SendNotificationToUserAsync(Guid userId, string title, string message, string type, string? targetUrl)
    {
        var entity = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            TargetUrl = targetUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Context.Notifications.AddAsync(entity);
        await _unitOfWork.SaveAsync();

        var dto = new NotificationDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Title = entity.Title,
            Message = entity.Message,
            Type = entity.Type,
            TargetUrl = entity.TargetUrl,
            IsRead = entity.IsRead,
            CreatedAt = entity.CreatedAt
        };

        await _hubContext.Clients.User(userId.ToString())
            .SendAsync("ReceiveNotification", dto);

        return dto;
    }
}
