using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Student;

[ApiController]
[Route("api/student/quizzes")]
[Authorize(Roles = "Student")]
public class StudentQuizzesController : ControllerBase
{
    private readonly IStudentLearningService _studentLearningService;
    private readonly IStudentService _studentService;

    public StudentQuizzesController(IStudentLearningService studentLearningService, IStudentService studentService)
    {
        _studentLearningService = studentLearningService;
        _studentService = studentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuizListItemDto>>> GetQuizzes()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _studentService.GetAvailableQuizzesAsync(studentId.Value);
        return Ok(result);
    }

    [HttpPost("{quizId:guid}/start")]
    public async Task<ActionResult<QuizSessionDto>> StartQuiz(Guid quizId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _studentService.StartQuizAsync(studentId.Value, quizId);
        return Ok(result);
    }

    [HttpGet("practice")]
    public async Task<ActionResult<QuizSessionDto>> GetPracticeQuiz([FromQuery] string? topic)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            var result = await _studentLearningService.GetPracticeQuizAsync(studentId.Value, topic);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("submit")]
    public async Task<ActionResult<QuizResultDto>> SubmitQuiz([FromBody] SubmitQuizRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            var result = await _studentLearningService.SubmitQuizAttemptAsync(studentId.Value, request);
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

    [HttpPost("answers")]
    public async Task<ActionResult<StudentSubmitQuestionResponseDto>> SubmitQuizAnswer([FromBody] StudentSubmitQuestionDto submit)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        submit.StudentId = studentId.Value;
        var result = await _studentService.SubmitQuizAsync(studentId.Value, submit);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
