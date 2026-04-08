using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Search;

public class GlobalSearchResponseDto
{
    public List<GlobalSearchCaseItemDto> Cases { get; set; } = new();
    public List<GlobalSearchQuizItemDto> Quizzes { get; set; } = new();
    public List<GlobalSearchClassItemDto> Classes { get; set; } = new();
    public List<GlobalSearchUserItemDto> Users { get; set; } = new();
    public List<GlobalSearchDocumentItemDto> Documents { get; set; } = new();
    public List<GlobalSearchEscalatedQuestionItemDto> EscalatedQuestions { get; set; } = new();
}

public class GlobalSearchCaseItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class GlobalSearchQuizItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
}

public class GlobalSearchClassItemDto
{
    public Guid Id { get; set; }
    public string ClassName { get; set; } = string.Empty;
}

public class GlobalSearchUserItemDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class GlobalSearchDocumentItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? IndexingStatus { get; set; }
}

public class GlobalSearchEscalatedQuestionItemDto
{
    public Guid AnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? CurrentAnswerText { get; set; }
    public DateTime? EscalatedAt { get; set; }
}
