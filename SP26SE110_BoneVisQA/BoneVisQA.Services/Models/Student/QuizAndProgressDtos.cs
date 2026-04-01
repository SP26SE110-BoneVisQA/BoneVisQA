using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Student;

public class QuizListItemDto
{
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }
    public bool IsCompleted { get; set; }
    public double? Score { get; set; }
}

public class StudentQuizQuestionDto
{
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Type { get; set; }
    public Guid? CaseId { get; set; }
}

public class QuizSessionDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public IReadOnlyList<StudentQuizQuestionDto> Questions { get; set; } = Array.Empty<StudentQuizQuestionDto>();
}

public class SubmitQuizQuestionAnswerDto
{
    public Guid QuestionId { get; set; }
    public string? StudentAnswer { get; set; }
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
}
