using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer/dashboard")]
[Tags("Lecturer - Dashboard")]
[Authorize(Roles = "Lecturer")]
public class LecturerDashboardController : ControllerBase
{
    private readonly ILecturerDashboardService _lecturerDashboardService;

    public LecturerDashboardController(ILecturerDashboardService lecturerDashboardService)
    {
        _lecturerDashboardService = lecturerDashboardService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<LecturerDashboardStatsDto>> GetStats()
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _lecturerDashboardService.GetDashboardStatsAsync(lecturerId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Aggregated analytics for the lecturer: class performance, topic scores, top/bottom students.
    /// </summary>
    [HttpGet("analytics")]
    [ProducesResponseType(typeof(LecturerAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LecturerAnalyticsDto>> GetAnalytics()
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _lecturerDashboardService.GetAnalyticsAsync(lecturerId.Value);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    #region Student Progress

    /// <summary>
    /// Lấy tổng quan tiến độ học tập của tất cả sinh viên trong một lớp.
    /// </summary>
    [HttpGet("classes/{classId:guid}/student-progress")]
    [ProducesResponseType(typeof(StudentProgressSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentProgressSummaryDto>> GetClassStudentProgress(Guid classId)
    {
        try
        {
            var result = await _lecturerDashboardService.GetClassStudentProgressAsync(classId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy chi tiết tiến độ học tập của một sinh viên cụ thể.
    /// </summary>
    [HttpGet("classes/{classId:guid}/students/{studentId:guid}/progress")]
    [ProducesResponseType(typeof(StudentProgressDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentProgressDetailDto>> GetStudentProgressDetail(Guid classId, Guid studentId)
    {
        var result = await _lecturerDashboardService.GetStudentProgressDetailAsync(classId, studentId);
        if (result == null)
            return NotFound(new { message = "Student or class not found." });
        return Ok(result);
    }

    /// <summary>
    /// Lấy tổng quan competency của cả lớp (phân bố điểm, topic yếu/mạnh).
    /// </summary>
    [HttpGet("classes/{classId:guid}/competency-overview")]
    [ProducesResponseType(typeof(ClassCompetencyOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassCompetencyOverviewDto>> GetClassCompetencyOverview(Guid classId)
    {
        try
        {
            var result = await _lecturerDashboardService.GetClassCompetencyOverviewAsync(classId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy mastery theo topic cho cả lớp.
    /// </summary>
    [HttpGet("classes/{classId:guid}/topics-mastery")]
    [ProducesResponseType(typeof(List<TopicMasteryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<TopicMasteryDto>>> GetClassTopicsMastery(Guid classId)
    {
        try
        {
            var result = await _lecturerDashboardService.GetClassTopicsMasteryAsync(classId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy bảng xếp hạng học sinh trong lớp.
    /// </summary>
    [HttpGet("monitoring/class-leaderboard")]
    [ProducesResponseType(typeof(List<ClassLeaderboardItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ClassLeaderboardItemDto>>> GetClassLeaderboard([FromQuery] Guid classId)
    {
        try
        {
            var lecturerId = GetUserId();
            if (lecturerId == null)
                return Unauthorized(new { message = "Token does not contain a valid user id." });

            var result = await _lecturerDashboardService.GetClassLeaderboardAsync(lecturerId.Value, classId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    #endregion
}
