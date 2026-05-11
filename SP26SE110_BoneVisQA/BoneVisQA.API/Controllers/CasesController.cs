using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/cases")]
[Tags("Cases")]
[Authorize(Roles = "Student")]
public class CasesController : ControllerBase
{
    private readonly IStudentService _studentService;

    public CasesController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpGet("filters")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CaseCatalogFiltersDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CaseCatalogFiltersDto>> GetCatalogFilters(CancellationToken cancellationToken)
    {
        var result = await _studentService.GetCaseCatalogFiltersAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Public endpoint - allows anyone to browse the case catalog without authentication.
    /// </summary>
    [HttpGet("catalog")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<CaseListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetPublicCatalog(
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
            Difficulty = difficulty,
            Q = !string.IsNullOrWhiteSpace(q) ? q : search
        };
        var result = await _studentService.GetCaseCatalogAsync(filter);
        return Ok(result);
    }
}
