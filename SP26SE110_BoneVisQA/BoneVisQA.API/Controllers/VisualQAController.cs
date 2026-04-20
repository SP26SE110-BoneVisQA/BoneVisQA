using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Models.VisualQA;
using System.ComponentModel;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
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

    [FromForm(Name = "clientRequestId")]
    public string? ClientRequestId { get; set; }

    /// <summary>Optional override (ISO 639-1), e.g. <c>vi</c>, <c>en</c>. Resolved together with <c>Accept-Language</c> and query <c>locale</c>.</summary>
    [FromForm(Name = "language")]
    public string? Language { get; set; }
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

    /// <summary>
    /// Response language: query <c>?locale=vi|en|...</c> (highest priority), form field <c>language</c>, JSON <c>language</c> on other endpoints, or <c>Accept-Language</c>. Default Vietnamese.
    /// </summary>
    [HttpPost("ask")]
    [RequestSizeLimit(MaxVisualImageBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxVisualImageBytes)]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("AiInteractionLimit")]
    public async Task<IActionResult> Ask(
        [FromForm] VisualQAFileUploadRequest formRequest,
        [FromQuery] string? locale,
        CancellationToken cancellationToken)
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

        var (multipartPrepared, multipartError) = await TryPrepareVisualQaMultipartAskAsync(studentId, formRequest, locale, cancellationToken);
        if (multipartError != null)
            return multipartError;
        var prepared = multipartPrepared!;
        var sessionId = prepared.SessionId;
        var request = prepared.Request;
        var uploadedBucket = prepared.UploadedBucket;
        var uploadedFilePath = prepared.UploadedFilePath;
        VisualQAResponseDto response;
        if (!string.IsNullOrWhiteSpace(request.ClientRequestId))
        {
            var existing = await _studentService.GetExistingVisualQaResponseAsync(
                studentId,
                sessionId,
                request.ClientRequestId,
                cancellationToken);
            if (existing != null)
            {
                var existingCapabilities = await _studentService.GetVisualQaSessionCapabilitiesAsync(studentId, sessionId, cancellationToken: cancellationToken);
                return Ok(ToApiResponse(existing, existingCapabilities));
            }
        }
        try
        {
            try
            {
                request.SessionId = sessionId;
                response = await _visualQaAiService.RunPipelineAsync(request, cancellationToken);
                response.SessionId = sessionId;
                await _studentService.SaveVisualQAMessagesAsync(sessionId, request, response);
            }
            catch (AiResponseFormatException ex)
            {
                if (!string.IsNullOrWhiteSpace(uploadedBucket) && !string.IsNullOrWhiteSpace(uploadedFilePath))
                {
                    try
                    {
                        await _storageService.DeleteFileAsync(uploadedBucket, uploadedFilePath, cancellationToken);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "Failed to cleanup malformed-response upload for {Path}.", uploadedFilePath);
                    }
                }

                _logger.LogWarning("Visual QA Ask: AI_RESPONSE_INVALID_FORMAT: {Message}", ex.Message);
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = ex.Message
                });
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

                _logger.LogWarning("Visual QA Ask: INTERNAL_SERVER_ERROR after cleanup");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "The system encountered an error while processing data. Temporary file cleanup completed; please try again."
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == "The AI system is overloaded. Please try again later."
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = ex.Message
                })
                : StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = ex.Message
                });
        }

        var capabilities = await _studentService.GetVisualQaSessionCapabilitiesAsync(studentId, sessionId, cancellationToken: cancellationToken);
        response.UserQuestionText ??= request.QuestionText;
        return Ok(ToApiResponse(response, capabilities));
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

    [HttpGet("history/personal")]
    [ProducesResponseType(typeof(PagedResultDto<VisualQaSessionHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResultDto<VisualQaSessionHistoryItemDto>>> GetPersonalHistory(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        var items = await _studentService.GetVisualQaPersonalHistoryAsync(studentId, limit, offset, cancellationToken);
        return Ok(items);
    }

    [HttpGet("~/api/student/studies/personal")]
    [ProducesResponseType(typeof(PagedResultDto<VisualQaSessionHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResultDto<VisualQaSessionHistoryItemDto>>> GetPersonalStudiesCompatibility(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
        => await GetPersonalHistory(limit, offset, cancellationToken);

    [HttpGet("history/cases")]
    [ProducesResponseType(typeof(PagedResultDto<VisualQaSessionHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResultDto<VisualQaSessionHistoryItemDto>>> GetCaseHistory(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        var items = await _studentService.GetVisualQaCaseHistoryAsync(studentId, limit, offset, cancellationToken);
        return Ok(items);
    }

    [HttpGet("history/{sessionId:guid}")]
    [ProducesResponseType(typeof(VisualQaThreadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VisualQaThreadDto>> GetHistoryThread(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        var thread = await _studentService.GetVisualQaThreadAsync(studentId, sessionId, cancellationToken);
        if (thread == null)
            return NotFound(new { message = "Q&A session not found." });

        return Ok(thread);
    }

    /// <summary>Same language resolution as <see cref="Ask"/> (query <c>locale</c>, body <c>language</c>, <c>Accept-Language</c>).</summary>
    [HttpPost("ask-json")]
    [EnableRateLimiting("AiInteractionLimit")]
    public async Task<ActionResult<VisualQaApiResponseDto>> AskJson(
        [FromBody] VisualQARequestDto request,
        [FromQuery] string? locale,
        CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        if (string.IsNullOrWhiteSpace(request.QuestionText))
            return BadRequest(BuildInputValidationErrorResponse("MISSING_QUESTION", "Please enter your question or observations."));

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
                return BadRequest(BuildSessionBlockedResponse("SESSION_EXPIRED"));
            }

            if (string.Equals(ex.Message, "SESSION_READ_ONLY", StringComparison.Ordinal))
            {
                return BadRequest(BuildSessionBlockedResponse("SESSION_READ_ONLY"));
            }

            if (string.Equals(ex.Message, "TURN_LIMIT_EXCEEDED", StringComparison.Ordinal))
            {
                return BadRequest(BuildSessionBlockedResponse("TURN_LIMIT_EXCEEDED"));
            }

            return BadRequest(new { message = ex.Message });
        }
        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            request = await _studentService.HydrateVisualQaFollowUpContextAsync(studentId, sessionId, request, cancellationToken);
        }

        request.Language = VisualQaRequestLanguage.ApplyVietnameseQuestionHeuristic(
            request.QuestionText,
            locale,
            VisualQaRequestLanguage.Resolve(Request, request.Language, locale));

        VisualQAResponseDto response;
        if (!string.IsNullOrWhiteSpace(request.ClientRequestId))
        {
            var existing = await _studentService.GetExistingVisualQaResponseAsync(
                studentId,
                sessionId,
                request.ClientRequestId,
                cancellationToken);
            if (existing != null)
            {
                var existingCapabilities = await _studentService.GetVisualQaSessionCapabilitiesAsync(studentId, sessionId, cancellationToken: cancellationToken);
                return Ok(ToApiResponse(existing, existingCapabilities));
            }
        }
        try
        {
            request.SessionId = sessionId;
            response = await _visualQaAiService.RunPipelineAsync(request, cancellationToken);
        }
        catch (AiResponseFormatException ex)
        {
            _logger.LogWarning("Visual QA AskJson: AI_RESPONSE_INVALID_FORMAT: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Visual QA AskJson: AI_SERVICE_UNAVAILABLE: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
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

        var capabilities = await _studentService.GetVisualQaSessionCapabilitiesAsync(studentId, sessionId, cancellationToken: cancellationToken);
        response.UserQuestionText ??= request.QuestionText;
        return Ok(ToApiResponse(response, capabilities));
    }

    /// <summary>
    /// Server-Sent Events: Gemini <c>streamGenerateContent</c> yields <c>event: delta</c> (<c>data: {"delta":"..."}</c>), then <c>event: complete</c> after persistence (same payload shape as <see cref="AskJson"/>).
    /// </summary>
    [HttpPost("ask-stream")]
    [RequestSizeLimit(MaxVisualImageBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxVisualImageBytes)]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("AiInteractionLimit")]
    [Produces("text/event-stream")]
    public async Task<IActionResult> AskStream(
        [FromForm] VisualQAFileUploadRequest formRequest,
        [FromQuery] string? locale,
        CancellationToken cancellationToken)
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

        var (multipartPrepared, multipartError) = await TryPrepareVisualQaMultipartAskAsync(studentId, formRequest, locale, cancellationToken);
        if (multipartError != null)
            return multipartError;

        var prepared = multipartPrepared!;
        var sessionId = prepared.SessionId;
        var request = prepared.Request;
        var uploadedBucket = prepared.UploadedBucket;
        var uploadedFilePath = prepared.UploadedFilePath;

        if (!string.IsNullOrWhiteSpace(request.ClientRequestId))
        {
            var existing = await _studentService.GetExistingVisualQaResponseAsync(
                studentId,
                sessionId,
                request.ClientRequestId,
                cancellationToken);
            if (existing != null)
            {
                var existingCapabilities = await _studentService.GetVisualQaSessionCapabilitiesAsync(studentId, sessionId, cancellationToken: cancellationToken);
                existing.UserQuestionText ??= request.QuestionText;
                await WriteVisualQaSseCompleteAsync(ToApiResponse(existing, existingCapabilities), cancellationToken);
                return new EmptyResult();
            }
        }

        request.SessionId = sessionId;

        VisualQaStreamingPipelineResult pipeline;
        try
        {
            pipeline = await _visualQaAiService.RunStreamingPipelineAsync(request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Visual QA AskStream: AI_SERVICE_UNAVAILABLE: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = ex.Message
            });
        }

        EnsureVisualQaSseHeaders();

        try
        {
            await foreach (var delta in pipeline.TextDeltas.WithCancellation(cancellationToken))
            {
                await WriteVisualQaSseDeltaAsync(delta, cancellationToken);
            }

            var response = await pipeline.CompletedResponseAsync;
            response.SessionId = sessionId;
            try
            {
                await _studentService.SaveVisualQAMessagesAsync(sessionId, request, response);
            }
            catch (InvalidOperationException ex)
            {
                await WriteVisualQaSseErrorAsync("PERSISTENCE_FAILED", ex.Message, cancellationToken);
                return new EmptyResult();
            }

            var capabilities = await _studentService.GetVisualQaSessionCapabilitiesAsync(studentId, sessionId, cancellationToken: cancellationToken);
            response.UserQuestionText ??= request.QuestionText;
            await WriteVisualQaSseCompleteAsync(ToApiResponse(response, capabilities), cancellationToken, writeHeaders: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AiResponseFormatException ex)
        {
            if (!string.IsNullOrWhiteSpace(uploadedBucket) && !string.IsNullOrWhiteSpace(uploadedFilePath))
            {
                try
                {
                    await _storageService.DeleteFileAsync(uploadedBucket, uploadedFilePath, cancellationToken);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to cleanup malformed-response upload for {Path}.", uploadedFilePath);
                }
            }

            await WriteVisualQaSseErrorAsync("AI_RESPONSE_INVALID_FORMAT", ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            await WriteVisualQaSseErrorAsync("AI_SERVICE_UNAVAILABLE", ex.Message, cancellationToken);
        }

        return new EmptyResult();
    }

    private sealed record VisualQaMultipartPrepared(
        Guid SessionId,
        VisualQARequestDto Request,
        string? UploadedBucket,
        string? UploadedFilePath);

    private async Task<(VisualQaMultipartPrepared? prepared, IActionResult? error)> TryPrepareVisualQaMultipartAskAsync(
        Guid studentId,
        VisualQAFileUploadRequest formRequest,
        string? locale,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(formRequest.QuestionText))
            return (null, BadRequest(BuildInputValidationErrorResponse("MISSING_QUESTION", "Please enter your question or observations.")));

        var isFollowUpTurn = formRequest.SessionId.HasValue && formRequest.SessionId.Value != Guid.Empty;

        if (!isFollowUpTurn && (formRequest.CustomImage == null || formRequest.CustomImage.Length == 0))
            return (null, BadRequest(BuildInputValidationErrorResponse("MISSING_IMAGE", "Please attach an image before submitting.")));
        if (formRequest.CustomImage != null && formRequest.CustomImage.Length > MaxVisualImageBytes)
        {
            return (null, BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Image exceeds the 5MB limit.",
                Instance = HttpContext.Request.Path
            }));
        }

        string? imageUrl = null;
        string? uploadedBucket = null;
        string? uploadedFilePath = null;
        if (formRequest.CustomImage != null && formRequest.CustomImage.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(formRequest.CustomImage.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return (null, BadRequest(new { message = "Only JPG, PNG, and WebP images are allowed." }));

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
            AnnotationId = null,
            ClientRequestId = formRequest.ClientRequestId,
            Language = VisualQaRequestLanguage.ApplyVietnameseQuestionHeuristic(
                formRequest.QuestionText,
                locale,
                VisualQaRequestLanguage.Resolve(Request, formRequest.Language, locale))
        };

        Guid sessionId;
        try
        {
            sessionId = await _studentService.CreateOrGetVisualQaSessionAsync(studentId, request);
        }
        catch (ArgumentException ex)
        {
            return (null, BadRequest(new { message = ex.Message }));
        }

        try
        {
            await _studentService.ValidateSessionStateAsync(studentId, sessionId, 3);
        }
        catch (KeyNotFoundException ex)
        {
            return (null, NotFound(new { message = ex.Message }));
        }
        catch (InvalidOperationException ex)
        {
            if (string.Equals(ex.Message, "SESSION_EXPIRED", StringComparison.Ordinal))
            {
                return (null, BadRequest(BuildSessionBlockedResponse("SESSION_EXPIRED")));
            }

            if (string.Equals(ex.Message, "SESSION_READ_ONLY", StringComparison.Ordinal))
            {
                return (null, BadRequest(BuildSessionBlockedResponse("SESSION_READ_ONLY")));
            }

            if (string.Equals(ex.Message, "TURN_LIMIT_EXCEEDED", StringComparison.Ordinal))
            {
                return (null, BadRequest(BuildSessionBlockedResponse("TURN_LIMIT_EXCEEDED")));
            }

            return (null, BadRequest(new { message = ex.Message }));
        }

        if (isFollowUpTurn)
        {
            request = await _studentService.HydrateVisualQaFollowUpContextAsync(studentId, sessionId, request, cancellationToken);
            request.Language = VisualQaRequestLanguage.ApplyVietnameseQuestionHeuristic(
                formRequest.QuestionText,
                locale,
                VisualQaRequestLanguage.Resolve(Request, request.Language, locale));
        }

        return (new VisualQaMultipartPrepared(sessionId, request, uploadedBucket, uploadedFilePath), null);
    }

    private void EnsureVisualQaSseHeaders()
    {
        Response.Headers.CacheControl = "no-cache,no-store";
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("X-Accel-Buffering", "no");
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
    }

    private async Task WriteVisualQaSseDeltaAsync(string delta, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(new { delta }, options);
        await Response.WriteAsync("event: delta\n", cancellationToken);
        await Response.WriteAsync("data: ", cancellationToken);
        await Response.WriteAsync(json, cancellationToken);
        await Response.WriteAsync("\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task WriteVisualQaSseErrorAsync(string errorCode, string message, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Visual QA SSE error: {ErrorCode}: {Message}", errorCode, message);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(new { message }, options);
        await Response.WriteAsync("event: error\n", cancellationToken);
        await Response.WriteAsync("data: ", cancellationToken);
        await Response.WriteAsync(json, cancellationToken);
        await Response.WriteAsync("\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task WriteVisualQaSseCompleteAsync(VisualQaApiResponseDto payload, CancellationToken cancellationToken, bool writeHeaders = true)
    {
        if (writeHeaders)
            EnsureVisualQaSseHeaders();

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(payload, options);
        await Response.WriteAsync("event: complete\n", cancellationToken);
        await Response.WriteAsync("data: ", cancellationToken);
        await Response.WriteAsync(json, cancellationToken);
        await Response.WriteAsync("\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    [HttpPost("turns/{turnId:guid}/request-review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestReview(Guid turnId, [FromQuery] Guid sessionId, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            await _studentService.RequestVisualQaReviewAsync(studentId, sessionId, turnId);
            var capabilities = await _studentService.GetVisualQaSessionCapabilitiesAsync(studentId, sessionId, cancellationToken: cancellationToken);
            var thread = await _studentService.GetVisualQaThreadAsync(studentId, sessionId, cancellationToken);
            return Ok(new
            {
                sessionId,
                reviewRequestedTurnId = turnId,
                capabilities,
                reviewState = thread?.ReviewState ?? "pending",
                systemNotice = BuildSystemNotice(capabilities.Reason)
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (string.Equals(ex.Message, "SESSION_EXPIRED", StringComparison.Ordinal))
            {
                _logger.LogWarning("Visual QA RequestReview: SESSION_EXPIRED");
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Session inactive",
                    Detail = "This Visual QA session expired after 24 hours of inactivity.",
                    Status = StatusCodes.Status403Forbidden,
                    Instance = HttpContext.Request.Path.Value
                });
            }

            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Cannot request review",
                Detail = ex.Message,
                Status = StatusCodes.Status403Forbidden,
                Instance = HttpContext.Request.Path.Value
            });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
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

    private static VisualQaApiResponseDto ToApiResponse(VisualQAResponseDto response, VisualQaCapabilitiesDto capabilities)
    {
        var systemNotice = BuildSystemNotice(capabilities.Reason);
        var assistantMessageId = Guid.TryParse(response.TurnId, out var parsedAssistantMessageId)
            ? parsedAssistantMessageId
            : (Guid?)null;
        var userMessage = response.UserQuestionText?.Trim() ?? string.Empty;
        return new VisualQaApiResponseDto
        {
            SessionId = response.SessionId,
            CaseId = response.CaseId,
            Diagnosis = (response.SuggestedDiagnosis ?? response.AnswerText ?? string.Empty).Trim(),
            Findings = SplitMultilineField(response.KeyImagingFindings),
            DifferentialDiagnoses = response.DifferentialDiagnoses?.ToList() ?? new List<string>(),
            ReflectiveQuestions = SplitMultilineField(response.ReflectiveQuestions),
            Citations = response.Citations,
            Capabilities = capabilities,
            ResponseKind = string.IsNullOrWhiteSpace(response.ResponseKind) ? "analysis" : response.ResponseKind,
            PolicyReason = response.PolicyReason,
            ClientRequestId = response.ClientRequestId,
            // Review workflow pending/escalated is driven by session status on GET thread, not by capabilities after a normal ask.
            ReviewState = "none",
            LastResponderRole = "assistant",
            SystemNotice = systemNotice,
            LatestTurn = new VisualQaTurnDto
            {
                SessionId = response.SessionId ?? Guid.Empty,
                TurnId = response.TurnId,
                ActorRole = "assistant",
                UserMessageId = Guid.Empty,
                AssistantMessageId = assistantMessageId,
                UserMessage = userMessage,
                QuestionText = userMessage,
                MessageText = response.AnswerText,
                AnswerText = response.AnswerText,
                Diagnosis = (response.SuggestedDiagnosis ?? response.AnswerText ?? string.Empty).Trim(),
                Findings = SplitMultilineField(response.KeyImagingFindings),
                DifferentialDiagnoses = response.DifferentialDiagnoses?.ToList() ?? new List<string>(),
                ReflectiveQuestions = SplitMultilineField(response.ReflectiveQuestions),
                Citations = response.Citations,
                CreatedAt = DateTime.UtcNow,
                ResponseKind = string.IsNullOrWhiteSpace(response.ResponseKind) ? "analysis" : response.ResponseKind,
                PolicyReason = response.PolicyReason,
                ReviewState = "none",
                LastResponderRole = "assistant",
                IsReviewTarget = false
            }
        };
    }

    private static IReadOnlyList<string> SplitMultilineField(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('-', '*').Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static object BuildSessionBlockedResponse(string reason)
    {
        var notice = BuildSystemNotice(reason);
        return new
        {
            message = notice,
            systemNotice = notice,
            capabilities = new VisualQaCapabilitiesDto
            {
                CanAskNext = false,
                IsReadOnly = reason is "SESSION_READ_ONLY" or "SESSION_EXPIRED",
                CanRequestReview = false,
                TurnsUsed = 0,
                TurnLimit = 3,
                Reason = reason
            },
            latestTurn = new
            {
                turnId = $"system:{reason.ToLowerInvariant()}",
                actorRole = "system",
                userMessage = string.Empty,
                questionText = string.Empty,
                messageText = notice,
                responseKind = "system_notice",
                policyReason = (string?)null
            }
        };
    }

    private static object BuildInputValidationErrorResponse(string reason, string message)
    {
        return new
        {
            message,
            systemNotice = message,
            capabilities = new VisualQaCapabilitiesDto
            {
                CanAskNext = true,
                IsReadOnly = false,
                CanRequestReview = false,
                TurnsUsed = 0,
                TurnLimit = 3,
                Reason = null
            },
            latestTurn = new
            {
                turnId = $"system:{reason.ToLowerInvariant()}",
                actorRole = "system",
                userMessage = string.Empty,
                questionText = string.Empty,
                messageText = message,
                responseKind = "system_notice",
                policyReason = (string?)null
            }
        };
    }

    private static string? BuildSystemNotice(string? reason)
    {
        return reason switch
        {
            "TURN_LIMIT_EXCEEDED" => "You have used all question turns for this Visual QA session.",
            "SESSION_EXPIRED" => "This Visual QA session expired after 24 hours of inactivity.",
            "SESSION_READ_ONLY" => "This session is locked. You cannot send new questions.",
            _ => null
        };
    }
}
