namespace BoneVisQA.Services.Constants;

/// <summary>
/// Smart triage: lecturer queue only surfaces answers whose RAG similarity is below this bar (unless escalated / terminal status).
/// </summary>
public static class LecturerTriageThresholds
{
    /// <summary>Minimum top-chunk cosine similarity (0–1) to auto-approve AI output and skip the lecturer triage queue.</summary>
    public const double MinConfidenceToBypassTriage = 0.75d;

    /// <summary>
    /// Determines whether an answer should appear on the lecturer triage workbench for a class.
    /// Escalations always surface; terminal lecturer/expert outcomes are excluded; otherwise the AI score gate applies.
    /// </summary>
    public static bool IsInLecturerTriageQueue(string? status, double? aiConfidenceScore)
    {
        if (CaseAnswerStatuses.IsEscalatedToExpert(status))
            return true;

        if (string.Equals(status, CaseAnswerStatuses.ExpertApproved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CaseAnswerStatuses.Approved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CaseAnswerStatuses.Revised, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CaseAnswerStatuses.Edited, StringComparison.OrdinalIgnoreCase))
            return false;

        return !aiConfidenceScore.HasValue || aiConfidenceScore.Value < MinConfidenceToBypassTriage;
    }
}
