using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Lecturer;

// ── Announcement DTOs ──────────────────────────────────────────────────────────

public class AnnouncementDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool SendEmail { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
}

public class CreateAnnouncementRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool SendEmail { get; set; } = true;
}

public class UpdateAnnouncementRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    /// <summary>Nếu true, gửi lại email cho sinh viên trong lớp (giống lúc tạo mới).</summary>
    public bool SendEmail { get; set; }
}

// ── Case DTOs ─────────────────────────────────────────────────────────────────

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
    public string? Language { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? AnswerText { get; set; }
    public string? AnswerStatus { get; set; }
    public Guid? EscalatedById { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public double? AiConfidenceScore { get; set; }
}

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

/// <summary>1 assignment entry gộp case + quiz assignment để FE dùng chung card.</summary>
public class ClassAssignmentDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    /// <summary>"case" hoặc "quiz"</summary>
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public bool IsMandatory { get; set; }
    public DateTime? AssignedAt { get; set; }
    /// <summary>Tổng sv đã enroll trong lớp</summary>
    public int TotalStudents { get; set; }
    /// <summary>Số sv đã xem case / đã nộp quiz</summary>
    public int SubmittedCount { get; set; }
    /// <summary>Số sv đã được chấm điểm (quiz có score)</summary>
    public int GradedCount { get; set; }
}
