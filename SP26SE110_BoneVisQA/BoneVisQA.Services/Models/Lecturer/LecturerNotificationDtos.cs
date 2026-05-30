using System;

namespace BoneVisQA.Services.Models.Lecturer;

public class LecturerNotificationSummaryDto
{
    public int PendingQuestionsCount { get; set; }
    public int EscalatedAnswersCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int UnreadNotificationsCount { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public class LecturerNotificationItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class LecturerBadgeCountDto
{
    public int TotalPending { get; set; }
    public int Questions { get; set; }
    public int Escalations { get; set; }
    public int Reviews { get; set; }
}
