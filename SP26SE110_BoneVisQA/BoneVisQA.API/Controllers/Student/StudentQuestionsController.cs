using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Student;

[ApiController]
[Route("api/student/questions")]
[Authorize(Roles = "Student")]
public class StudentQuestionsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentQuestionsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpPost]
    public async Task<ActionResult<StudentQuestionDto>> AskQuestion([FromBody] AskQuestionRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _studentService.AskQuestionAsync(studentId.Value, request);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StudentQuestionHistoryItemDto>>> GetQuestionHistory()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _studentService.GetQuestionHistoryAsync(studentId.Value);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
