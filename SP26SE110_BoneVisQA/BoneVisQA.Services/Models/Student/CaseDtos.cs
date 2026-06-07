using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoneVisQA.Services.Models.Student;

public static class StudentCaseOriginValues
{
    /// <summary>Promoted from an escalated student Visual QA / Q&amp;A session.</summary>
    public const string CommunityPromoted = "communityPromoted";

    /// <summary>Created directly by an expert (New case or DICOM ingest).</summary>
    public const string ExpertCreated = "expertCreated";

    /// <summary>Legacy display label — kept for backward-compatible clients.</summary>
    public const string FromCommunityRequest = "From Community Request";

    /// <summary>Legacy display label — kept for backward-compatible clients.</summary>
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
    public string CaseOrigin { get; set; } = StudentCaseOriginValues.ExpertCreated;

    [JsonPropertyName("boneLocation")]
    public string? BoneLocation { get; set; }

    [JsonPropertyName("lesionType")]
    public string? LesionType { get; set; }

    [JsonPropertyName("expertName")]
    public string? ExpertName { get; set; }
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

    [JsonPropertyName("suggestedDiagnosis")]
    public string? SuggestedDiagnosis => ExpertSummary;

    public string? KeyFindings { get; set; }

    [JsonPropertyName("primaryImageUrl")]
    public string? PrimaryImageUrl { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl => PrimaryImageUrl;

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl => PrimaryImageUrl;

    /// <summary>Alias for <see cref="Images"/> (FE catalog workspace).</summary>
    [JsonPropertyName("medicalImages")]
    public IReadOnlyList<MedicalImageDto> MedicalImages => Images;

    /// <summary>First ingested DICOM study row (<c>case_media.id</c>) when present.</summary>
    [JsonPropertyName("mediaId")]
    public Guid? MediaId { get; set; }

    /// <summary>First catalog raster row (<c>medical_images.id</c>) when present.</summary>
    [JsonPropertyName("catalogImageId")]
    public Guid? CatalogImageId { get; set; }

    /// <summary>DICOM tags from <c>case_media.dicom_metadata</c> for Visual QA workspace prefill.</summary>
    [JsonPropertyName("dicomMetadata")]
    public JsonElement? DicomMetadata { get; set; }

    public bool IsApproved { get; set; }
    public IReadOnlyList<MedicalImageDto> Images { get; set; } = Array.Empty<MedicalImageDto>();

    public DateTime? CreatedAt { get; set; }

    public string CaseOrigin { get; set; } = StudentCaseOriginValues.ExpertCreated;

    [JsonPropertyName("boneLocation")]
    public string? BoneLocation { get; set; }

    [JsonPropertyName("lesionType")]
    public string? LesionType { get; set; }

    [JsonPropertyName("expertName")]
    public string? ExpertName { get; set; }

    [JsonPropertyName("reflectiveQuestions")]
    public string? ReflectiveQuestions { get; set; }

    /// <summary>Populated for <see cref="StudentCaseOriginValues.CommunityPromoted"/> cases.</summary>
    [JsonPropertyName("studentQuestion")]
    public string? StudentQuestion { get; set; }

    [JsonPropertyName("differentialDiagnoses")]
    public IReadOnlyList<string>? DifferentialDiagnoses { get; set; }

    [JsonPropertyName("referencesAndCitations")]
    public IReadOnlyList<string>? ReferencesAndCitations { get; set; }

    [JsonPropertyName("clinicalDescription")]
    public string? ClinicalDescription => Description;
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
