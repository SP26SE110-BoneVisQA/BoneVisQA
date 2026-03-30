using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Models.VisualQA;
using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace BoneVisQA.API.Controllers;

public class VisualQAFileUploadRequest
{
    [DefaultValue("Nguyên nhân gây ra thoái hóa khớp là gì?")]
    public string QuestionText { get; set; } = string.Empty;
    public IFormFile? CustomImage { get; set; }

    [DefaultValue(null)]
    public string? Coordinates { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VisualQAController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IVisualQaAiService _visualQaAiService;
    private readonly ISupabaseStorageService _storageService;
    private readonly IConfiguration _configuration;

    public VisualQAController(
        IStudentService studentService,
        IVisualQaAiService visualQaAiService,
        ISupabaseStorageService storageService,
        IConfiguration configuration)
    {
        _studentService = studentService;
        _visualQaAiService = visualQaAiService;
        _storageService = storageService;
        _configuration = configuration;
    }

    [HttpPost("ask")]
    [RequestSizeLimit(52428800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52428800)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<VisualQAResponseDto>> Ask([FromForm] VisualQAFileUploadRequest formRequest, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        if (string.IsNullOrWhiteSpace(formRequest.QuestionText))
            return BadRequest(new { message = "QuestionText is required." });

        if (formRequest.CustomImage == null || formRequest.CustomImage.Length == 0)
            return BadRequest(new { message = "File không được để trống." });
        if (formRequest.CustomImage.Length > 52428800)
            return BadRequest(new { message = "File quá lớn. Dung lượng tối đa là 50MB." });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(formRequest.CustomImage.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Only JPG, PNG, and WebP images are allowed." });

        // Stream upload only (OpenReadStream); no FileStream to disk / project directory — avoids Hot Reload FileSystemWatcher crashes.
        var imageUrl = await _storageService.UploadFileAsync(
            formRequest.CustomImage,
            "student_uploads",
            $"images/{studentId}");

        var request = new VisualQARequestDto
        {
            QuestionText = formRequest.QuestionText,
            ImageUrl = imageUrl,
            Coordinates = formRequest.Coordinates,
            CaseId = null,
            AnnotationId = null
        };

        StudentQuestionDto question;
        try
        {
            question = await _studentService.CreateVisualQAQuestionAsync(studentId, request);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        var response = await _visualQaAiService.RunPipelineAsync(request, cancellationToken);
        try
        {
            await _studentService.SaveVisualQAAnswerAsync(question.Id, response);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }

        return Ok(response);
    }

    [HttpPost("ask-json")]
    public async Task<ActionResult<VisualQAResponseDto>> AskJson([FromBody] VisualQARequestDto request, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        if (string.IsNullOrWhiteSpace(request.QuestionText))
            return BadRequest(new { message = "QuestionText is required." });

        if (!string.IsNullOrWhiteSpace(request.ImageUrl)
            && !IsSupabaseHostedImageUrl(request.ImageUrl, _configuration["Supabase:Url"]))
        {
            return BadRequest(new { message = "Chỉ hỗ trợ phân tích hình ảnh được lưu trữ trên hệ thống." });
        }

        StudentQuestionDto question;
        try
        {
            question = await _studentService.CreateVisualQAQuestionAsync(studentId, request);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        var response = await _visualQaAiService.RunPipelineAsync(request, cancellationToken);
        try
        {
            await _studentService.SaveVisualQAAnswerAsync(question.Id, response);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }

        return Ok(response);
    }

    /// <summary>
    /// SSRF guard: only allow image URLs on the configured Supabase host (same scheme + host as Supabase:Url).
    /// </summary>
    private static bool IsSupabaseHostedImageUrl(string imageUrl, string? configuredSupabaseUrl)
    {
        if (string.IsNullOrWhiteSpace(configuredSupabaseUrl))
            return false;

        var trimmedUrl = imageUrl.Trim();
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var imgUri))
            return false;

        var baseTrim = configuredSupabaseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(baseTrim, UriKind.Absolute, out var baseUri))
            return false;

        return string.Equals(imgUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(imgUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase);
    }
}
