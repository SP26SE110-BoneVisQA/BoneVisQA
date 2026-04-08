using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer/classes/{classId:guid}/assignments")]
[Tags("Lecturer - Assignments")]
[Authorize(Roles = "Lecturer")]
public class LecturerAssignmentsController : ControllerBase
{
    private readonly ILecturerAssignmentService _lecturerAssignmentService;

    public LecturerAssignmentsController(ILecturerAssignmentService lecturerAssignmentService)
    {
        _lecturerAssignmentService = lecturerAssignmentService;
    }

    [HttpPost("cases")]
    public async Task<ActionResult<IReadOnlyList<ClassCaseAssignmentDto>>> AssignCases(Guid classId, [FromBody] AssignCasesRequestDto request)
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            var result = await _lecturerAssignmentService.AssignCasesAsync(lecturerId.Value, classId, request);
            return Ok(result);
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

    [HttpPost("quizzes")]
    public async Task<ActionResult<ClassQuizSessionDto>> AssignQuiz(Guid classId, [FromBody] AssignQuizSessionRequestDto request)
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            var result = await _lecturerAssignmentService.AssignQuizSessionAsync(lecturerId.Value, classId, request);
            return Ok(result);
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

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
