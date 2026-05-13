using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Expert;

[ApiController]
[Route("api/expert/class")]
[Tags("Expert - Teaching Objectives")]
[Authorize(Roles = "Expert")]
public class ExpertTeachingObjectivesController : ControllerBase
{
    private readonly ITeachingObjectiveService _teachingObjectiveService;

    public ExpertTeachingObjectivesController(ITeachingObjectiveService teachingObjectiveService)
    {
        _teachingObjectiveService = teachingObjectiveService;
    }

    private Guid? GetExpertId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    /// <summary>
    /// Lấy Teaching Objectives của một lớp cụ thể mà Expert được phân công.
    /// </summary>
    [HttpGet("{classId:guid}/objectives")]
    [ProducesResponseType(typeof(ExpertTeachingObjectivesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpertTeachingObjectivesDto>> GetClassObjectives(Guid classId)
    {
        var expertId = GetExpertId();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _teachingObjectiveService.GetClassObjectivesForExpertAsync(expertId.Value, classId);
        if (result == null)
            return NotFound(new { message = "Class not found or you don't have access to this class." });
        return Ok(result);
    }

    /// <summary>
    /// Lấy Teaching Objectives của tất cả các lớp mà Expert được phân công.
    /// </summary>
    [HttpGet("objectives/all")]
    [ProducesResponseType(typeof(List<ExpertTeachingObjectivesDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExpertTeachingObjectivesDto>>> GetAllClassObjectives()
    {
        var expertId = GetExpertId();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _teachingObjectiveService.GetAssignedClassesObjectivesAsync(expertId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Expert đề xuất một Teaching Objective mới cho lớp học.
    /// </summary>
    [HttpPost("objectives/suggest")]
    [ProducesResponseType(typeof(TeachingObjectiveSuggestionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TeachingObjectiveSuggestionDto>> SuggestObjective([FromBody] SuggestObjectiveRequestDto request)
    {
        var expertId = GetExpertId();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        if (string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { message = "Topic is required." });

        if (string.IsNullOrWhiteSpace(request.Objective))
            return BadRequest(new { message = "Objective is required." });

        try
        {
            var result = await _teachingObjectiveService.SuggestObjectiveAsync(expertId.Value, request);
            return CreatedAtAction(nameof(GetClassObjectives), new { classId = request.ClassId }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy các đề xuất đang chờ của Expert hiện tại.
    /// </summary>
    [HttpGet("objectives/my-suggestions")]
    [ProducesResponseType(typeof(List<TeachingObjectiveSuggestionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TeachingObjectiveSuggestionDto>>> GetMyPendingSuggestions()
    {
        var expertId = GetExpertId();
        if (expertId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _teachingObjectiveService.GetMyPendingSuggestionsAsync(expertId.Value);
        return Ok(result);
    }
}
