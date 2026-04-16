using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BoneVisQA.Services.Models.Lecturer;

/// <summary>Row returned by GET /api/lecturer/triage for the QA Triage workbench.</summary>
public class LecturerTriageRowDto
{
    /// <summary>Visual QA: session id. Case QA: <c>case_answers.id</c> (use for escalate / workflows).</summary>
    [JsonPropertyName("answerId")]
    public Guid AnswerId { get; set; }

    public Guid QuestionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentEmail { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string? CaseTitle { get; set; }
    /// <summary>X-ray / study image: Visual QA upload (<c>CustomImageUrl</c>) or first <c>MedicalImage</c> on the case.</summary>
    public string? ThumbnailUrl { get; set; }
    /// <summary>Resolved study image: personal upload or first case image (explicit for FE binding).</summary>
    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? AnswerText { get; set; }
    public string Status { get; set; } = "Pending";
    public double? AiConfidenceScore { get; set; }
    public DateTime? AskedAt { get; set; }
    public bool IsEscalated { get; set; }
    public string? EscalatedByName { get; set; }
    public DateTime? EscalatedAt { get; set; }

    /// <summary><c>VisualQA</c> (session-based) or <c>CaseQA</c> (student case question).</summary>
    public string TriageSource { get; set; } = "VisualQA";

    public string? StructuredDiagnosis { get; set; }
    public string? ReflectiveQuestions { get; set; }
    public string? KeyImagingFindings { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    public string? AnnotationLabel { get; set; }
    public string? AnnotationCoordinates { get; set; }
    public string? CustomCoordinates { get; set; }
    public string? CustomImageUrl { get; set; }
}

/// <summary>Full detail of a single student question for the lectuer to view and respond.</summary>
public class LectStudentQuestionDetailDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string? CaseTitle { get; set; }
    public string? CaseDescription { get; set; }
    public string? CaseThumbnailUrl { get; set; }
    /// <summary>Same resolved image as <see cref="CaseThumbnailUrl"/> (<c>imageUrl</c> for clients).</summary>
    public string? ImageUrl { get; set; }
    public string? CaseDifficulty { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Language { get; set; }
    public DateTime? CreatedAt { get; set; }

    /// <summary>Latest AI / workflow <c>case_answers.id</c> for this question (escalate target).</summary>
    [JsonPropertyName("answerId")]
    public Guid? AnswerId { get; set; }
    public string? AnswerText { get; set; }
    public string? StructuredDiagnosis { get; set; }
    public List<string>? DifferentialDiagnoses { get; set; }
    public string? KeyImagingFindings { get; set; }
    public string? AnswerStatus { get; set; }
    public double? AiConfidenceScore { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool IsEscalated { get; set; }
    public string? EscalatedByName { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public List<LectQAMessageDto> Messages { get; set; } = new();
}

public class LectQAMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Coordinates { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RespondToQuestionRequestDto
{
    public string AnswerText { get; set; } = string.Empty;
    public string? StructuredDiagnosis { get; set; }
    public List<string>? DifferentialDiagnoses { get; set; }
    public bool Approve { get; set; } = false;
}

public class LecturerAnswerDto
{
    public Guid AnswerId { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public string? StructuredDiagnosis { get; set; }
    public List<string>? DifferentialDiagnoses { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Student learning progress summary per class.</summary>
public class ClassStudentProgressDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentEmail { get; set; }
    public string? StudentCode { get; set; }
    public int TotalCasesViewed { get; set; }
    public int TotalQuestionsAsked { get; set; }
    public double? AvgQuizScore { get; set; }
    public int QuizAttempts { get; set; }
    public int EscalatedAnswers { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public class EscalateAnswerRequestDto
{
    public string? ReviewNote { get; set; }
}

public class RejectAnswerRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

public class EscalatedAnswerDto
{
    public Guid AnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string? CurrentAnswerText { get; set; }
    public string? StructuredDiagnosis { get; set; }
    public List<string>? DifferentialDiagnoses { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? EscalatedById { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public double? AiConfidenceScore { get; set; }
    public Guid? ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
}
