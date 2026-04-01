using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Lecturer;

public class AssignCasesRequestDto
{
    public List<Guid> CaseIds { get; set; } = new();
    public DateTime? DueDate { get; set; }
    public bool IsMandatory { get; set; }
}

public class AssignQuizSessionRequestDto
{
    public Guid QuizId { get; set; }
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int? PassingScore { get; set; }
}

public class ClassCaseAssignmentDto
{
    public Guid ClassId { get; set; }
    public Guid CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public DateTime? AssignedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsMandatory { get; set; }
}

public class ClassQuizSessionDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int? PassingScore { get; set; }
    public DateTime? CreatedAt { get; set; }
}
