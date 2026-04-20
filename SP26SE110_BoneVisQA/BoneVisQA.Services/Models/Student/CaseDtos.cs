using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BoneVisQA.Services.Models.Student;

public static class StudentCaseOriginValues
{
    public const string FromCommunityRequest = "From Community Request";
    public const string CreatedByExpert = "Created by Expert";
}

public class CaseListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Difficulty { get; set; }
    public string? CategoryName { get; set; }

    [JsonPropertyName("categoryDisplay")]
    public string? CategoryDisplay => CategoryName;

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailImageUrl { get; set; }

    public bool IsApproved { get; set; }
    public List<string>? Tags { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary><see cref="StudentCaseOriginValues"/> for FE (Ask AI lockout).</summary>
    public string CaseOrigin { get; set; } = StudentCaseOriginValues.CreatedByExpert;
}

public class MedicalImageDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Modality { get; set; }

    /// <summary>Primary ROI JSON from <c>case_annotations</c> (first annotation on this image).</summary>
    [JsonPropertyName("roiBoundingBox")]
    public string? RoiBoundingBox { get; set; }
}

public class CaseDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Difficulty { get; set; }
    public string? CategoryName { get; set; }

    [JsonPropertyName("categoryDisplay")]
    public string? CategoryDisplay => CategoryName;

    public string? ExpertSummary { get; set; }
    public string? KeyFindings { get; set; }

    [JsonPropertyName("primaryImageUrl")]
    public string? PrimaryImageUrl { get; set; }

    public bool IsApproved { get; set; }
    public IReadOnlyList<MedicalImageDto> Images { get; set; } = Array.Empty<MedicalImageDto>();

    public DateTime? CreatedAt { get; set; }

    public string CaseOrigin { get; set; } = StudentCaseOriginValues.CreatedByExpert;
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

public class CaseCatalogFiltersDto
{
    public IReadOnlyList<string> Locations { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> LesionTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Difficulties { get; set; } = Array.Empty<string>();
}
