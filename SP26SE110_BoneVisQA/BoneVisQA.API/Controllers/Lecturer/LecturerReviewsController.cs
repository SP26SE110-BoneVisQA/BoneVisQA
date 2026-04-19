using System.Security.Claims;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Lecturer;

/// <summary>Review actions (escalate to expert, etc.) under <c>/api/lecturer/reviews</c> for FE routing.</summary>
[ApiController]
[Route("api/lecturer/reviews")]
[Tags("Lecturer - Reviews")]
[Authorize(Roles = "Lecturer")]
public class LecturerReviewsController : ControllerBase
{
    private readonly ILecturerTriageService _lecturerTriageService;

    public LecturerReviewsController(ILecturerTriageService lecturerTriageService)
    {
        _lecturerTriageService = lecturerTriageService;
    }

    /// <summary>Escalate a case answer to the class expert (<c>CaseAnswer.Status</c> → <c>EscalatedToExpert</c>).</summary>
    [HttpPut("{answerId:guid}/escalate")]
    [ProducesResponseType(typeof(EscalatedAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EscalatedAnswerDto>> EscalatePut(Guid answerId, [FromBody] EscalateAnswerRequestDto? request)
    {
        var lecturerId = GetUserIdFromClaims();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            var result = await _lecturerTriageService.EscalateAnswerAsync(lecturerId.Value, answerId, request);
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

    private Guid? GetUserIdFromClaims()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
