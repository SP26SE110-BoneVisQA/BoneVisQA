using System;

namespace BoneVisQA.Services.Models.Lecturer;

/// <summary>Row returned by GET /api/lecturer/triage for the QA Triage workbench.</summary>
public class LecturerTriageRowDto
{
    public Guid AnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentEmail { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string? CaseTitle { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? AnswerText { get; set; }
    public string Status { get; set; } = "Pending";
    public double? AiConfidenceScore { get; set; }
    public DateTime? AskedAt { get; set; }
    public bool IsEscalated { get; set; }
    public string? EscalatedByName { get; set; }
    public DateTime? EscalatedAt { get; set; }
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
    public string? CaseDifficulty { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Language { get; set; }
    public DateTime? CreatedAt { get; set; }
    public Guid? AnswerId { get; set; }
    public string? AnswerText { get; set; }
    public string? StructuredDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    public string? AnswerStatus { get; set; }
    public double? AiConfidenceScore { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool IsEscalated { get; set; }
    public string? EscalatedByName { get; set; }
    public DateTime? EscalatedAt { get; set; }
}

public class RespondToQuestionRequestDto
{
    public string AnswerText { get; set; } = string.Empty;
    public string? StructuredDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    public bool Approve { get; set; } = false;
}

public class LecturerAnswerDto
{
    public Guid AnswerId { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public string? StructuredDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
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
    public string? DifferentialDiagnoses { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? EscalatedById { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public double? AiConfidenceScore { get; set; }
    public Guid? ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
}
