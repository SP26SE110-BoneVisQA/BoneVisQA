using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Student;

[ApiController]
[Route("api/student/progress")]
[Authorize(Roles = "Student")]
public class StudentProgressController : ControllerBase
{
    private readonly IStudentLearningService _studentLearningService;

    public StudentProgressController(IStudentLearningService studentLearningService)
    {
        _studentLearningService = studentLearningService;
    }

    [HttpGet]
    public async Task<ActionResult<StudentProgressDto>> GetProgress()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _studentLearningService.GetProgressSummaryAsync(studentId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Returns student progress grouped by inferred topic, including quiz accuracy and engagement counts.
    /// </summary>
    [HttpGet("topic-stats")]
    [ProducesResponseType(typeof(IReadOnlyList<StudentTopicStatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<StudentTopicStatDto>>> GetTopicStats()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _studentLearningService.GetTopicStatsAsync(studentId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Returns the latest 10 student activities across quizzes and question interactions.
    /// </summary>
    [HttpGet("recent-activity")]
    [ProducesResponseType(typeof(IReadOnlyList<StudentRecentActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<StudentRecentActivityDto>>> GetRecentActivity()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _studentLearningService.GetRecentActivityAsync(studentId.Value);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
