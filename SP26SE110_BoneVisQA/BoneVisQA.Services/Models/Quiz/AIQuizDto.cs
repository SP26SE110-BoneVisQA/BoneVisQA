using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Quiz;

/// <summary>
/// Input DTO for AI Quiz Case
/// </summary>
public class AIQuizCaseInputDto
{
    public Guid? CaseId { get; set; }
    public string? CaseTitle { get; set; }
    public string? CaseDescription { get; set; }
    public string? ImageUrl { get; set; }
    public string? Modality { get; set; }
    public string? KeyFindings { get; set; }
    public string? SuggestedDiagnosis { get; set; }
    public string? Difficulty { get; set; }
}

/// <summary>
/// Single AI-generated question
/// </summary>
public class AIQuizQuestionDto
{
    public string QuestionText { get; set; } = string.Empty;
    public string Type { get; set; } = "MultipleChoice";
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string? CaseTitle { get; set; }
    public string? ImageUrl { get; set; }
    public string? Explanation { get; set; }
}

/// <summary>
/// Result of AI Quiz Generation
/// </summary>
public class AIQuizGenerationResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<AIQuizQuestionDto> Questions { get; set; } = new();
    public string? Topic { get; set; }
    public string? Difficulty { get; set; }
}

/// <summary>
/// Request for AI Auto-Generate Quiz (Lecturer)
/// </summary>
public class AIAutoGenerateQuizRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? Difficulty { get; set; }
    public string? Classification { get; set; }
    public int QuestionCount { get; set; } = 5;
    public Guid? ClassId { get; set; }
    public Guid CreatedByLecturerId { get; set; }
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }
}

/// <summary>
/// Request for AI Suggest Questions from Cases (Lecturer)
/// </summary>
public class AISuggestQuestionsRequestDto
{
    public List<AIQuizCaseInputDto> Cases { get; set; } = new();
    public int QuestionsPerCase { get; set; } = 2;
    public string? Difficulty { get; set; }
}
