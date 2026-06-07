using System.Text.Json;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.AspNetCore.Http;

namespace BoneVisQA.Services.Helpers;

/// <summary>Stages DICOM study archives and invokes Python <c>POST /ingest</c>.</summary>
public static class StudyArchiveIngestHelper
{
    public const long StudyArchiveMaxBytes = 209715200; // 200 MB — keep in sync with Kestrel limits

    private static readonly string[] AllowedExtensions = [".zip", ".rar"];

    public const string MissingArchiveMessage =
        "DICOM study archive is required. Send multipart/form-data with field "
        + "\"file\" (preferred), or \"dicomFile\", \"archive\", \"dicomArchive\", or \"studyArchive\" "
        + "containing a .zip or .rar file.";

    public static string? ValidateArchive(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return MissingArchiveMessage;

        if (file.Length > StudyArchiveMaxBytes)
            return $"File size exceeds {StudyArchiveMaxBytes / 1048576} MB limit.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return "Only .zip or .rar study archives are allowed.";

        return null;
    }

    /// <summary>Resolve archive from direct action parameters or nested form model.</summary>
    public static IFormFile? ResolveStudyArchive(
        IFormFile? file,
        IFormFile? dicomFile,
        IFormFile? archive,
        IFormFile? dicomArchive,
        IFormFile? studyArchive,
        ExpertDicomStudyUploadForm? form)
    {
        IFormFile? FirstNonEmpty(params IFormFile?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (candidate is { Length: > 0 })
                    return candidate;
            }

            return null;
        }

        return FirstNonEmpty(file, dicomFile, archive, dicomArchive, studyArchive)
               ?? form?.ResolveFile();
    }

    public static async Task<string> StageArchiveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var stagingRoot = Path.Combine(Path.GetTempPath(), "BoneVisQA", "study-ingest-staging");
        Directory.CreateDirectory(stagingRoot);

        var filePath = Path.Combine(stagingRoot, $"{Guid.NewGuid():N}{extension}");
        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return Path.GetFullPath(filePath);
    }

    public static void TryDeleteStagedFile(string? absoluteArchivePath)
    {
        if (string.IsNullOrWhiteSpace(absoluteArchivePath))
            return;

        try
        {
            if (File.Exists(absoluteArchivePath))
                File.Delete(absoluteArchivePath);
        }
        catch
        {
            // Best-effort cleanup; do not fail the HTTP response.
        }
    }

    public static async Task<IngestResultDto> IngestStagedArchiveAsync(
        IPythonAiConnectorService pythonAi,
        ISupabaseStorageService storage,
        string absoluteArchivePath,
        string ingestPurpose,
        Guid? ownerUserId,
        string? diagnosisText,
        CancellationToken cancellationToken)
    {
        var purpose = string.IsNullOrWhiteSpace(ingestPurpose)
            ? "library"
            : ingestPurpose.Trim().ToLowerInvariant();
        var extension = Path.GetExtension(absoluteArchivePath).ToLowerInvariant();
        var archiveId = Guid.NewGuid().ToString("N");

        var bucket = purpose == "personal" ? "student_uploads" : "medical-cases";
        var objectPath = purpose == "personal" && ownerUserId.HasValue
            ? $"ingest-staging/{ownerUserId.Value:N}/{archiveId}{extension}"
            : $"ingest-staging/{archiveId}{extension}";

        var contentType = extension switch
        {
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            _ => "application/octet-stream",
        };

        string? stagingObjectPath = null;
        try
        {
            var publicUrl = await storage.UploadLocalFileAsync(
                absoluteArchivePath,
                bucket,
                objectPath,
                contentType,
                cancellationToken);
            stagingObjectPath = objectPath;

            // Python AI runs on a separate host (Railway); pass a downloadable URL, not a Render-local path.
            var ingestReference = await storage.CreateSignedUrlAsync(
                $"{bucket}/{objectPath}",
                duration: 3600,
                cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(ingestReference))
                ingestReference = publicUrl;

            return await pythonAi.TriggerIngestAsync(
                ingestReference,
                diagnosis: diagnosisText ?? string.Empty,
                ingestPurpose: purpose,
                ownerUserId: ownerUserId,
                cancellationToken: cancellationToken);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(stagingObjectPath))
            {
                try
                {
                    await storage.DeleteFileAsync(bucket, stagingObjectPath, cancellationToken);
                }
                catch
                {
                    // Best-effort cleanup; ingest outcome must not depend on delete success.
                }
            }
        }
    }

    /// <summary>True when Python rejected the archive (client error, not gateway outage).</summary>
    public static bool IsClientIngestFailure(IngestResultDto ingest) =>
        ingest.StatusCode is 400 or 422;

    /// <summary>User-facing message for ingest failures (parses FastAPI <c>detail</c> when present).</summary>
    public static string ResolveIngestErrorMessage(IngestResultDto ingest)
    {
        var raw = ingest.ErrorMessage?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return "Invalid DICOM archive. The file may be corrupt or contain no readable DICOM images.";

        if (TryParseFastApiDetail(raw, out var detail))
            return MapPythonIngestDetail(detail);

        if (raw.Contains("no DICOM", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("not a valid ZIP", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("not a valid RAR", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("zip slip", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("BadZipFile", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("corrupt", StringComparison.OrdinalIgnoreCase))
            return "Invalid DICOM archive. The file may be corrupt or contain no readable DICOM images.";

        return raw.Length > 400 ? raw[..400] + "…" : raw;
    }

    private static bool TryParseFastApiDetail(string body, out string detail)
    {
        detail = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var d))
            {
                detail = d.ValueKind == JsonValueKind.String
                    ? d.GetString() ?? string.Empty
                    : d.ToString();
                return !string.IsNullOrWhiteSpace(detail);
            }
        }
        catch
        {
            // Plain-text error body from Python.
        }

        return false;
    }

    private static string MapPythonIngestDetail(string detail)
    {
        if (detail.Contains("no DICOM", StringComparison.OrdinalIgnoreCase))
            return "No valid DICOM images were found in this archive.";
        if (detail.Contains("not a valid ZIP", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("BadZipFile", StringComparison.OrdinalIgnoreCase))
            return "The file is not a valid ZIP archive.";
        if (detail.Contains("not a valid RAR", StringComparison.OrdinalIgnoreCase))
            return "The file is not a valid RAR archive.";
        if (detail.Contains("archive not found", StringComparison.OrdinalIgnoreCase))
            return "Study archive could not be read after upload.";
        return detail;
    }
}
