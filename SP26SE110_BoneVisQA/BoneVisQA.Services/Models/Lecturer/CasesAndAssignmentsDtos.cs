using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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

    /// <summary>Latest <c>case_answers.id</c> for this question (escalation / reviews).</summary>
    [JsonPropertyName("answerId")]
    public Guid? AnswerId { get; set; }

    /// <summary><c>CaseQA</c> (student_questions) vs <c>VisualQA</c> (visual_qa_sessions) when <c>source=all</c> or Visual QA branch.</summary>
    public string? QuestionSource { get; set; }

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
    public bool AllowLate { get; set; }
    public bool ShowResultsAfterSubmission { get; set; } = true;
    /// <summary>Nếu true, sử dụng thời gian mở/đóng từ Expert thay vì thời gian của Lecturer.</summary>
    public bool UseExpertTime { get; set; }
}

/// <summary>
/// Request để tạo Assignment THỦ CÔNG (không auto gửi email notification).
/// Dùng khi Lecturer muốn tạo assignment card trước rồi gửi thông báo sau.
/// </summary>
public class CreateAssignmentManualRequestDto
{
    /// <summary>"case" hoặc "quiz"</summary>
    public string AssignmentType { get; set; } = string.Empty;
    
    /// <summary>Danh sách lớp học được gán assignment này</summary>
    public List<Guid> ClassIds { get; set; } = new();
    
    /// <summary>ID của Case (nếu type = "case")</summary>
    public Guid? CaseId { get; set; }
    
    /// <summary>ID của Quiz (nếu type = "quiz")</summary>
    public Guid? QuizId { get; set; }
    
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int? PassingScore { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool AllowRetake { get; set; }
    public bool AllowLate { get; set; }
    public bool ShowResultsAfterSubmission { get; set; } = true;
    public bool UseExpertTime { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsMandatory { get; set; } = true;
    
    /// <summary>
    /// Nếu true, gửi email notification cho sinh viên ngay lập tức.
    /// Nếu false, chỉ tạo assignment card mà không gửi email.
    /// </summary>
    public bool SendNotification { get; set; } = false;
}

/// <summary>
/// Response trả về sau khi tạo assignment thủ công
/// </summary>
public class CreateAssignmentManualResponseDto
{
    public List<ManualAssignmentResultDto> Results { get; set; } = new();
}

public class ManualAssignmentResultDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid AssignmentId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
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
    public bool AllowLate { get; set; }
    public bool ShowResultsAfterSubmission { get; set; }
    public DateTime? RetakeResetAt { get; set; }
    /// <summary>Cảnh báo nếu thời gian của Lecturer vượt khoảng thời gian của Expert.</summary>
    public string? Warning { get; set; }
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
    public string? EssayAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public Guid AnswerId { get; set; }
    public int MaxScore { get; set; } = 10;
    public decimal? ScoreAwarded { get; set; }
    public string? LecturerFeedback { get; set; }
    public bool IsGraded { get; set; } = false;
    public string? ReferenceAnswer { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>Request chỉnh sửa điểm / câu trả lời của một quiz attempt.</summary>
public class UpdateQuizAttemptRequestDto
{
    /// <summary>Điểm mới (null = giữ nguyên).</summary>
    public double? Score { get; set; }

    /// <summary>Danh sách câu trả lời cần cập nhật.</summary>
    public List<UpdateAnswerDto> Answers { get; set; } = new();
}

/// <summary>Cap nhat 1 cau tra loi cu the.</summary>
public class UpdateAnswerDto
{
    public Guid AnswerId { get; set; }
    public string? StudentAnswer { get; set; }
    public string? EssayAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal? ScoreAwarded { get; set; }
    public string? LecturerFeedback { get; set; }
    public bool? IsGraded { get; set; }
}

// ── Assignment CRUD DTOs ───────────────────────────────────────────────────────

/// <summary>Chi tiết đầy đủ của một assignment (case hoặc quiz).</summary>
public class AssignmentDetailDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    /// <summary>"case" hoặc "quiz"</summary>
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? OpenDate { get; set; }
    public bool IsMandatory { get; set; }
    public DateTime? AssignedAt { get; set; }
    public int TotalStudents { get; set; }
    public int SubmittedCount { get; set; }
    public int GradedCount { get; set; }
    public int? MaxScore { get; set; }
    public int? PassingScore { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public bool AllowLate { get; set; }
    public bool AllowRetake { get; set; }
    public bool ShowResultsAfterSubmission { get; set; }
    public double? AvgScore { get; set; }
    public DateTime? CreatedAt { get; set; }
    /// <summary>Cảnh báo nếu thời gian của Lecturer vượt khoảng thời gian của Expert.</summary>
    public string? Warning { get; set; }
}

/// <summary>Yêu cầu cập nhật thông tin assignment.</summary>
public class UpdateAssignmentRequestDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? OpenDate { get; set; }
    public bool? IsMandatory { get; set; }
    public int? MaxScore { get; set; }
    public int? PassingScore { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public bool? AllowLate { get; set; }
    public bool? AllowRetake { get; set; }
    public bool? ShowResultsAfterSubmission { get; set; }
    /// <summary>Gửi email thông báo cập nhật cho sinh viên.</summary>
    public bool SendEmailUpdate { get; set; } = true;
    /// <summary>Nếu true, sử dụng thời gian mở/đóng từ Expert thay vì thời gian của Lecturer.</summary>
    public bool UseExpertTime { get; set; }
}

/// <summary>Thông tin submission của một sinh viên.</summary>
public class AssignmentSubmissionDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public double? Score { get; set; }
    /// <summary>"graded", "pending", "not-submitted"</summary>
    public string Status { get; set; } = "not-submitted";
}

/// <summary>Cập nhật điểm cho một submission.</summary>
public class UpdateSubmissionScoreDto
{
    public Guid StudentId { get; set; }
    public double? Score { get; set; }
}

/// <summary>Yêu cầu cập nhật điểm cho nhiều submissions.</summary>
public class UpdateSubmissionsRequestDto
{
    public List<UpdateSubmissionScoreDto> Submissions { get; set; } = new();
}
