using System;
using System.Collections.Generic;
using System.Linq;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Exceptions;
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

    [FromForm(Name = "clientRequestId")]
    public string? ClientRequestId { get; set; }
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
    public async Task<ActionResult<VisualQaApiResponseDto>> Ask([FromForm] VisualQAFileUploadRequest formRequest, CancellationToken cancellationToken)
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
            return BadRequest(BuildInputValidationErrorResponse("MISSING_QUESTION", "Please enter your question or observations."));

        var isFollowUpTurn = formRequest.SessionId.HasValue && formRequest.SessionId.Value != Guid.Empty;

        if (!isFollowUpTurn && (formRequest.CustomImage == null || formRequest.CustomImage.Length == 0))
            return BadRequest(BuildInputValidationErrorResponse("MISSING_IMAGE", "Please attach an image before submitting."));
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
            AnnotationId = null,
            ClientRequestId = formRequest.ClientRequestId
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
                return BadRequest(BuildSessionBlockedResponse("SESSION_EXPIRED", "The Q&A session expired due to 24 hours of inactivity."));
            }

            if (string.Equals(ex.Message, "SESSION_READ_ONLY", StringComparison.Ordinal))
            {
                return BadRequest(BuildSessionBlockedResponse("SESSION_READ_ONLY", "This Q&A session is view-only and cannot be continued."));
            }

            if (string.Equals(ex.Message, "TURN_LIMIT_EXCEEDED", StringComparison.Ordinal))
            {
                return BadRequest(BuildSessionBlockedResponse("TURN_LIMIT_EXCEEDED", "This chat session has reached the 3-question limit."));
            }

            return BadRequest(new { message = ex.Message });
        }
        if (isFollowUpTurn)
        {
            request = await _studentService.HydrateVisualQaFollowUpContextAsync(studentId, sessionId, request, cancellationToken);
        }
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

                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    errorCode = "AI_RESPONSE_INVALID_FORMAT",
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

    [HttpPost("ask-json")]
    [EnableRateLimiting("AiInteractionLimit")]
    public async Task<ActionResult<VisualQaApiResponseDto>> AskJson([FromBody] VisualQARequestDto request, CancellationToken cancellationToken)
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
                return BadRequest(BuildSessionBlockedResponse("SESSION_EXPIRED", "The Q&A session expired due to 24 hours of inactivity."));
            }

            if (string.Equals(ex.Message, "SESSION_READ_ONLY", StringComparison.Ordinal))
            {
                return BadRequest(BuildSessionBlockedResponse("SESSION_READ_ONLY", "This Q&A session is view-only and cannot be continued."));
            }

            if (string.Equals(ex.Message, "TURN_LIMIT_EXCEEDED", StringComparison.Ordinal))
            {
                return BadRequest(BuildSessionBlockedResponse("TURN_LIMIT_EXCEEDED", "This chat session has reached the 3-question limit."));
            }

            return BadRequest(new { message = ex.Message });
        }
        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            request = await _studentService.HydrateVisualQaFollowUpContextAsync(studentId, sessionId, request, cancellationToken);
        }
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
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                errorCode = "AI_RESPONSE_INVALID_FORMAT",
                message = ex.Message
            });
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

        var capabilities = await _studentService.GetVisualQaSessionCapabilitiesAsync(studentId, sessionId, cancellationToken: cancellationToken);
        response.UserQuestionText ??= request.QuestionText;
        return Ok(ToApiResponse(response, capabilities));
    }

    [HttpPost("turns/{turnId:guid}/request-review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
                systemNotice = BuildSystemNotice(capabilities.Reason),
                systemNoticeCode = capabilities.Reason
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
                return BadRequest(new
                {
                    errorCode = "SESSION_EXPIRED",
                    message = "The Q&A session expired due to 24 hours of inactivity."
                });
            }

            return BadRequest(new { message = ex.Message });
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
            Diagnosis = (response.SuggestedDiagnosis ?? response.AnswerText ?? string.Empty).Trim(),
            Findings = SplitMultilineField(response.KeyImagingFindings),
            DifferentialDiagnoses = response.DifferentialDiagnoses?.ToList() ?? new List<string>(),
            ReflectiveQuestions = SplitMultilineField(response.ReflectiveQuestions),
            Citations = response.Citations,
            Capabilities = capabilities,
            ResponseKind = string.IsNullOrWhiteSpace(response.ResponseKind) ? "analysis" : response.ResponseKind,
            PolicyReason = response.PolicyReason,
            ClientRequestId = response.ClientRequestId,
            ReviewState = capabilities.IsReadOnly && capabilities.Reason == "SESSION_READ_ONLY" ? "pending" : "none",
            LastResponderRole = "assistant",
            SystemNotice = systemNotice,
            SystemNoticeCode = capabilities.Reason,
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
                Diagnosis = (response.SuggestedDiagnosis ?? response.AnswerText ?? string.Empty).Trim(),
                Findings = SplitMultilineField(response.KeyImagingFindings),
                DifferentialDiagnoses = response.DifferentialDiagnoses?.ToList() ?? new List<string>(),
                ReflectiveQuestions = SplitMultilineField(response.ReflectiveQuestions),
                Citations = response.Citations,
                CreatedAt = DateTime.UtcNow,
                ResponseKind = string.IsNullOrWhiteSpace(response.ResponseKind) ? "analysis" : response.ResponseKind,
                PolicyReason = response.PolicyReason,
                ReviewState = capabilities.IsReadOnly && capabilities.Reason == "SESSION_READ_ONLY" ? "pending" : "none",
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

    private static object BuildSessionBlockedResponse(string reason, string message)
    {
        return new
        {
            errorCode = reason,
            message,
            systemNotice = BuildSystemNotice(reason),
            systemNoticeCode = reason,
            capabilities = new VisualQaCapabilitiesDto
            {
                CanAskNext = false,
                IsReadOnly = reason is "SESSION_READ_ONLY" or "SESSION_EXPIRED",
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
                messageText = BuildSystemNotice(reason) ?? message,
                responseKind = "system_notice",
                policyReason = reason
            }
        };
    }

    private static object BuildInputValidationErrorResponse(string reason, string message)
    {
        return new
        {
            errorCode = reason,
            message,
            systemNotice = message,
            systemNoticeCode = reason,
            capabilities = new VisualQaCapabilitiesDto
            {
                CanAskNext = true,
                IsReadOnly = false,
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
                policyReason = reason
            }
        };
    }

    private static string? BuildSystemNotice(string? reason)
    {
        return reason switch
        {
            "TURN_LIMIT_EXCEEDED" => "You have reached the maximum number of questions for this Visual QA session.",
            "SESSION_EXPIRED" => "This Visual QA session expired after 24 hours of inactivity.",
            "SESSION_READ_ONLY" => "This Visual QA session is now read-only because it has entered the review workflow.",
            _ => null
        };
    }
}
