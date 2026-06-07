using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace BoneVisQA.Services.Models.VisualQA;

/// <summary>Multipart body for <c>POST /api/expert/cases/upload-dicom</c>.</summary>
public sealed class ExpertDicomStudyUploadForm
{
    public IFormFile? File { get; set; }
    public IFormFile? DicomFile { get; set; }
    public IFormFile? Archive { get; set; }
    public IFormFile? DicomArchive { get; set; }
    public IFormFile? StudyArchive { get; set; }
    public string? DiagnosisText { get; set; }

    /// <summary>First non-empty archive part (any supported field name).</summary>
    public IFormFile? ResolveFile() =>
        FirstNonEmpty(File, DicomFile, Archive, DicomArchive, StudyArchive);

    private static IFormFile? FirstNonEmpty(params IFormFile?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is { Length: > 0 })
                return candidate;
        }

        return null;
    }
}

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

/// <summary>Bootstrap payload when opening Visual QA from the published case catalog (Ask with AI).</summary>
public sealed class StudentCatalogCaseSessionBootstrapResponse
{
    public Guid SessionId { get; set; }
    public Guid CaseId { get; set; }
    public Guid? MediaId { get; set; }
    public Guid? CatalogImageId { get; set; }
    public string PreviewImageUrl { get; set; } = string.Empty;
    public JsonElement? DicomMetadata { get; set; }
}
