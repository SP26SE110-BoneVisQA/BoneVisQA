using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Lecturer;

public class CaseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Difficulty { get; set; }
    public string? CategoryName { get; set; }
    public bool IsApproved { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AssignCasesToClassRequestDto
{
    public List<Guid> CaseIds { get; set; } = new List<Guid>();
}

public class ApproveCaseRequestDto
{
    public bool IsApproved { get; set; }
}

public class LectStudentQuestionDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public Guid CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string? AnswerText { get; set; }
    public string? AnswerStatus { get; set; }
}
