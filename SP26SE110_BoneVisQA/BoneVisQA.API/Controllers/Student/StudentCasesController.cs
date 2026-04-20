using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Student;

[ApiController]
[Route("api/student/cases")]
[Tags("Student - Cases")]
[Authorize(Roles = "Student")]
public class StudentCasesController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentCasesController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    /// <summary>
    /// Returns the published case catalog/library for students with optional filters.
    /// </summary>
    [HttpGet("catalog")]
    [ProducesResponseType(typeof(IReadOnlyList<CaseListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetCatalog(
        [FromQuery] string? location,
        [FromQuery] string? lesionType,
        [FromQuery] string? difficulty,
        [FromQuery] string? q,
        [FromQuery] string? search)
    {
        var filter = new CaseFilterRequestDto
        {
            Location = location,
            LesionType = lesionType,
            LessonType = lesionType,
            Difficulty = difficulty,
            Q = !string.IsNullOrWhiteSpace(q) ? q : search
        };

        var result = await _studentService.GetCaseCatalogAsync(filter);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetCases()
    {
        var result = await _studentService.GetCaseCatalogAsync();
        return Ok(result);
    }

    [HttpGet("filter")]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetFilteredCases([FromQuery] CaseFilterRequestDto filter)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _studentService.GetFilteredCasesAsync(studentId.Value, filter);
        return Ok(result);
    }

    /// <summary>
    /// Returns the authenticated student's personal case interaction history, including latest answer status.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<StudentCaseHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<StudentCaseHistoryItemDto>>> GetHistory()
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _studentService.GetCaseHistoryAsync(studentId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single case detail in catalog detail shape (images, expert summary, key findings).
    /// </summary>
    [HttpGet("{caseId:guid}")]
    [ProducesResponseType(typeof(CaseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaseDetailDto>> GetCaseDetail(Guid caseId)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _studentService.GetCaseDetailAsync(caseId, studentId.Value);
        return result == null
            ? NotFound(new { message = "Medical case not found." })
            : Ok(result);
    }

    [HttpPost("annotations")]
    public async Task<ActionResult<AnnotationDto>> CreateAnnotation([FromBody] CreateAnnotationRequestDto request)
    {
        var studentId = GetUserId();
        if (studentId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var result = await _studentService.CreateAnnotationAsync(studentId.Value, request);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
