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
    [ProducesResponseType(typeof(CaseCatalogFiltersDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CaseCatalogFiltersDto>> GetCatalogFilters(CancellationToken cancellationToken)
    {
        var result = await _studentService.GetCaseCatalogFiltersAsync(cancellationToken);
        return Ok(result);
    }
}
