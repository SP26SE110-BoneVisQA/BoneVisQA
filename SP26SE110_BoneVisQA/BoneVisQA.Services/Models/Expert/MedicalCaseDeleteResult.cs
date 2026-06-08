namespace BoneVisQA.Services.Models.Expert;

public sealed class MedicalCaseDeleteResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public int UnlinkedSessionCount { get; init; }

    public static MedicalCaseDeleteResult Deleted(int unlinkedSessions = 0) => new()
    {
        Success = true,
        UnlinkedSessionCount = unlinkedSessions
    };

    public static MedicalCaseDeleteResult NotFound() => new()
    {
        Success = false,
        ErrorCode = "NOT_FOUND",
        Message = "The requested medical case was not found."
    };

    public static MedicalCaseDeleteResult Forbidden() => new()
    {
        Success = false,
        ErrorCode = "FORBIDDEN",
        Message = "You do not have permission to delete this medical case."
    };

    public static MedicalCaseDeleteResult Blocked(string detail) => new()
    {
        Success = false,
        ErrorCode = "DELETE_BLOCKED",
        Message = detail
    };
}
