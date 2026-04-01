using System;

namespace BoneVisQA.Services.Models.Student;

public class CreateAnnotationRequestDto
{
    public Guid ImageId { get; set; }
    public string Label { get; set; } = string.Empty;
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
    public string? AnswerStatus { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
