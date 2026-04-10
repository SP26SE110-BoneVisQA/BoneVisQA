using System;
using System.Security.Claims;
using BoneVisQA.Services.Exceptions;
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

    /// <summary>
    /// Gets the expert's escalated review queue together with the retrieved RAG evidence chunks.
    /// </summary>

    [ProducesResponseType(typeof(IReadOnlyList<ExpertEscalatedAnswerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [HttpGet("case-answer")]
    public async Task<ActionResult<IReadOnlyList<ExpertEscalatedAnswerDto>>> GetCaseAanswer()
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _expertReviewService.GetCaseAnswersAsync(expertId.Value);
        return Ok(result);
    }

    [ProducesResponseType(typeof(IReadOnlyList<ExpertEscalatedAnswerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [HttpGet("escalated")]
    public async Task<ActionResult<IReadOnlyList<ExpertEscalatedAnswerDto>>> GetEscalated()
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _expertReviewService.GetEscalatedAnswersAsync(expertId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Resolves an escalated answer and stores the expert-reviewed outcome.
    /// </summary>
    [ProducesResponseType(typeof(ExpertEscalatedAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPost("{answerId:guid}/resolve")]
    public async Task<ActionResult<ExpertEscalatedAnswerDto>> Resolve(Guid answerId, [FromBody] ResolveEscalatedAnswerRequestDto request)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            var result = await _expertReviewService.ResolveEscalatedAnswerAsync(expertId.Value, answerId, request);
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
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    /// <summary>Approves / finalizes an escalated answer (same contract as <c>resolve</c>).</summary>
    [HttpPost("{answerId:guid}/approve")]
    [ProducesResponseType(typeof(ExpertEscalatedAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ExpertEscalatedAnswerDto>> ApprovePost(Guid answerId, [FromBody] ResolveEscalatedAnswerRequestDto request)
        => Resolve(answerId, request);

    /// <summary>Approves / finalizes an escalated answer (same contract as <c>resolve</c>).</summary>
    [HttpPut("{answerId:guid}/approve")]
    [ProducesResponseType(typeof(ExpertEscalatedAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ExpertEscalatedAnswerDto>> ApprovePut(Guid answerId, [FromBody] ResolveEscalatedAnswerRequestDto request)
        => Resolve(answerId, request);

    /// <summary>
    /// Flags a retrieved document chunk as low quality for later knowledge base review.
    /// </summary>
    [HttpPost("chunks/{chunkId:guid}/flag")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FlagChunk(Guid chunkId, [FromBody] FlagChunkRequestDto request)
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

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
            return ex.Message.Contains("bắt buộc", StringComparison.OrdinalIgnoreCase)
                ? BadRequest(new { message = ex.Message })
                : StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    private Guid? GetUserIdFromClaims()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
