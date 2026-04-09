namespace BoneVisQA.Services.Constants;

/// <summary>Values persisted in <c>case_answers.status</c> (must match PostgreSQL check constraint).</summary>
public static class CaseAnswerStatuses
{
    public const string Pending = "Pending";
    /// <summary>AI output needs lecturer triage (low confidence or not auto-approved).</summary>
    public const string RequiresLecturerReview = "RequiresLecturerReview";
    /// <summary>Lecturer escalated to assigned class expert.</summary>
    public const string EscalatedToExpert = "EscalatedToExpert";
    /// <summary>Expert finalized the answer after review.</summary>
    public const string ExpertApproved = "ExpertApproved";

    public const string Approved = "Approved";
    public const string Edited = "Edited";
    public const string Rejected = "Rejected";

    /// <summary>Legacy; prefer <see cref="EscalatedToExpert"/>.</summary>
    public const string Escalated = "Escalated";
    /// <summary>Legacy expert outcome; prefer <see cref="ExpertApproved"/>.</summary>
    public const string Revised = "Revised";

    public static bool IsEscalatedToExpert(string? status) =>
        string.Equals(status, EscalatedToExpert, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Escalated, StringComparison.OrdinalIgnoreCase);

    public static bool CanEscalateFromLecturer(string? status) =>
        string.Equals(status, RequiresLecturerReview, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Rejected, StringComparison.OrdinalIgnoreCase);
}
