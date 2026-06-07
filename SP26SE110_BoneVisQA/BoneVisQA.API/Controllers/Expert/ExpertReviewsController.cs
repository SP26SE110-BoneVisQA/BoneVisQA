using System;
using System.Security.Claims;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Expert;

[ApiController]
[Route("api/expert/reviews")]
[Authorize(Roles = "Expert")]
[Tags("Expert - Reviews")]
public class ExpertReviewsController : ControllerBase
{
    private readonly IExpertReviewService _expertReviewService;

    public ExpertReviewsController(IExpertReviewService expertReviewService)
    {
        _expertReviewService = expertReviewService;
    }


    [ProducesResponseType(typeof(IReadOnlyList<ExpertEscalatedAnswerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [HttpGet("case-answer")]
    public async Task<ActionResult<IReadOnlyList<ExpertEscalatedAnswerDto>>> GetCaseAanswer(
        [FromQuery] Guid? specialtyId = null,
        [FromQuery] string? status = null)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _expertReviewService.GetCaseAnswersAsync(expertId.Value, specialtyId, status);
        return Ok(result);
    }

    [ProducesResponseType(typeof(IReadOnlyList<ExpertEscalatedAnswerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [HttpGet("escalated")]
    public async Task<ActionResult<IReadOnlyList<ExpertEscalatedAnswerDto>>> GetEscalated(
        [FromQuery] Guid? specialtyId = null,
        [FromQuery] string? status = null)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _expertReviewService.GetEscalatedAnswersAsync(expertId.Value, specialtyId, status);
        return Ok(result);
    }

    private async Task<ActionResult<ExpertEscalatedAnswerDto>> GetEscalatedSessionDetailCore(Guid sessionId)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            var result = await _expertReviewService.GetEscalatedSessionDetailAsync(expertId.Value, sessionId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(ExpertEscalatedAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ExpertEscalatedAnswerDto>> GetEscalatedSessionDetail(Guid sessionId)
        => GetEscalatedSessionDetailCore(sessionId);

    [HttpGet("{sessionId:guid}/session")]
    [ProducesResponseType(typeof(ExpertEscalatedAnswerDto), StatusCodes.Status200OK)]
    public Task<ActionResult<ExpertEscalatedAnswerDto>> GetEscalatedSessionDetailSessionAlias(Guid sessionId)
        => GetEscalatedSessionDetailCore(sessionId);

    [ProducesResponseType(typeof(ExpertEscalatedAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPost("{sessionId:guid}/resolve")]
    public async Task<ActionResult<ExpertEscalatedAnswerDto>> Resolve(Guid sessionId, [FromBody] ResolveEscalatedAnswerRequestDto request)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            var result = await _expertReviewService.ResolveEscalatedAnswerAsync(expertId.Value, sessionId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("required", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("human note", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = ex.Message });

            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost("{sessionId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApprovePost(Guid sessionId, CancellationToken cancellationToken)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var request = await PromoteToLibraryRequestReader.ReadAsync(Request, cancellationToken)
                      ?? new PromoteToLibraryRequestDto();

        try
        {
            await _expertReviewService.ApproveSessionAsync(expertId.Value, sessionId);
            var caseId = await _expertReviewService.PromoteToLibraryAsync(expertId.Value, sessionId, request);
            return Ok(new { caseId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("permission", StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{sessionId:guid}/respond")]
    [ProducesResponseType(typeof(ExpertEscalatedAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpertEscalatedAnswerDto>> Respond(Guid sessionId, [FromBody] ExpertRespondRequestDto request)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            var result = await _expertReviewService.RespondToSessionAsync(expertId.Value, sessionId, request.Content);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("required", StringComparison.OrdinalIgnoreCase)
                ? BadRequest(new { message = ex.Message })
                : StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{sessionId:guid}/promote")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Promote(Guid sessionId, CancellationToken cancellationToken)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var request = await PromoteToLibraryRequestReader.ReadAsync(Request, cancellationToken);
        if (request == null)
        {
            return BadRequest(new
            {
                message = "Request body must be JSON (Content-Type: application/json) with promote-to-library fields.",
                code = "MISSING_BODY",
            });
        }

        try
        {
            var caseId = await _expertReviewService.PromoteToLibraryAsync(expertId.Value, sessionId, request);
            return Ok(new { caseId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("permission", StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("chunks/{chunkId:guid}/flag")]
    [HttpPost("~/api/expert/documents/chunks/{chunkId:guid}/flag")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FlagChunk(Guid chunkId, [FromBody] FlagChunkRequestDto request)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            await _expertReviewService.FlagChunkAsync(expertId.Value, chunkId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("required", StringComparison.OrdinalIgnoreCase)
                ? BadRequest(new { message = ex.Message })
                : StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPut("{sessionId:guid}/draft")]
    [ProducesResponseType(typeof(ExpertVisualSessionDraftResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExpertVisualSessionDraftResponseDto>> PutDraft(Guid sessionId, [FromBody] ExpertVisualSessionDraftRequestDto request)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            var result = await _expertReviewService.UpsertSessionReviewDraftAsync(expertId.Value, sessionId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{sessionId:guid}/draft")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteDraft(Guid sessionId)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            await _expertReviewService.DeleteSessionReviewDraftAsync(expertId.Value, sessionId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private Guid? GetUserIdFromClaims()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
