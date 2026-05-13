using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer/notifications")]
[Tags("Lecturer - Notifications")]
[Authorize(Roles = "Lecturer")]
public class LecturerNotificationsController : ControllerBase
{
    private readonly ILecturerNotificationService _notificationService;

    public LecturerNotificationsController(ILecturerNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private Guid? GetLecturerId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    /// <summary>
    /// Get notification summary with badge counts for the lecturer dashboard.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetNotificationSummary()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var summary = await _notificationService.GetNotificationSummaryAsync(lecturerId.Value);
        return Ok(summary);
    }

    /// <summary>
    /// Get recent notifications for the lecturer.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecentNotifications([FromQuery] int limit = 20)
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (limit < 1) limit = 20;
        if (limit > 100) limit = 100;

        var notifications = await _notificationService.GetRecentNotificationsAsync(lecturerId.Value, limit);
        return Ok(notifications);
    }

    /// <summary>
    /// Get pending questions count for badge display.
    /// </summary>
    [HttpGet("pending-questions/count")]
    public async Task<IActionResult> GetPendingQuestionsCount()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var count = await _notificationService.GetPendingQuestionsCountAsync(lecturerId.Value);
        return Ok(new { count });
    }

    /// <summary>
    /// Get escalated answers count for badge display.
    /// </summary>
    [HttpGet("escalated/count")]
    public async Task<IActionResult> GetEscalatedAnswersCount()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var count = await _notificationService.GetEscalatedAnswersCountAsync(lecturerId.Value);
        return Ok(new { count });
    }

    /// <summary>
    /// Get pending review count for badge display.
    /// </summary>
    [HttpGet("pending-review/count")]
    public async Task<IActionResult> GetPendingReviewCount()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var count = await _notificationService.GetPendingReviewCountAsync(lecturerId.Value);
        return Ok(new { count });
    }

    /// <summary>
    /// Mark a notification as read.
    /// </summary>
    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId)
    {
        await _notificationService.MarkNotificationAsReadAsync(notificationId);
        return NoContent();
    }

    /// <summary>
    /// Mark all notifications as read for the lecturer.
    /// </summary>
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        await _notificationService.MarkAllNotificationsAsReadAsync(lecturerId.Value);
        return NoContent();
    }
}
