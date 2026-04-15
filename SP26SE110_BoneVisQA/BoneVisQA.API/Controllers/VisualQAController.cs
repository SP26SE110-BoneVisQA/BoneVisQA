using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Models.VisualQA;
using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.API.Controllers;

public class VisualQAFileUploadRequest
{
    [DefaultValue("What causes osteoarthritis?")]
    public string QuestionText { get; set; } = string.Empty;
    public IFormFile? CustomImage { get; set; }

    /// <summary>Normalized bounding box JSON <c>{"x","y","width","height"}</c> (0–1). FE field name <c>customPolygon</c> (legacy).</summary>
    [FromForm(Name = "customPolygon")]
    [DefaultValue(null)]
    public string? Coordinates { get; set; }

    [FromForm(Name = "sessionId")]
    public Guid? SessionId { get; set; }
}

[ApiController]
[Route("api/student/visual-qa")]
[Tags("Student - Visual QA")]
[Authorize(Roles = "Student")]
public class VisualQAController : ControllerBase
{
    private const long MaxVisualImageBytes = 5 * 1024 * 1024;
    private readonly IStudentService _studentService;
    private readonly IVisualQaAiService _visualQaAiService;
    private readonly ISupabaseStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VisualQAController> _logger;

    public VisualQAController(
        IStudentService studentService,
        IVisualQaAiService visualQaAiService,
        ISupabaseStorageService storageService,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<VisualQAController> logger)
    {
        _studentService = studentService;
        _visualQaAiService = visualQaAiService;
        _storageService = storageService;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    [HttpPost("ask")]
    [RequestSizeLimit(MaxVisualImageBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxVisualImageBytes)]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("AiInteractionLimit")]
    public async Task<ActionResult<VisualQAResponseDto>> Ask([FromForm] VisualQAFileUploadRequest formRequest, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        var lockKey = $"VisualQA_Ask_Lock_{studentId}";
        if (_cache.TryGetValue(lockKey, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, "Too Many Requests");
        _cache.Set(lockKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(3)
        });

        if (string.IsNullOrWhiteSpace(formRequest.QuestionText))
            return BadRequest(new { message = "QuestionText is required." });

        var isFollowUpTurn = formRequest.SessionId.HasValue && formRequest.SessionId.Value != Guid.Empty;

        if (!isFollowUpTurn && (formRequest.CustomImage == null || formRequest.CustomImage.Length == 0))
            return BadRequest(new { message = "File must not be empty." });
        if (formRequest.CustomImage != null && formRequest.CustomImage.Length > MaxVisualImageBytes)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Image exceeds the 5MB limit.",
                Instance = HttpContext.Request.Path
            });
        }

        string? imageUrl = null;
        string? uploadedBucket = null;
        string? uploadedFilePath = null;
        if (formRequest.CustomImage != null && formRequest.CustomImage.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(formRequest.CustomImage.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Only JPG, PNG, and WebP images are allowed." });

            // Stream upload only (OpenReadStream); no FileStream to disk / project directory — avoids Hot Reload FileSystemWatcher crashes.
            imageUrl = await _storageService.UploadFileAsync(
                formRequest.CustomImage,
                "student_uploads",
                $"images/{studentId}",
                cancellationToken);

            if (TryExtractSupabaseFilePointer(imageUrl, out var bucket, out var filePath))
            {
                uploadedBucket = bucket;
                uploadedFilePath = filePath;
            }
        }

        var request = new VisualQARequestDto
        {
            QuestionText = formRequest.QuestionText,
            ImageUrl = imageUrl,
            Coordinates = formRequest.Coordinates,
            SessionId = formRequest.SessionId,
            CaseId = null,
            AnnotationId = null
        };

        Guid sessionId;
        try
        {
            sessionId = await _studentService.CreateOrGetVisualQaSessionAsync(studentId, request);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        try
        {
            await _studentService.ValidateSessionStateAsync(studentId, sessionId, 3);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (string.Equals(ex.Message, "SESSION_EXPIRED", StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    errorCode = "SESSION_EXPIRED",
                    message = "The Q&A session expired due to 24 hours of inactivity."
                });
            }

            if (string.Equals(ex.Message, "SESSION_READ_ONLY", StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    errorCode = "SESSION_READ_ONLY",
                    message = "This Q&A session is view-only and cannot be continued."
                });
            }

            if (string.Equals(ex.Message, "TURN_LIMIT_EXCEEDED", StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    errorCode = "TURN_LIMIT_EXCEEDED",
                    message = "This chat session has reached the 3-question limit."
                });
            }

            return BadRequest(new { message = ex.Message });
        }
        VisualQAResponseDto response;
        try
        {
            try
            {
                response = await _visualQaAiService.RunPipelineAsync(request, cancellationToken);
                response.SessionId = sessionId;
                await _studentService.SaveVisualQAMessagesAsync(sessionId, request, response);
            }
            catch (Exception)
            {
                if (!string.IsNullOrWhiteSpace(uploadedBucket) && !string.IsNullOrWhiteSpace(uploadedFilePath))
                {
                    try
                    {
                        await _storageService.DeleteFileAsync(uploadedBucket, uploadedFilePath, cancellationToken);
                        _logger.LogWarning(
                            "Compensating transaction executed: Deleted orphaned file {Path} due to downstream failure.",
                            uploadedFilePath);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(
                            deleteEx,
                            "Compensating transaction failed to delete orphaned file {Path}.",
                            uploadedFilePath);
                    }
                }

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    errorCode = "INTERNAL_SERVER_ERROR",
                    message = "The system encountered an error while processing data. Temporary file cleanup completed; please try again."
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == "The AI system is overloaded. Please try again later."
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    errorCode = "AI_SERVICE_UNAVAILABLE",
                    message = ex.Message
                })
                : StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    errorCode = "INTERNAL_SERVER_ERROR",
                    message = ex.Message
                });
        }

        return Ok(response);
    }

    /// <summary>Lists Visual QA sessions for the current student (newest activity first).</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(PagedResultDto<VisualQaSessionHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResultDto<VisualQaSessionHistoryItemDto>>> GetHistory(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        var items = await _studentService.GetVisualQaHistoryAsync(studentId, limit, offset, cancellationToken);
        return Ok(items);
    }

    [HttpPost("ask-json")]
    [EnableRateLimiting("AiInteractionLimit")]
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
            return BadRequest(new { message = "Only analysis of images stored in the system is supported." });
        }

        Guid sessionId;
        try
        {
            sessionId = await _studentService.CreateOrGetVisualQaSessionAsync(studentId, request);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        try
        {
            await _studentService.ValidateSessionStateAsync(studentId, sessionId, 3);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (string.Equals(ex.Message, "SESSION_EXPIRED", StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    errorCode = "SESSION_EXPIRED",
                    message = "The Q&A session expired due to 24 hours of inactivity."
                });
            }

            if (string.Equals(ex.Message, "SESSION_READ_ONLY", StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    errorCode = "SESSION_READ_ONLY",
                    message = "This Q&A session is view-only and cannot be continued."
                });
            }

            if (string.Equals(ex.Message, "TURN_LIMIT_EXCEEDED", StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    errorCode = "TURN_LIMIT_EXCEEDED",
                    message = "This chat session has reached the 3-question limit."
                });
            }

            return BadRequest(new { message = ex.Message });
        }
        VisualQAResponseDto response;
        try
        {
            response = await _visualQaAiService.RunPipelineAsync(request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                errorCode = "AI_SERVICE_UNAVAILABLE",
                message = ex.Message
            });
        }
        response.SessionId = sessionId;
        try
        {
            await _studentService.SaveVisualQAMessagesAsync(sessionId, request, response);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }

        return Ok(response);
    }

    [HttpPost("{sessionId:guid}/request-review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestReview(Guid sessionId, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            await _studentService.RequestVisualQaReviewAsync(studentId, sessionId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (string.Equals(ex.Message, "SESSION_EXPIRED", StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    errorCode = "SESSION_EXPIRED",
                    message = "The Q&A session expired due to 24 hours of inactivity."
                });
            }

            return BadRequest(new { message = ex.Message });
        }
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

    private static bool TryExtractSupabaseFilePointer(string imageUrl, out string bucket, out string filePath)
    {
        bucket = string.Empty;
        filePath = string.Empty;

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            return false;

        const string marker = "/storage/v1/object/public/";
        var path = uri.AbsolutePath;
        var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return false;

        var relative = path[(markerIndex + marker.Length)..].Trim('/');
        var slash = relative.IndexOf('/');
        if (slash <= 0 || slash == relative.Length - 1)
            return false;

        bucket = relative[..slash];
        filePath = relative[(slash + 1)..];
        return !string.IsNullOrWhiteSpace(bucket) && !string.IsNullOrWhiteSpace(filePath);
    }
}
