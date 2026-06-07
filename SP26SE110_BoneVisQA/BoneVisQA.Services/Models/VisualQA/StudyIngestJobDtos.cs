using System.Text.Json;

namespace BoneVisQA.Services.Models.VisualQA;

public enum StudyIngestJobKind
{
    ExpertLibrary,
    StudentPersonal,
}

public sealed class StudyIngestJobStatusDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = "processing";
    public StudyIngestJobKind Kind { get; set; }
    public bool IngestOk { get; set; }
    public string? IngestError { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? CatalogImageId { get; set; }
    public string? PreviewImageUrl { get; set; }
    public JsonElement? DicomMetadata { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class StudyIngestJobStatusResponse
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = "processing";
    public bool IngestOk { get; set; }
    public string? IngestError { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? CatalogImageId { get; set; }
    public string? PreviewImageUrl { get; set; }
    public JsonElement? DicomMetadata { get; set; }
}
