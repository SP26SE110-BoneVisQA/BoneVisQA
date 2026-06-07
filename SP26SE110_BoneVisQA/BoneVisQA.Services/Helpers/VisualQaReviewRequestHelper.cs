using BoneVisQA.Repositories.Models;

namespace BoneVisQA.Services.Helpers;

public enum VisualQaStudentReviewRoute
{
    None,
    Lecturer,
    Expert,
}

public static class VisualQaReviewRequestHelper
{
    /// <summary>
    /// <paramref name="turnId"/> may be an assistant message id (preferred) or the paired user message id from <see cref="Models.VisualQA.VisualQaTurnDto.TurnId"/>.
    /// </summary>
    public static QAMessage? ResolveAssistantMessageForReview(VisualQASession session, Guid? turnId)
    {
        var messages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();

        if (!turnId.HasValue || turnId.Value == Guid.Empty)
        {
            return messages
                .Where(m => string.Equals(m.Role, "Assistant", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();
        }

        var byAssistant = messages.FirstOrDefault(m =>
            m.Id == turnId.Value && string.Equals(m.Role, "Assistant", StringComparison.OrdinalIgnoreCase));
        if (byAssistant != null)
            return byAssistant;

        var userMessage = messages.FirstOrDefault(m =>
            m.Id == turnId.Value && string.Equals(m.Role, "User", StringComparison.OrdinalIgnoreCase));
        if (userMessage == null)
            return null;

        return messages
            .Where(m =>
                string.Equals(m.Role, "Assistant", StringComparison.OrdinalIgnoreCase) &&
                m.CreatedAt >= userMessage.CreatedAt)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .FirstOrDefault()
            ?? messages
                .Where(m => string.Equals(m.Role, "Assistant", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();
    }

    public static bool IsReviewClosedStatus(string? status) =>
        string.Equals(status, "ExpertApproved", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase);

    public static bool IsReviewInProgressStatus(string? status) =>
        string.Equals(status, "PendingExpertReview", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "EscalatedToExpert", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "LecturerApproved", StringComparison.OrdinalIgnoreCase);

    public static string MapReviewErrorCode(string message) =>
        message switch
        {
            "SESSION_EXPIRED" => "SESSION_EXPIRED",
            "TURN_LIMIT_EXCEEDED" => "TURN_LIMIT_EXCEEDED",
            "REVIEW_ALREADY_REQUESTED" => "REVIEW_ALREADY_REQUESTED",
            "REVIEW_CLOSED" => "REVIEW_CLOSED",
            "NO_REVIEW_PATH" => "NO_REVIEW_PATH",
            _ when message.Contains("current state", StringComparison.OrdinalIgnoreCase) => "INVALID_SESSION_STATUS",
            _ => "REVIEW_FORBIDDEN",
        };
}
