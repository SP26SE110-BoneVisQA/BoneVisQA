using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/specialties")]
[Route("api/expert/specialties")]
[Tags("Shared - Specialties")]
[Authorize(Roles = "Admin,Lecturer,Expert")]
public class SpecialtiesController : ControllerBase
{
    private readonly ISpecialtyCatalogService _specialtyCatalogService;

    public SpecialtiesController(ISpecialtyCatalogService specialtyCatalogService)
    {
        _specialtyCatalogService = specialtyCatalogService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BoneSpecialtyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<BoneSpecialtyDto>>> GetBoneSpecialties(CancellationToken cancellationToken)
    {
        var items = await _specialtyCatalogService.GetBoneSpecialtiesAsync(cancellationToken);
        return Ok(items);
    }
}
