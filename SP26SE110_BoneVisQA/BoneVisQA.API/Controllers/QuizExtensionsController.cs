using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Services.QuizExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/quiz-extensions")]
[Tags("Quiz Extensions")]
[Authorize]
public class QuizExtensionsController : ControllerBase
{
    private readonly QuizReviewService _reviewService;
    private readonly SpacedRepetitionService _spacedRepetitionService;
    private readonly AdaptiveQuizService _adaptiveQuizService;
    private readonly IUnitOfWork _unitOfWork;

    public QuizExtensionsController(
        QuizReviewService reviewService,
        SpacedRepetitionService spacedRepetitionService,
        AdaptiveQuizService adaptiveQuizService,
        IUnitOfWork unitOfWork)
    {
        _reviewService = reviewService;
        _spacedRepetitionService = spacedRepetitionService;
        _adaptiveQuizService = adaptiveQuizService;
        _unitOfWork = unitOfWork;
    }

    private Guid GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out var userId) ? userId : Guid.Empty;
    }

    #region Detailed Review

    [HttpGet("review/{attemptId}/detailed")]
    public async Task<IActionResult> GetDetailedReview(Guid attemptId)
    {
        var review = await _reviewService.GetDetailedReviewAsync(attemptId);
        if (review == null) return NotFound();
        return Ok(review);
    }

    [HttpPost("review/{attemptId}/generate")]
    public async Task<IActionResult> GenerateReviewItems(Guid attemptId, [FromBody] string? aiExplanations = null)
    {
        await _reviewService.GenerateReviewItemsAsync(attemptId, aiExplanations);
        return Ok(new { message = "Review items generated" });
    }

    [HttpGet("review/{attemptId}/explanation/{questionId}")]
    public async Task<IActionResult> GetQuestionExplanation(Guid attemptId, Guid questionId)
    {
        var items = await _reviewService.GetReviewItemsAsync(attemptId);
        var item = items.Find(i => i.QuestionId == questionId);
        if (item == null) return NotFound();
        return Ok(new { explanation = item.AiExplanation });
    }

    [HttpPut("review/{reviewItemId}/explanation")]
    public async Task<IActionResult> UpdateExplanation(Guid reviewItemId, [FromBody] string explanation)
    {
        await _reviewService.UpdateAiExplanationAsync(reviewItemId, explanation);
        return Ok();
    }

    #endregion

    #region Spaced Repetition

    [HttpGet("spaced-repetition/due")]
    public async Task<IActionResult> GetDueReviews([FromQuery] int limit = 20)
    {
        var studentId = GetCurrentUserId();
        if (studentId == Guid.Empty) return Unauthorized();

        var reviews = await _spacedRepetitionService.GetDueReviewsAsync(studentId, limit);
        return Ok(reviews);
    }

    [HttpGet("spaced-repetition/stats")]
    public async Task<IActionResult> GetSpacedRepetitionStats()
    {
        var studentId = GetCurrentUserId();
        if (studentId == Guid.Empty) return Unauthorized();

        var stats = await _spacedRepetitionService.GetStatsAsync(studentId);
        return Ok(stats);
    }

    [HttpPost("spaced-repetition/schedule")]
    public async Task<IActionResult> ScheduleReview([FromBody] ScheduleReviewRequest request)
    {
        await _spacedRepetitionService.ScheduleReviewAsync(
            request.StudentId,
            request.CaseId,
            request.QuizId,
            request.QuestionId,
            request.WasCorrect);
        return Ok();
    }

    [HttpPost("spaced-repetition/review")]
    public async Task<IActionResult> SubmitReview([FromBody] SubmitReviewRequest request)
    {
        await _spacedRepetitionService.UpdateReviewAsync(request.ScheduleId, request.Quality);
        return Ok();
    }

    [HttpDelete("spaced-repetition/{scheduleId}")]
    public async Task<IActionResult> DeleteReview(Guid scheduleId)
    {
        await _spacedRepetitionService.DeleteReviewAsync(scheduleId);
        return Ok();
    }

    #endregion

    #region Adaptive Quiz

    [HttpGet("adaptive/{quizId}/preview")]
    public async Task<IActionResult> GetAdaptiveQuizPreview(Guid quizId)
    {
        var studentId = GetCurrentUserId();
        if (studentId == Guid.Empty) return Unauthorized();

        var preview = await _adaptiveQuizService.GetQuizPreviewAsync(quizId, studentId);
        if (preview == null) return NotFound();
        return Ok(preview);
    }

    [HttpGet("adaptive/{attemptId}/next-questions")]
    public async Task<IActionResult> GetNextQuestions(Guid attemptId, [FromQuery] int count = 1)
    {
        var questions = await _adaptiveQuizService.GetNextQuestionsForAdaptiveQuizAsync(attemptId, count);
        return Ok(questions);
    }

    [HttpPost("adaptive/{attemptId}/answer")]
    public async Task<IActionResult> SubmitAdaptiveAnswer(Guid attemptId, [FromBody] AdaptiveAnswerRequest request)
    {
        await _adaptiveQuizService.AdjustDifficultyAfterAnswerAsync(attemptId, request.WasCorrect);
        
        if (request.SpacedRepetitionEnabled)
        {
            await _spacedRepetitionService.ScheduleReviewAsync(
                request.StudentId,
                request.CaseId,
                request.QuizId,
                request.QuestionId,
                request.WasCorrect);
        }

        return Ok(new { newDifficulty = request.WasCorrect ? "Hard" : "Easy" });
    }

    [HttpPost("quiz/{quizId}/enable-adaptive")]
    [Authorize(Roles = "Lecturer,Admin,Expert")]
    public async Task<IActionResult> EnableAdaptiveMode(Guid quizId)
    {
        var success = await _adaptiveQuizService.EnableAdaptiveModeAsync(quizId);
        return success ? Ok() : NotFound();
    }

    [HttpPost("quiz/{quizId}/enable-spaced-repetition")]
    [Authorize(Roles = "Lecturer,Admin,Expert")]
    public async Task<IActionResult> EnableSpacedRepetition(Guid quizId)
    {
        await _adaptiveQuizService.EnableSpacedRepetitionAsync(quizId);
        return Ok();
    }

    #endregion
}

public class ScheduleReviewRequest
{
    public Guid StudentId { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? QuizId { get; set; }
    public Guid? QuestionId { get; set; }
    public bool WasCorrect { get; set; }
}

public class SubmitReviewRequest
{
    public Guid ScheduleId { get; set; }
    public int Quality { get; set; }
}

public class AdaptiveAnswerRequest
{
    public Guid StudentId { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? QuizId { get; set; }
    public Guid? QuestionId { get; set; }
    public bool WasCorrect { get; set; }
    public bool SpacedRepetitionEnabled { get; set; }
}
