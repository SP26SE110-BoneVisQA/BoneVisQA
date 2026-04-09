using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer/monitoring")]
[Tags("Lecturer - Monitoring")]
[Authorize(Roles = "Lecturer")]
public class LecturerMonitoringController : ControllerBase
{
    private readonly ILecturerDashboardService _lecturerDashboardService;

    public LecturerMonitoringController(ILecturerDashboardService lecturerDashboardService)
    {
        _lecturerDashboardService = lecturerDashboardService;
    }

    /// <summary>
    /// Returns a class leaderboard ranked by quiz performance for the selected lecturer-owned class.
    /// </summary>
    [HttpGet("class-leaderboard")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassLeaderboardItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ClassLeaderboardItemDto>>> GetClassLeaderboard([FromQuery] Guid classId)
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            var result = await _lecturerDashboardService.GetClassLeaderboardAsync(lecturerId.Value, classId);
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
