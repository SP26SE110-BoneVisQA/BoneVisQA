using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Student;

public class QuizListItemDto
{
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }
    /// <summary>Số câu trong quiz (quiz_questions) — dùng cho màn hình student trước khi bắt đầu.</summary>
    public int TotalQuestions { get; set; }
    public bool IsCompleted { get; set; }
    public double? Score { get; set; }
    /// <summary>Attempt ID của lần làm gần nhất — dùng để review.</summary>
    public Guid? AttemptId { get; set; }
    /// <summary>Thời gian tạo quiz — dùng để sắp xếp theo quiz mới nhất.</summary>
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// Kết quả trả về từ repository: quiz + session + class (dùng cho danh sách quiz của student).
/// </summary>
public class QuizWithSessionDto
{
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }
}

public class StudentQuizQuestionDto
{
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Type { get; set; }
    public Guid? CaseId { get; set; }
    /// <summary>Tên case (nếu câu hỏi gắn case) — hiển thị trên UI student.</summary>
    public string? CaseTitle { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? ImageUrl { get; set; }
    public int MaxScore { get; set; } = 10;
    public string? ReferenceAnswer { get; set; } // For essay, shown only after grading if configured
}

public class QuizSessionDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
    /// <summary>Thời giới hạn làm bài (phút), từ quizzes.time_limit — FE dùng cho đồng hồ đếm ngược.</summary>
    public int? TimeLimit { get; set; }
    /// <summary>Thời gian đóng quiz — FE dùng để auto submit khi đến giờ đóng.</summary>
    public DateTime? CloseTime { get; set; }
    public IReadOnlyList<StudentQuizQuestionDto> Questions { get; set; } = Array.Empty<StudentQuizQuestionDto>();
}

public class SubmitQuizQuestionAnswerDto
{
    public Guid QuestionId { get; set; }
    public string? StudentAnswer { get; set; } // For MC/TF
    public string? EssayAnswer { get; set; }   // For essay
}

public class SubmitQuizRequestDto
{
    public Guid AttemptId { get; set; }
    public IReadOnlyList<SubmitQuizQuestionAnswerDto> Answers { get; set; } = Array.Empty<SubmitQuizQuestionAnswerDto>();
}

public class QuizResultDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public double? Score { get; set; }
    public int? PassingScore { get; set; }
    public bool Passed { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    /// <summary>
    /// Số câu essay chưa được giảng viên chấm.
    /// Nếu > 0, điểm hiện tại là điểm tạm và có thể thay đổi sau khi giảng viên chấm.
    /// </summary>
    public int UngradedEssayCount { get; set; }
}

public class StudentProgressDto
{
    /// <summary>Số lần xem case (case_view_logs).</summary>
    public int TotalCasesViewed { get; set; }

    /// <summary>Số câu hỏi đã gửi trong ngữ cảnh case / RAG (student_questions).</summary>
    public int TotalQuestionsAsked { get; set; }

    /// <summary>Số quiz đã hoàn thành (quiz_attempts có điểm hoặc completed_at).</summary>
    public int QuizzesCompleted { get; set; }

    /// <summary>Số câu hỏi trắc nghiệm trong quiz đã trả lời (student_quiz_answers).</summary>
    public int TotalQuizAnswersSubmitted { get; set; }

    /// <summary>Điểm trung bình các quiz đã chấm điểm.</summary>
    public double? AvgQuizScore { get; set; }
    public int TotalQuizAttempts { get; set; }
    public int CompletedQuizzes { get; set; }
    public int EscalatedAnswers { get; set; }
    public double? LatestQuizScore { get; set; }
    public double? QuizAccuracyRate { get; set; }
}

public class StudentTopicStatDto
{
    public string Topic { get; set; } = string.Empty;
    public int QuizAttempts { get; set; }
    public int QuestionsAsked { get; set; }
    public double? AverageQuizScore { get; set; }
    public double? AccuracyRate { get; set; }
    public int TotalInteractions { get; set; }
}

public class StudentRecentActivityDto
{
    /// <summary>Stable token for UI routing (e.g. <c>visual_qa</c> for Visual QA timeline rows).</summary>
    public string ActivityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public DateTime OccurredAt { get; set; }
    /// <summary>Visual QA session id when <see cref="ActivityType"/> is visual QA.</summary>
    public Guid? SessionId { get; set; }
    /// <summary>Optional deep link (relative). FE may derive from <see cref="SessionId"/> when null.</summary>
    public string? TargetUrl { get; set; }
}

/// <summary>
/// Trả về session quiz AI sau khi lưu vào DB — student có thể bắt đầu làm ngay.
/// </summary>
public class StudentGeneratedQuizAttemptDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public IReadOnlyList<StudentQuizQuestionDto> Questions { get; set; } = Array.Empty<StudentQuizQuestionDto>();
    public bool SavedToHistory { get; set; }
}

/// <summary>
/// Một lần làm quiz trong lịch sử — dùng cho trang Quiz History.
/// </summary>
public class StudentQuizAttemptSummaryDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public string? Difficulty { get; set; }
    public string? ClassName { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? Score { get; set; }
    public int? PassingScore { get; set; }
    public bool Passed { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public bool IsAiGenerated { get; set; }
}

/// <summary>
/// Chi tiết đáp án của một quiz attempt — dùng để hiển thị review sau khi nộp.
/// </summary>
public class QuizAttemptReviewDto
{
    public Guid AttemptId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public double? Score { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public bool Passed { get; set; }
    public int? PassingScore { get; set; }
    public IReadOnlyList<QuestionReviewItemDto> Questions { get; set; } = Array.Empty<QuestionReviewItemDto>();
}

public class QuestionReviewItemDto
{
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? StudentAnswer { get; set; }
    public string? EssayAnswer { get; set; }
    public string? CorrectAnswer { get; set; }
    public bool IsCorrect { get; set; }
    public string? ImageUrl { get; set; }
    public string? CaseId { get; set; }
    public decimal? ScoreAwarded { get; set; }
    public string? LecturerFeedback { get; set; }
    public bool IsGraded { get; set; }
    public int MaxScore { get; set; } = 10;
}
