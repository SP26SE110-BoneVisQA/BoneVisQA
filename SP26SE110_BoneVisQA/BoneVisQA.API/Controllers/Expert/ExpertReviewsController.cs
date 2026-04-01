using System.Security.Claims;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Expert;

[ApiController]
[Route("api/expert/reviews")]
[Authorize(Roles = "Expert")]
public class ExpertReviewsController : ControllerBase
{
    private readonly IExpertReviewService _expertReviewService;

    public ExpertReviewsController(IExpertReviewService expertReviewService)
    {
        _expertReviewService = expertReviewService;
    }

    [HttpGet("escalated")]
    public async Task<ActionResult<IReadOnlyList<ExpertEscalatedAnswerDto>>> GetEscalated()
    {
        var expertId = GetUserIdFromClaims();
        if (expertId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _expertReviewService.GetEscalatedAnswersAsync(expertId.Value);
        return Ok(result);
    }

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
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    private Guid? GetUserIdFromClaims()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
