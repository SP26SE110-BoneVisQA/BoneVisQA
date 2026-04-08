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

    [HttpPost("{answerId:guid}/escalate")]
    [ProducesResponseType(typeof(EscalatedAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EscalatedAnswerDto>> Escalate(Guid answerId, [FromBody] EscalateAnswerRequestDto? request)
    {
        var lecturerId = GetUserIdFromClaims();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

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
