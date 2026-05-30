using BoneVisQA.Services.Constants;

namespace BoneVisQA.Services.Helpers;

/// <summary>Maps optional expert review queue <c>status</c> query values to persisted Visual QA session statuses.</summary>
public static class ExpertReviewStatusFilter
{
    private static readonly string[] Pending = [CaseAnswerStatuses.EscalatedToExpert];
    private static readonly string[] Approved = [CaseAnswerStatuses.ExpertApproved];
    private static readonly string[] Rejected = [CaseAnswerStatuses.Rejected];
    private static readonly string[] History =
    [
        CaseAnswerStatuses.ExpertApproved,
        CaseAnswerStatuses.Rejected
    ];

    public static IReadOnlyList<string> ResolveVisualQaStatuses(string? statusFilter)
    {
        var normalized = statusFilter?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return Pending;

        return normalized.ToLowerInvariant() switch
        {
            "pending" or "escalated" or "escalatedtoexpert" => Pending,
            "approved" or "expertapproved" => Approved,
            "rejected" => Rejected,
            "resolved" or "history" => History,
            _ => [normalized]
        };
    }

    public static bool IsPendingQueueFilter(IReadOnlyList<string> statuses) =>
        statuses.Count == 1 &&
        string.Equals(statuses[0], CaseAnswerStatuses.EscalatedToExpert, StringComparison.Ordinal);
}
