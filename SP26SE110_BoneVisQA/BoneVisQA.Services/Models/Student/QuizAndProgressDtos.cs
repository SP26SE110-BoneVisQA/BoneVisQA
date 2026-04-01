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
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
}

public class QuizSessionDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
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
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
}

public class StudentProgressDto
{
    public int TotalCasesViewed { get; set; }
    public int TotalQuestionsAsked { get; set; }
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
    public string ActivityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public DateTime OccurredAt { get; set; }
}
