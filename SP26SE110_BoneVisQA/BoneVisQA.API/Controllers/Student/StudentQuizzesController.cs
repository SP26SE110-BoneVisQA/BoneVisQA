using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Models.Quiz;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Student;

[ApiController]
[Route("api/student/quizzes")]
[Tags("Student - Quizzes")]
[Authorize(Roles = "Student")]
public class StudentQuizzesController : ControllerBase
{
    private readonly IStudentLearningService _studentLearningService;
    private readonly IStudentService _studentService;
    private readonly IAIQuizService _aiQuizService;

    public StudentQuizzesController(
        IStudentLearningService studentLearningService,
        IStudentService studentService,
        IAIQuizService aiQuizService)
    {
        _studentLearningService = studentLearningService;
        _studentService = studentService;
        _aiQuizService = aiQuizService;
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

    /// <summary>
    /// AI Generate + Lưu vào DB → Trả về session quiz để student bắt đầu làm ngay.
    /// Kết hợp: Generate (AI) + Save (Quiz + QuizAttempt) trong 1 lần gọi.
    /// </summary>
    [HttpPost("practice/generate")]
    public async Task<ActionResult<StudentGeneratedQuizAttemptDto>> GenerateAndSavePracticeQuiz(
        [FromBody] GeneratePracticeQuizRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        if (string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { message = "Topic là bắt buộc." });

        // 1. Gọi AI tạo câu hỏi
        var generated = await _aiQuizService.GenerateQuizQuestionsAsync(
            request.Topic,
            request.QuestionCount ?? 5,
            request.Difficulty);

        if (!generated.Success || generated.Questions.Count == 0)
            return Ok(new StudentGeneratedQuizAttemptDto
            {
                AttemptId = Guid.Empty,
                QuizId = Guid.Empty,
                Title = string.Empty,
                Topic = request.Topic,
                Questions = Array.Empty<StudentQuizQuestionDto>(),
                SavedToHistory = false,
            });

        // 2. Lưu vào DB (tạo Quiz + QuizAttempt)
        var session = await _studentLearningService.SaveAndStartGeneratedQuizAsync(
            studentId.Value,
            generated,
            request.Topic,
            request.Difficulty);

        return Ok(session);
    }

    [HttpPost("practice/save")]
    public async Task<ActionResult<AIQuizGenerationResultDto>> GeneratePracticeQuiz([FromBody] GeneratePracticeQuizRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        if (string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { message = "Topic là bắt buộc." });

        var result = await _aiQuizService.GenerateQuizQuestionsAsync(
            request.Topic,
            request.QuestionCount ?? 5,
            request.Difficulty);

        return Ok(result);
    }

    public class GeneratePracticeQuizRequestDto
    {
        public string Topic { get; set; } = string.Empty;
        public int? QuestionCount { get; set; }
        public string? Difficulty { get; set; }
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<StudentQuizAttemptSummaryDto>>> GetQuizHistory()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _studentLearningService.GetQuizAttemptHistoryAsync(studentId.Value);
        return Ok(result);
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

    /// <summary>
    /// Lấy chi tiết đáp án của một quiz attempt đã nộp (để review sau khi nộp).
    /// </summary>
    [HttpGet("{attemptId}/review")]
    public async Task<ActionResult<QuizAttemptReviewDto>> GetQuizAttemptReview(Guid attemptId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Unauthorized." });

        try
        {
            var result = await _studentLearningService.GetQuizAttemptReviewAsync(studentId.Value, attemptId);
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

    /// <summary>
    /// Xóa một quiz attempt của student.
    /// </summary>
    [HttpDelete("{attemptId}")]
    public async Task<ActionResult> DeleteQuizAttempt(Guid attemptId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Unauthorized." });

        try
        {
            await _studentLearningService.DeleteQuizAttemptAsync(studentId.Value, attemptId);
            return NoContent();
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
