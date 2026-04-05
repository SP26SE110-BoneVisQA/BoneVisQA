using System;

namespace BoneVisQA.Services.Models.Lecturer;

public class QuizDto
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public string Title { get; set; } = null!;

    public string? Topic { get; set; }

    public bool IsAiGenerated { get; set; }

    public string? Difficulty { get; set; }

    public string? Classification { get; set; }

    public DateTime? OpenTime { get; set; }

    public DateTime? CloseTime { get; set; }

    public int? TimeLimit { get; set; }

    public int? PassingScore { get; set; }

    public DateTime? CreatedAt { get; set; }
}

// Update Quiz Request Dto
public class UpdateQuizRequestDto
{
    public string Title { get; set; } = string.Empty;
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }
}

public class UpdateQuizQuestionRequestDto
{
    public string QuestionText { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? CorrectAnswer { get; set; }
    public List<string>? Options { get; set; }
}

// ========== COPIED FROM Expert/QuizDTO ==========

// ClassQuizDto - For quiz-class assignment
public class ClassQuizDto
{
    public Guid ClassId { get; set; }
    public Guid QuizId { get; set; }
    public string? QuizName { get; set; }
    public string? ClassName { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int QuestionCount { get; set; }
}

// CreateQuizQuestionDto - For creating questions (with individual options)
public class CreateQuizQuestionDto
{
    public Guid QuizId { get; set; }
    public Guid? CaseId { get; set; }
    public string QuestionText { get; set; } = null!;
    public string? Type { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? ImageUrl { get; set; }
}

// UpdateQuizsQuestionRequestDto - For updating questions (expert style)
public class UpdateQuizsQuestionRequestDto
{
    public string QuestionText { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? ImageUrl { get; set; }
}

// UpdateQuizsQuestionResponseDto - Response for updating questions
public class UpdateQuizsQuestionResponseDto
{
    public string? QuizTitle { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? ImageUrl { get; set; }
}

// QuizQuestionDto - For quiz questions with individual options
public class QuizQuestionDto
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public string? QuizTitle { get; set; }
    public Guid? CaseId { get; set; }
    public string? CaseTitle { get; set; }
    public string QuestionText { get; set; } = null!;
    public string? Type { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? ImageUrl { get; set; }
}

// QuizScoreResultDto - Used for quiz score calculation
public class QuizScoreResultDto
{
    public Guid AttemptId { get; set; }
    public Guid StudentId { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = null!;
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public float Score { get; set; }
    public int? PassingScore { get; set; }
    public bool IsPassed { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// StudentSubmitQuestionDto - Used for student quiz submission
public class StudentSubmitQuestionDto
{
    public Guid StudentId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public string? StudentAnswer { get; set; }
}

// StudentSubmitQuestionResponseDto - Response for student quiz submission
public class StudentSubmitQuestionResponseDto
{
    public string? QuizTitle { get; set; }
    public string? QuestionText { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? StudentAnswer { get; set; }
    public string? StudentAnswerText { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? CorrectAnswerText { get; set; }
    public bool? IsCorrect { get; set; }
}