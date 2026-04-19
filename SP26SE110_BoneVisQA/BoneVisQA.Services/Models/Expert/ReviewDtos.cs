using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BoneVisQA.Services.Models.Expert;

public class ResolveEscalatedAnswerRequestDto
{
    public string AnswerText { get; set; } = string.Empty;
    public string? StructuredDiagnosis { get; set; }

    /// <summary>FE may send a JSON string or a string array; stored as text on <c>case_answers</c>.</summary>
    public JsonElement? DifferentialDiagnoses { get; set; }
    public string? KeyImagingFindings { get; set; }
    public string? ReflectiveQuestions { get; set; }
    public string? ReviewNote { get; set; }
}

public class FlagChunkRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ExpertRespondRequestDto
{
    public string Content { get; set; } = string.Empty;
}

public class PromoteToLibraryRequestDto
{
    [Required]
    public string KeyFindings { get; set; } = string.Empty;

    [Required]
    public string ReflectiveQuestions { get; set; } = string.Empty;

    [Required]
    public string SuggestedDiagnosis { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;
}

public class ExpertCitationDto
{
    public Guid ChunkId { get; set; }
    public string? SourceText { get; set; }
    public string? ReferenceUrl { get; set; }
    public int? PageNumber { get; set; }
    public int? StartPage { get; set; }
    public int? EndPage { get; set; }
}

public class ExpertEscalatedAnswerDto
{
    public Guid AnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    /// <summary>Full case description when session is case-based.</summary>
    public string? CaseDescription { get; set; }
    /// <summary>Expert teaching diagnosis from case.</summary>
    public string? CaseSuggestedDiagnosis { get; set; }
    /// <summary>Expert key findings from case.</summary>
    public string? CaseKeyFindings { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? CurrentAnswerText { get; set; }
    public string? StructuredDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    public string? KeyImagingFindings { get; set; }
    public string? ReflectiveQuestions { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? EscalatedById { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public double? AiConfidenceScore { get; set; }
    public Guid? ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public Guid? PromotedCaseId { get; set; }
    public List<ExpertCitationDto> Citations { get; set; } = new();

    /// <summary>Resolved study image (Visual QA upload or first case image).</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Normalized ROI JSON on <c>student_questions.custom_coordinates</c>.</summary>
    public string? CustomCoordinates { get; set; }
    public Guid? RequestedReviewMessageId { get; set; }
    public Guid? SelectedUserMessageId { get; set; }
    public Guid? SelectedAssistantMessageId { get; set; }
}

// Expert Dashboard DTOs
public class ExpertDashboardStatsDto
{
    public int TotalCases { get; set; }
    public int TotalReviews { get; set; }
    public int PendingReviews { get; set; }
    public int ApprovedThisMonth { get; set; }
    public int StudentInteractions { get; set; }
}

public class ExpertDashboardPendingReviewDto
{
    public Guid Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string CaseTitle { get; set; } = string.Empty;
    public string QuestionSnippet { get; set; } = string.Empty;
    public string AiAnswerSnippet { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string Priority { get; set; } = "normal";
    public string Category { get; set; } = string.Empty;
}

public class ExpertDashboardRecentCaseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BoneLocation { get; set; } = string.Empty;
    public string LesionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AddedBy { get; set; } = string.Empty;
    public DateTime AddedDate { get; set; }
    public int ViewCount { get; set; }
    public int UsageCount { get; set; }
}

public class ExpertDashboardActivityDto
{
    public List<DailyActivityItemDto> WeeklyActivity { get; set; } = new();
    public float AvgDailyReviews { get; set; }
}

public class DailyActivityItemDto
{
    public string Day { get; set; } = string.Empty;
    public int Reviews { get; set; }
    public int Cases { get; set; }
}
