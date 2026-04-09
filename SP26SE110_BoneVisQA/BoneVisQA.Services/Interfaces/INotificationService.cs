using System;
using System.Threading.Tasks;
using BoneVisQA.Services.Models.Notification;

namespace BoneVisQA.Services.Interfaces;

public interface INotificationService
{
    Task<NotificationDto> SendNotificationToUserAsync(Guid userId, string title, string message, string type, string? targetUrl);
}
