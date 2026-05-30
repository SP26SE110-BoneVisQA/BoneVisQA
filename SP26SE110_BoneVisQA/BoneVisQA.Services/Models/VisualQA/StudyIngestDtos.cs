using System.Text.Json;

namespace BoneVisQA.Services.Models.VisualQA;

public sealed class ExpertDicomStudyUploadResponse
{
    public Guid CaseId { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? CatalogImageId { get; set; }
    public string PreviewImageUrl { get; set; } = string.Empty;
    /// <summary>DICOM tags extracted at ingest (Modality, BodyPartExamined, PatientAge, etc.) for FE auto-fill.</summary>
    public JsonElement? DicomMetadata { get; set; }
    public bool IngestOk { get; set; }
    public string? IngestError { get; set; }
}

public sealed class StudentPersonalStudyUploadResponse
{
    public Guid SessionId { get; set; }
    public Guid CaseId { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? CatalogImageId { get; set; }
    public string PreviewImageUrl { get; set; } = string.Empty;
    /// <summary>DICOM tags extracted at ingest for FE auto-fill and subsequent <c>ask-json</c> context.</summary>
    public JsonElement? DicomMetadata { get; set; }
    public bool IngestOk { get; set; }
    public string? IngestError { get; set; }
}
