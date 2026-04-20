using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/qa")]
[Tags("Q&A")]
[Authorize(Roles = "Student")]
public class QaController : ControllerBase
{
    private readonly IStudentService _studentService;

    public QaController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpPost("{answerId:guid}/request-support")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestSupport(Guid answerId, CancellationToken cancellationToken)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(rawUserId, out var studentId))
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            await _studentService.RequestSupportAsync(studentId, answerId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
