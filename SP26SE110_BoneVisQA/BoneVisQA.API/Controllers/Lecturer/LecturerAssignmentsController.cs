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

    // ── Quiz Review Endpoints ───────────────────────────────────────────────────

    /// <summary>Xem danh sách bài quiz của tất cả sinh viên trong lớp.</summary>
    [HttpGet("quizzes/{quizId:guid}/attempts")]
    public async Task<ActionResult<IReadOnlyList<StudentQuizAttemptDto>>> GetQuizAttempts(Guid classId, Guid quizId)
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            var result = await _lecturerAssignmentService.GetClassQuizAttemptsAsync(lecturerId.Value, classId, quizId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Xem chi tiết bài làm của 1 sinh viên (câu hỏi + câu trả lời + điểm).</summary>
    [HttpGet("quizzes/{quizId:guid}/attempts/{attemptId:guid}")]
    public async Task<ActionResult<QuizAttemptDetailDto>> GetQuizAttemptDetail(Guid classId, Guid quizId, Guid attemptId)
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            var result = await _lecturerAssignmentService.GetQuizAttemptDetailAsync(lecturerId.Value, classId, quizId, attemptId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Chỉnh sửa điểm / câu trả lời của 1 bài quiz.</summary>
    [HttpPut("quizzes/{quizId:guid}/attempts/{attemptId:guid}")]
    public async Task<ActionResult<QuizAttemptDetailDto>> UpdateQuizAttempt(
        Guid classId, Guid quizId, Guid attemptId, [FromBody] UpdateQuizAttemptRequestDto request)
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            var result = await _lecturerAssignmentService.UpdateQuizAttemptAsync(
                lecturerId.Value, classId, quizId, attemptId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
