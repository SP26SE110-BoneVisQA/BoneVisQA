using System.Security.Claims;
using System.Text.Json;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models;
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
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _studentService.GetAvailableQuizzesAsync(studentId.Value);
        return Ok(result);
    }

    [HttpPost("{quizId:guid}/start")]
    public async Task<ActionResult<QuizSessionDto>> StartQuiz(Guid quizId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            var result = await _studentService.StartQuizAsync(studentId.Value, quizId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("practice")]
    public async Task<ActionResult<QuizSessionDto>> GetPracticeQuiz([FromQuery] string? topic)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

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
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { message = "Topic is required." });

        // 1. Gọi AI tạo câu hỏi
        var generated = await _aiQuizService.GenerateQuizQuestionsAsync(
            request.Topic,
            request.QuestionCount ?? 5,
            request.Difficulty);

        if (!generated.Success || generated.Questions.Count == 0)
            return Ok(new StudentGeneratedQuizAttemptDto
            {
                Success = false,
                Message = generated.Message ?? "Unable to generate questions. Please try again.",
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

        return Ok(new StudentGeneratedQuizAttemptDto
        {
            Success = true,
            Message = $"AI generated {session.Questions.Count} questions for you!",
            AttemptId = session.AttemptId,
            QuizId = session.QuizId,
            Title = session.Title,
            Topic = session.Topic,
            Questions = session.Questions,
            SavedToHistory = session.SavedToHistory,
        });
    }

    [HttpPost("practice/save")]
    public async Task<ActionResult<AIQuizGenerationResultDto>> GeneratePracticeQuiz([FromBody] GeneratePracticeQuizRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { message = "Topic is required." });

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
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _studentLearningService.GetQuizAttemptHistoryAsync(studentId.Value);
        return Ok(result);
    }

    [HttpGet("history/paged")]
    public async Task<ActionResult<PagedResultDTO<StudentQuizAttemptSummaryDto>>> GetQuizHistoryPaged(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? quizTitle = null,
        [FromQuery] string? topic = null,
        [FromQuery] bool? isAiGenerated = null,
        [FromQuery] bool? passed = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _studentLearningService.GetQuizAttemptHistoryPagedAsync(
            studentId.Value,
            pageIndex,
            pageSize,
            quizTitle,
            topic,
            isAiGenerated,
            passed,
            fromDate,
            toDate);
        return Ok(result);
    }

    [HttpPost("submit")]
    public async Task<ActionResult<QuizResultDto>> SubmitQuiz([FromBody] SubmitQuizRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

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
            return Unauthorized(new { message = "Token does not contain a valid user id." });

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

    /// <summary>
    /// Student gửi yêu cầu làm lại quiz — tạo notification + email cho lecturer.
    /// </summary>
    [HttpPost("{quizId:guid}/request-retake")]
    public async Task<ActionResult> RequestRetake(Guid quizId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            await _studentService.RequestRetakeAsync(studentId.Value, quizId);
            return Ok(new { message = "Retake request has been sent to your lecturer." });
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
    /// Lưu quiz (sau khi nộp) vào flashcard deck để student luyện tập lại.
    /// Tạo deck mới với tên quiz, mỗi câu hỏi trở thành 1 flashcard có đáp án + giải thích.
    /// </summary>
    [HttpPost("{attemptId}/save-to-flashcards")]
    public async Task<ActionResult<SaveQuizToFlashcardsResultDto>> SaveQuizToFlashcards(
        Guid attemptId,
        [FromBody] SaveQuizToFlashcardsRequestDto? request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        try
        {
            var result = await _studentLearningService.SaveQuizAttemptToFlashcardsAsync(
                studentId.Value,
                attemptId,
                request?.DeckName,
                request?.Description);
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
    /// Lấy danh sách cases có sẵn để student chọn tạo AI practice quiz.
    /// </summary>
    [HttpGet("cases")]
    public async Task<ActionResult<List<AIQuizCaseInputDto>>> GetAvailableCases(
        [FromQuery] string? topic = null,
        [FromQuery] int limit = 20)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var cases = await _aiQuizService.GetAvailableCasesAsync(topic, limit);
        return Ok(cases);
    }

    /// <summary>
    /// AI Generate + Lưu quiz từ Case đã chọn → Trả về session quiz để student bắt đầu làm ngay.
    /// Student chọn 1 hoặc nhiều cases, AI tạo câu hỏi dựa trên case(s) đó.
    /// </summary>
    [HttpPost("practice/from-cases")]
    public async Task<ActionResult<StudentGeneratedQuizAttemptDto>> GeneratePracticeQuizFromCases(
        [FromBody] GeneratePracticeQuizFromCasesRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (request.Cases == null || request.Cases.Count == 0)
            return BadRequest(new { message = "Please select at least 1 case." });

        // 1. Gọi AI tạo câu hỏi từ cases
        var generated = await _aiQuizService.GenerateQuizFromCasesAsync(
            request.Cases,
            request.QuestionCount ?? 5,
            request.Difficulty);

        if (!generated.Success || generated.Questions.Count == 0)
            return Ok(new StudentGeneratedQuizAttemptDto
            {
                Success = false,
                Message = generated.Message ?? "Unable to generate questions. Please try again.",
                AttemptId = Guid.Empty,
                QuizId = Guid.Empty,
                Title = string.Empty,
                Topic = generated.Topic,
                Questions = Array.Empty<StudentQuizQuestionDto>(),
                SavedToHistory = false,
            });

        // 2. Lưu vào DB (tạo Quiz + QuizAttempt)
        var session = await _studentLearningService.SaveAndStartGeneratedQuizAsync(
            studentId.Value,
            generated,
            generated.Topic,
            request.Difficulty);

        return Ok(new StudentGeneratedQuizAttemptDto
        {
            Success = true,
            Message = $"AI generated {session.Questions.Count} questions from {request.Cases.Count} case(s)!",
            AttemptId = session.AttemptId,
            QuizId = session.QuizId,
            Title = session.Title,
            Topic = session.Topic,
            Questions = session.Questions,
            SavedToHistory = session.SavedToHistory,
        });
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    public class SaveQuizToFlashcardsRequestDto
    {
        public string? DeckName { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Request để tạo AI practice quiz từ Case đã chọn.
    /// </summary>
    public class GeneratePracticeQuizFromCasesRequestDto
    {
        public List<AIQuizCaseInputDto> Cases { get; set; } = new();
        public int? QuestionCount { get; set; }
        public string? Difficulty { get; set; }
    }
}
