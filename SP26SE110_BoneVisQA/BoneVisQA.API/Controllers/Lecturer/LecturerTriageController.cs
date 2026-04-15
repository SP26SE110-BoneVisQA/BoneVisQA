using System.Security.Claims;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer/triage")]
[Tags("Lecturer - Triage")]
[Authorize(Roles = "Lecturer")]
public class LecturerTriageController : ControllerBase
{
    private readonly ILecturerTriageService _lecturerTriageService;

    public LecturerTriageController(ILecturerTriageService lecturerTriageService)
    {
        _lecturerTriageService = lecturerTriageService;
    }

    /// <summary>Same as <c>PUT /api/lecturer/reviews/{answerId}/escalate</c>; kept for older clients.</summary>
    [HttpPost("{answerId:guid}/escalate")]
    [HttpPut("{answerId:guid}/escalate")]
    [ProducesResponseType(typeof(EscalatedAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EscalatedAnswerDto>> Escalate(Guid answerId, [FromBody] EscalateAnswerRequestDto? request)
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

    [HttpPost("{sessionId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid sessionId, [FromBody] RejectAnswerRequestDto request)
    {
        var lecturerId = GetUserIdFromClaims();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            await _lecturerTriageService.RejectAnswerAsync(lecturerId.Value, sessionId, request.Reason);
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

    private Guid? GetUserIdFromClaims()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
