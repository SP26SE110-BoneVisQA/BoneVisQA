using System.Security.Claims;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Models.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Tags("Notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>Notifications for the current user, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetMine()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var list = await _unitOfWork.Context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                TargetUrl = n.TargetUrl,
                Route = NotificationAppRoute.Normalize(n.TargetUrl),
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Mark a notification as read (must belong to current user).</summary>
    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var entity = await _unitOfWork.Context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId.Value);

        if (entity == null)
            return NotFound(new { message = "Notification not found." });

        if (!entity.IsRead)
        {
            entity.IsRead = true;
            _unitOfWork.Context.Notifications.Update(entity);
            await _unitOfWork.SaveAsync();
        }

        return NoContent();
    }

    /// <summary>Mark all notifications as read for the current user.</summary>
    [HttpPut("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var unread = await _unitOfWork.Context.Notifications
            .Where(n => n.UserId == userId.Value && !n.IsRead)
            .ToListAsync();

        if (unread.Count == 0)
            return NoContent();

        foreach (var entity in unread)
            entity.IsRead = true;

        await _unitOfWork.SaveAsync();
        return NoContent();
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
