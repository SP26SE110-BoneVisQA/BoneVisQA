using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/analytics")]
[Tags("Analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly AnalyticsService _analyticsService;
    private readonly IUnitOfWork _unitOfWork;

    public AnalyticsController(AnalyticsService analyticsService, IUnitOfWork unitOfWork)
    {
        _analyticsService = analyticsService;
        _unitOfWork = unitOfWork;
    }

    private Guid GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out var userId) ? userId : Guid.Empty;
    }

    [HttpGet("student/competencies")]
    public async Task<IActionResult> GetStudentCompetencies()
    {
        var studentId = GetCurrentUserId();
        if (studentId == Guid.Empty) return Unauthorized();

        var competencies = await _analyticsService.GetStudentCompetenciesAsync(studentId);
        return Ok(competencies);
    }

    [HttpGet("student/error-patterns")]
    public async Task<IActionResult> GetStudentErrorPatterns()
    {
        var studentId = GetCurrentUserId();
        if (studentId == Guid.Empty) return Unauthorized();

        var patterns = await _analyticsService.GetStudentErrorPatternsAsync(studentId);
        return Ok(patterns);
    }

    [HttpGet("student/insights")]
    public async Task<IActionResult> GetStudentInsights()
    {
        var studentId = GetCurrentUserId();
        if (studentId == Guid.Empty) return Unauthorized();

        var insights = await _analyticsService.GetStudentInsightsAsync(studentId);
        return Ok(insights);
    }

    [HttpGet("student/dashboard")]
    public async Task<IActionResult> GetStudentDashboard()
    {
        var studentId = GetCurrentUserId();
        if (studentId == Guid.Empty) return Unauthorized();

        var competencies = await _analyticsService.GetStudentCompetenciesAsync(studentId);
        var errorPatterns = await _analyticsService.GetStudentErrorPatternsAsync(studentId);
        var insights = await _analyticsService.GetStudentInsightsAsync(studentId);

        var avgScore = competencies.Any() ? competencies.Average(c => c.Score) : 0;
        var weakTopics = competencies.Where(c => c.Score < 50).ToList();

        return Ok(new
        {
            competencies = competencies,
            errorPatterns = errorPatterns,
            insights = insights,
            summary = new
            {
                averageScore = avgScore,
                totalQuizzes = competencies.Sum(c => c.TotalAttempts),
                weakTopicCount = weakTopics.Count,
                activeErrorPatterns = errorPatterns.Count
            }
        });
    }

    [HttpPost("student/insights/{insightId}/read")]
    public async Task<IActionResult> MarkInsightAsRead(Guid insightId)
    {
        var success = await _analyticsService.MarkInsightAsReadAsync(insightId);
        return success ? Ok() : NotFound();
    }

    [HttpPost("student/insights/{insightId}/action")]
    public async Task<IActionResult> MarkInsightAsActionTaken(Guid insightId)
    {
        var success = await _analyticsService.MarkInsightAsActionTakenAsync(insightId);
        return success ? Ok() : NotFound();
    }

    [HttpPost("student/error-patterns/{patternId}/resolve")]
    public async Task<IActionResult> ResolveErrorPattern(Guid patternId)
    {
        var success = await _analyticsService.ResolveErrorPatternAsync(patternId);
        return success ? Ok() : NotFound();
    }

    [HttpPost("quiz-attempt/{attemptId}/analyze")]
    public async Task<IActionResult> AnalyzeQuizAttempt(Guid attemptId)
    {
        await _analyticsService.AnalyzeQuizAttemptAndUpdateAnalyticsAsync(attemptId);
        return Ok(new { message = "Analysis completed" });
    }
}
