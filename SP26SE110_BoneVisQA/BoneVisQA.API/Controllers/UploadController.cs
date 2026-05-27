using Microsoft.AspNetCore.Mvc;
using BoneVisQA.Services.Interfaces;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/upload")]
[Tags("Upload")]
public class UploadController : ControllerBase
{
    private readonly ISupabaseStorageService _storageService;
    private const string ImagesBucket = "medical-images";
    private const string DicomBucket = "medical-images"; // Dùng chung bucket

    public UploadController(ISupabaseStorageService storageService)
    {
        _storageService = storageService;
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

        // Upload to Supabase Storage
        var fileName = $"general/{Guid.NewGuid()}{extension}";
        var url = await _storageService.UploadFileToPathAsync(file, ImagesBucket, fileName);

        return Ok(new ImageUploadResponse { Url = url });
    }

    /// <summary>Upload DICOM/medical images for quiz questions. Accepts DICOM, JPEG, PNG.</summary>
    [HttpPost("dicom")]
    [RequestSizeLimit(20971520)]
    [RequestFormLimits(MultipartBodyLengthLimit = 20971520)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImageUploadResponse>> UploadDicom(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (file.Length > 20971520)
            return BadRequest(new { message = "File size exceeds 20MB limit." });

        var allowedExtensions = new[] { ".dcm", ".dicom", ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Only DICOM, JPG, PNG files are allowed." });

        // Upload to Supabase Storage
        var fileName = $"dicom/{Guid.NewGuid()}{extension}";
        var url = await _storageService.UploadFileToPathAsync(file, DicomBucket, fileName);

        return Ok(new ImageUploadResponse { Url = url });
    }
}

public class ImageUploadResponse
{
    public string Url { get; set; } = string.Empty;
}