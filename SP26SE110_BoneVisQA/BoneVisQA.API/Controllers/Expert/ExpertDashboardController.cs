using System.Security.Claims;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Expert;

[ApiController]
[Route("api/expert/dashboard")]
[Tags("Expert - Dashboard")]
[Authorize(Roles = "Expert")]

public class ExpertDashboardController : ControllerBase
{
    private readonly IExpertDashboardService _expertDashboardService;

    public ExpertDashboardController(IExpertDashboardService expertDashboardService)
    {
        _expertDashboardService = expertDashboardService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<ExpertDashboardStatsDto>> GetStats()
    {
        var expertId = GetUserId();
        if (expertId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _expertDashboardService.GetDashboardStatsAsync(expertId.Value);
        return Ok(result);
    }

    [HttpGet("pending-reviews")]
    public async Task<ActionResult<IReadOnlyList<ExpertDashboardPendingReviewDto>>> GetPendingReviews()
    {
        var expertId = GetUserId();
        if (expertId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _expertDashboardService.GetPendingReviewsAsync(expertId.Value);
        return Ok(result);
    }

    [HttpGet("recent-cases")]
    public async Task<ActionResult<IReadOnlyList<ExpertDashboardRecentCaseDto>>> GetRecentCases()
    {
        var expertId = GetUserId();
        if (expertId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _expertDashboardService.GetRecentCasesAsync(expertId.Value);
        return Ok(result);
    }

    [HttpGet("activity")]
    public async Task<ActionResult<ExpertDashboardActivityDto>> GetActivity()
    {
        var expertId = GetUserId();
        if (expertId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _expertDashboardService.GetActivityAsync(expertId.Value);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
