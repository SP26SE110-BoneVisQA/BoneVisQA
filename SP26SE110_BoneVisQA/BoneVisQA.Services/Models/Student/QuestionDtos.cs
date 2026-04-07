using System;
using System.Collections.Generic;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Models.Student;

public class CreateAnnotationRequestDto
{
    public Guid ImageId { get; set; }
    public string Label { get; set; } = string.Empty;
    /// <summary>Preferred: closed polygon (≥3 points). Serialized to <c>case_annotations.coordinates</c> JSON.</summary>
    public List<PointDto>? CustomPolygon { get; set; }
    /// <summary>Legacy box JSON or raw polygon JSON string when <see cref="CustomPolygon"/> is not sent.</summary>
    public string? Coordinates { get; set; }
}

public class AnnotationDto
{
    public Guid Id { get; set; }
    public Guid ImageId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Coordinates { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AskQuestionRequestDto
{
    public Guid CaseId { get; set; }
    public Guid? AnnotationId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
}

public class StudentQuestionDto
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? AnnotationId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}

public class StudentQuestionHistoryItemDto
{
    public Guid Id { get; set; }
    public Guid? CaseId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string? AnswerText { get; set; }
    public string? StructuredDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    public string? KeyImagingFindings { get; set; }
    public string? ReflectiveQuestions { get; set; }
    public string? AnswerStatus { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
