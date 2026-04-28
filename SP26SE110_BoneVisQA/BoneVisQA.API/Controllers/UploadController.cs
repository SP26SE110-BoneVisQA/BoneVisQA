using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/upload")]
[Tags("Upload")]
public class UploadController : ControllerBase
{
    private readonly IPythonAiConnectorService _pythonAiConnector;

    public UploadController(IPythonAiConnectorService pythonAiConnector)
    {
        _pythonAiConnector = pythonAiConnector;
    }

    [HttpPost("image")]
    [RequestSizeLimit(10485760)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImageUploadResponse>> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (file.Length > 10485760)
            return BadRequest(new { message = "File size exceeds 10MB limit." });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Only JPG, PNG, GIF, WEBP, SVG files are allowed." });

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "images");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"/uploads/images/{fileName}";
        return Ok(new ImageUploadResponse { Url = url });
    }

    /// <summary>Upload DICOM/medical images for quiz questions. Accepts DICOM, JPEG, PNG.</summary>
    [HttpPost("dicom")]
    [RequestSizeLimit(20971520)]
    [RequestFormLimits(MultipartBodyLengthLimit = 20971520)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImageUploadResponse>> UploadDicom(
        IFormFile file,
        [FromForm] string? diagnosisText = null,
        [FromForm] string? chandoanPath = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        _ = chandoanPath; // reserved for future Python chandoan tab lookup; TriggerIngest uses DICOM + diagnosis only today.

        if (file.Length > 20971520)
            return BadRequest(new { message = "File size exceeds 20MB limit." });

        var allowedExtensions = new[] { ".dcm", ".dicom", ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Only DICOM, JPG, PNG files are allowed." });

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "dicom");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var absolutePath = Path.GetFullPath(filePath);
        var ingestOk = await _pythonAiConnector.TriggerIngestAsync(
            absolutePath,
            patientId: string.Empty,
            diagnosis: diagnosisText ?? string.Empty,
            cancellationToken);

        var url = $"/uploads/dicom/{fileName}";
        return Ok(new ImageUploadResponse
        {
            Url = url,
            AiIngestOk = ingestOk,
            AiIngestStatus = ingestOk ? 200 : 502,
            AiIngestBody = null,
            AiIngestError = ingestOk ? null : "Python AI ingest failed or returned a non-success status.",
        });
    }
}

public class ImageUploadResponse
{
    public string Url { get; set; } = string.Empty;

    /// <summary>True if Python <c>POST /ingest</c> returned 2xx.</summary>
    public bool? AiIngestOk { get; set; }

    public int? AiIngestStatus { get; set; }

    /// <summary>Raw JSON from Python (ontology + case ids), when successful.</summary>
    public string? AiIngestBody { get; set; }

    public string? AiIngestError { get; set; }
}