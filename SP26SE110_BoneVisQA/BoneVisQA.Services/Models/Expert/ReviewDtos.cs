using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Models.Expert;

public class ResolveEscalatedAnswerRequestDto
{
    public string Decision { get; set; } = "approve";
    public string AnswerText { get; set; } = string.Empty;
    public string? StructuredDiagnosis { get; set; }
    public JsonElement? DifferentialDiagnoses { get; set; }
    public string? KeyImagingFindings { get; set; }
    public string? ReflectiveQuestions { get; set; }
    public string? ReviewNote { get; set; }
    public double[]? CorrectedRoiBoundingBox { get; set; }
}

public class FlagChunkRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ExpertRespondRequestDto
{
    public string Content { get; set; } = string.Empty;
}

/// <summary>ROI payload for promote-to-library; stored in <c>case_annotations.coordinates</c> (JSON text).</summary>
public class PromoteCaseAnnotationDto
{
    public string? Label { get; set; }

    /// <summary>BBox / polygon / normalized ROI from FE (object or primitive JSON).</summary>
    public JsonElement? Coordinates { get; set; }
}

public class PromoteToLibraryRequestDto
{
    /// <summary>Optional; when empty a default community title is used.</summary>
    public string? Title { get; set; }

    public Guid? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string? Difficulty { get; set; }

    public List<string>? TagNames { get; set; }

    public List<PromoteCaseAnnotationDto>? TurnAnnotations { get; set; }

    public List<PromoteCaseAnnotationDto>? ImageAnnotations { get; set; }

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
    /// <summary>Same as <see cref="ChunkId"/> (FE merge / flag APIs).</summary>
    public Guid DocumentChunkId => ChunkId;
    /// <summary>RAG library document (<c>documents.id</c>).</summary>
    public Guid? DocumentId { get; set; }
    public Guid? MedicalCaseId { get; set; }
    public string? SourceText { get; set; }
    public string? ReferenceUrl { get; set; }
    public string? Href { get; set; }
    public int? PageNumber { get; set; }
    public int? StartPage { get; set; }
    public int? EndPage { get; set; }
    public string? PageLabel { get; set; }
    public string? DisplayLabel { get; set; }
    public string? Snippet { get; set; }
    /// <summary>Same excerpt as <see cref="Snippet"/> (many clients bind <c>preview</c>).</summary>
    public string? Preview { get; set; }
    public string Kind { get; set; } = "doc";
}

public class ExpertVisualSessionDraftRequestDto
{
    public string? ReviewNote { get; set; }
    public double[]? CorrectedRoiBoundingBox { get; set; }
}

public class ExpertVisualSessionDraftResponseDto
{
    public Guid SessionId { get; set; }
    public Guid ReviewRowId { get; set; }
    public string? ReviewNote { get; set; }
    public double[]? ExpertCorrectedRoiBoundingBox { get; set; }
}

public class ExpertEscalatedAnswerDto
{
    public Guid AnswerId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public string? CaseDescription { get; set; }
    public string? CaseSuggestedDiagnosis { get; set; }
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

    public string? ImageUrl { get; set; }

    public string? CustomCoordinates { get; set; }
    public double[]? ExpertCorrectedRoiBoundingBox { get; set; }
    public Guid? RequestedReviewMessageId { get; set; }
    public Guid? SelectedUserMessageId { get; set; }
    public Guid? SelectedAssistantMessageId { get; set; }
    public IReadOnlyList<VisualQaTurnDto> Turns { get; set; } = Array.Empty<VisualQaTurnDto>();
}

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
