using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Services.AiQuizServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Student;

[ApiController]
[Route("api/student/questions")]
[Tags("Student - Questions")]
[Authorize(Roles = "Student")]
public class StudentQuestionsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IQuizHintService _quizHintService;

    public StudentQuestionsController(IStudentService studentService, IQuizHintService quizHintService)
    {
        _studentService = studentService;
        _quizHintService = quizHintService;
    }

    [HttpPost]
    public async Task<ActionResult<StudentQuestionDto>> AskQuestion([FromBody] AskQuestionRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _studentService.AskQuestionAsync(studentId.Value, request);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StudentQuestionHistoryItemDto>>> GetQuestionHistory()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _studentService.GetQuestionHistoryAsync(studentId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Get AI hint for a quiz question.
    /// </summary>
    [HttpGet("{questionId:guid}/hint")]
    public async Task<ActionResult<QuizHintResultDto>> GetHint(
        Guid questionId,
        [FromQuery] Guid? attemptId,
        [FromQuery] int level = 1)
    {
        var result = await _quizHintService.GetHintAsync(questionId, attemptId, level);
        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
