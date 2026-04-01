using System;

namespace BoneVisQA.Services.Models.Expert;

public class ResolveEscalatedAnswerRequestDto
{
    public string AnswerText { get; set; } = string.Empty;
    public string? StructuredDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    public string? ReviewNote { get; set; }
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
