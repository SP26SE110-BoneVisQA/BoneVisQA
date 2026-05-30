using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Models.Expert;

public class ResolveEscalatedAnswerRequestDto
{
    [RegularExpression("^$|^(approve|reject)$", ErrorMessage = "Decision must be approve or reject.")]
    public string Decision { get; set; } = "approve";

    [Required(AllowEmptyStrings = false)]
    [StringLength(8000, MinimumLength = 3)]
    public string AnswerText { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? StructuredDiagnosis { get; set; }

    public JsonElement? DifferentialDiagnoses { get; set; }

    [StringLength(4000)]
    public string? KeyImagingFindings { get; set; }

    [StringLength(4000)]
    public string? ReflectiveQuestions { get; set; }

    [StringLength(2000)]
    public string? ReviewNote { get; set; }

    [MinLength(4)]
    [MaxLength(4)]
    public double[]? CorrectedRoiBoundingBox { get; set; }
}

public class FlagChunkRequestDto
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(1000, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public class ExpertRespondRequestDto
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(8000, MinimumLength = 3)]
    public string Content { get; set; } = string.Empty;
}

/// <summary>ROI payload for promote-to-library; stored in <c>case_annotations.coordinates</c> (JSON text).</summary>
public class PromoteCaseAnnotationDto
{
    [StringLength(200)]
    public string? Label { get; set; }

    /// <summary>BBox / polygon / normalized ROI from FE (object or primitive JSON).</summary>
    public JsonElement? Coordinates { get; set; }
}

public class PromoteToLibraryRequestDto
{
    /// <summary>Optional; when empty a default community title is used.</summary>
    [StringLength(256)]
    public string? Title { get; set; }

    public Guid? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    /// <summary>Easy / Medium / Hard (strict ontology).</summary>
    public string Difficulty { get; set; } = string.Empty;

    [MaxLength(20)]
    public List<string>? TagNames { get; set; }

    public List<PromoteCaseAnnotationDto>? TurnAnnotations { get; set; }

    public List<PromoteCaseAnnotationDto>? ImageAnnotations { get; set; }

    [Required]
    [StringLength(4000, MinimumLength = 10)]
    public string KeyFindings { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 10)]
    public string ReflectiveQuestions { get; set; } = string.Empty;

    [Required]
    [StringLength(2000, MinimumLength = 3)]
    public string SuggestedDiagnosis { get; set; } = string.Empty;

    [Required]
    [StringLength(8000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    /// <summary>X-Ray, CT, MRI, or Ultrasound (matches <c>case_metadata.modality</c>).</summary>
    [Required]
    [StringLength(50)]
    public string Modality { get; set; } = string.Empty;

    /// <summary>Fine-grained site (Spine, Knee, …).</summary>
    [Required]
    [StringLength(100)]
    public string AnatomySite { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Laterality { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string ViewPosition { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string PathologyGroup { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string SourceType { get; set; } = string.Empty;

    /// <summary>0.0–1.0 quality score for ontology / DICOM-derived QA.</summary>
    public float QualityScore { get; set; } = 0.85f;

    [Required]
    [StringLength(8000, MinimumLength = 10)]
    public string ClinicalEvidence { get; set; } = string.Empty;

    /// <summary>At least two distinct lines required by the expert promotion gate.</summary>
    [MinLength(2)]
    [MaxLength(10)]
    public List<string> DifferentialDiagnoses { get; set; } = new();
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
    [StringLength(2000)]
    public string? ReviewNote { get; set; }

    [MinLength(4)]
    [MaxLength(4)]
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

    /// <summary>Flattened AI answer for expert triage UI (includes structured fields when <c>Content</c> is empty).</summary>
    [JsonPropertyName("answerText")]
    public string? AnswerText { get; set; }

    public string? CurrentAnswerText { get; set; }
    public string? StructuredDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    public string? KeyImagingFindings { get; set; }
    public string? ReflectiveQuestions { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Workflow status on <c>visual_qa_sessions.status</c> (alias of <see cref="Status"/> for FE clarity).</summary>
    [JsonPropertyName("sessionStatus")]
    public string SessionStatus { get; set; } = string.Empty;

    /// <summary>Human review note on <c>visual_qa_sessions.review_feedback</c>; separate from AI assistant JSON fields.</summary>
    [JsonPropertyName("reviewFeedback")]
    public string? ReviewFeedback { get; set; }

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

    [JsonPropertyName("dicomMetadata")]
    public JsonElement? DicomMetadata { get; set; }
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
