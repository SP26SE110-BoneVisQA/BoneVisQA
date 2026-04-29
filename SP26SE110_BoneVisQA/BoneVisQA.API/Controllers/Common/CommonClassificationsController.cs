using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Common;

/// <summary>
/// Shared classification API accessible by all authenticated users (Admin, Lecturer, Expert).
/// This provides Bone Specialty tree and Pathology Categories for dropdowns.
/// </summary>
[Authorize]
[ApiController]
[Route("api/common/classifications")]
[Tags("Common - Classifications")]
public class CommonClassificationsController : ControllerBase
{
    private readonly IQuizsService _quizService;

    public CommonClassificationsController(IQuizsService quizService)
    {
        _quizService = quizService;
    }

    /// <summary>
    /// Get all Bone Specialties in tree (hierarchical) structure.
    /// Used for dropdown in Create/Edit Quiz forms.
    /// </summary>
    [HttpGet("bone-specialties/tree")]
    [ProducesResponseType(typeof(List<BoneSpecialtyTreeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBoneSpecialtiesTree()
    {
        var result = await _quizService.GetBoneSpecialtiesTreeAsync();
        return Ok(result);
    }

    /// <summary>
    /// Get all Pathology Categories in flat list.
    /// Used for dropdown in Create/Edit Quiz forms.
    /// </summary>
    [HttpGet("pathology-categories")]
    [ProducesResponseType(typeof(List<PathologyCategorySimpleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPathologyCategories()
    {
        var result = await _quizService.GetPathologyCategoriesAsync();
        return Ok(result);
    }
}
