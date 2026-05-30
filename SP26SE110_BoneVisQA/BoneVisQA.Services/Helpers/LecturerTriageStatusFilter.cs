using BoneVisQA.Services.Constants;

namespace BoneVisQA.Services.Helpers;

/// <summary>Maps optional triage list <c>status</c> query values to persisted Visual QA / Case QA statuses.</summary>
public static class LecturerTriageStatusFilter
{
    private static readonly string[] VisualQaPending = ["PendingExpertReview"];
    private static readonly string[] VisualQaApproved = ["LecturerApproved"];
    private static readonly string[] VisualQaRejected = ["Rejected"];
    private static readonly string[] VisualQaResolved =
    [
        "LecturerApproved",
        "Rejected",
        CaseAnswerStatuses.EscalatedToExpert,
        CaseAnswerStatuses.ExpertApproved
    ];

    private static readonly string[] CaseQaPending = [CaseAnswerStatuses.RequiresLecturerReview];
    private static readonly string[] CaseQaApproved =
    [
        CaseAnswerStatuses.Approved,
        CaseAnswerStatuses.Edited,
        CaseAnswerStatuses.Revised
    ];
    private static readonly string[] CaseQaRejected = [CaseAnswerStatuses.Rejected];
    private static readonly string[] CaseQaResolved =
    [
        CaseAnswerStatuses.Approved,
        CaseAnswerStatuses.Edited,
        CaseAnswerStatuses.Revised,
        CaseAnswerStatuses.Rejected,
        CaseAnswerStatuses.Escalated,
        CaseAnswerStatuses.EscalatedToExpert,
        CaseAnswerStatuses.ExpertApproved
    ];

    public static IReadOnlyList<string> ResolveVisualQaStatuses(string? statusFilter)
    {
        var normalized = statusFilter?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return VisualQaPending;

        return normalized.ToLowerInvariant() switch
        {
            "pending" => VisualQaPending,
            "approved" or "lecturerapproved" => VisualQaApproved,
            "rejected" => VisualQaRejected,
            "resolved" or "history" => VisualQaResolved,
            "escalated" or "escalatedtoexpert" => [CaseAnswerStatuses.EscalatedToExpert],
            _ => [normalized]
        };
    }

    public static IReadOnlyList<string> ResolveCaseQaStatuses(string? statusFilter)
    {
        var normalized = statusFilter?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return CaseQaPending;

        return normalized.ToLowerInvariant() switch
        {
            "pending" => CaseQaPending,
            "approved" => CaseQaApproved,
            "rejected" => CaseQaRejected,
            "resolved" or "history" => CaseQaResolved,
            "escalated" or "escalatedtoexpert" => [CaseAnswerStatuses.Escalated, CaseAnswerStatuses.EscalatedToExpert],
            _ => [normalized]
        };
    }
}
