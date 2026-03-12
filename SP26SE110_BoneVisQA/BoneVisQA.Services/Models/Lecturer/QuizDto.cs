using System;

namespace BoneVisQA.Services.Models.Lecturer;

public class QuizDto
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public string Title { get; set; } = null!;

    public DateTime? OpenTime { get; set; }

    public DateTime? CloseTime { get; set; }

    public int? TimeLimit { get; set; }

    public int? PassingScore { get; set; }

    public DateTime? CreatedAt { get; set; }
}
public class QuizQuestionDto
{
    public Guid Id { get; set; }

    public Guid QuizId { get; set; }

    public Guid? CaseId { get; set; }

    public string? CaseTitle { get; set; }

    public string QuestionText { get; set; } = null!;

    public string? Type { get; set; }

    public string? CorrectAnswer { get; set; }

    public List<string>? Options { get; set; }
}

public class CreateQuizQuestionRequestDto
{
    public Guid QuizId { get; set; }
    public Guid? CaseId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string Type { get; set; } = "multiple_choice";
    public string? CorrectAnswer { get; set; }
    public List<string>? Options { get; set; }
}

public class UpdateQuizQuestionRequestDto
{
    public string QuestionText { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? CorrectAnswer { get; set; }
    public List<string>? Options { get; set; }
}