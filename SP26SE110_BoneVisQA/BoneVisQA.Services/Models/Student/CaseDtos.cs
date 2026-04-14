using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Student;

public class CaseListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Difficulty { get; set; }
    public string? CategoryName { get; set; }
    public string? ThumbnailImageUrl { get; set; }
    public bool IsApproved { get; set; }
    public List<string>? Tags { get; set; }
}

public class MedicalImageDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Modality { get; set; }
}

public class CaseDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Difficulty { get; set; }
    public string? CategoryName { get; set; }
    public string? ExpertSummary { get; set; }
    public string? KeyFindings { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public bool IsApproved { get; set; }
    public IReadOnlyList<MedicalImageDto> Images { get; set; } = Array.Empty<MedicalImageDto>();
}

public class StudentCaseHistoryItemDto
{
    public Guid CaseId { get; set; }
    public string CaseTitle { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? Difficulty { get; set; }
    public DateTime LastInteractedAt { get; set; }
    public string InteractionType { get; set; } = string.Empty;
    public string? LatestQuestionText { get; set; }
    public string? LatestAnswerStatus { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
