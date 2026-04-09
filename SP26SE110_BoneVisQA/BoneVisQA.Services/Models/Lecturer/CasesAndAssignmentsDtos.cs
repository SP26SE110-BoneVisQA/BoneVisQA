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
    public bool ShuffleQuestions { get; set; }
    public bool AllowRetake { get; set; }
}

/// <summary>Yêu cầu bật retake cho một attempt cụ thể của sinh viên.</summary>
public class AllowRetakeRequestDto
{
    public Guid AttemptId { get; set; }
}

/// <summary>Yêu cầu bật retake cho toàn bộ sinh viên trong một lớp / quiz.</summary>
public class AllowRetakeAllRequestDto
{
    public Guid ClassId { get; set; }
    public Guid QuizId { get; set; }
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
    public bool ShuffleQuestions { get; set; }
    public bool AllowRetake { get; set; }
    public DateTime? RetakeResetAt { get; set; }
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

// ── Quiz Review DTOs ───────────────────────────────────────────────────────────

/// <summary>Tóm tắt 1 bài quiz của sinh viên (dùng cho danh sách).</summary>
public class StudentQuizAttemptDto
{
    public Guid AttemptId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public double? Score { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public bool IsGraded { get; set; }
}

/// <summary>Chi tiết 1 bài quiz: câu hỏi + câu trả lời sinh viên.</summary>
public class QuizAttemptDetailDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public double? Score { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? PassingScore { get; set; }
    public List<QuestionWithAnswerDto> Questions { get; set; } = new();
}

/// <summary>Câu hỏi + câu trả lời sinh viên (cho FE hiển thị + chỉnh sửa).</summary>
public class QuestionWithAnswerDto
{
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? StudentAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public Guid AnswerId { get; set; }
}

/// <summary>Request chỉnh sửa điểm / câu trả lời của một quiz attempt.</summary>
public class UpdateQuizAttemptRequestDto
{
    /// <summary>Điểm mới (null = giữ nguyên).</summary>
    public double? Score { get; set; }

    /// <summary>Danh sách câu trả lời cần cập nhật.</summary>
    public List<UpdateAnswerDto> Answers { get; set; } = new();
}

/// <summary>Cập nhật 1 câu trả lời cụ thể.</summary>
public class UpdateAnswerDto
{
    public Guid AnswerId { get; set; }
    public string? StudentAnswer { get; set; }
    public bool? IsCorrect { get; set; }
}
