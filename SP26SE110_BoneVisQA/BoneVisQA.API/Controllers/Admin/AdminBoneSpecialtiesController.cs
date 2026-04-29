using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/bone-specialties")]
[Tags("Admin - Bone Specialties")]
public class AdminBoneSpecialtiesController : ControllerBase
{
    private readonly IBoneSpecialtyService _boneSpecialtyService;

    public AdminBoneSpecialtiesController(IBoneSpecialtyService boneSpecialtyService)
    {
        _boneSpecialtyService = boneSpecialtyService;
    }

    /// <summary>Get all bone specialties (flat or hierarchical)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<BoneSpecialtyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? parentId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool flat = true)
    {
        var query = new BoneSpecialtyQueryDto
        {
            ParentId = parentId,
            IsActive = isActive,
            FlatList = flat
        };

        var result = flat
            ? await _boneSpecialtyService.GetAllAsync(query)
            : await _boneSpecialtyService.GetTreeAsync();

        return Ok(result);
    }

    /// <summary>Get bone specialty tree (hierarchical)</summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(List<BoneSpecialtyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree()
    {
        var result = await _boneSpecialtyService.GetTreeAsync();
        return Ok(result);
    }

    /// <summary>Get bone specialty by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BoneSpecialtyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _boneSpecialtyService.GetByIdAsync(id);
        if (result == null)
            return NotFound(new { message = "Bone specialty not found." });
        return Ok(result);
    }

    /// <summary>Create a new bone specialty</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BoneSpecialtyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] BoneSpecialtyCreateDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Request body is required." });

        try
        {
            var result = await _boneSpecialtyService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Update a bone specialty</summary>
    [HttpPut]
    [ProducesResponseType(typeof(BoneSpecialtyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] BoneSpecialtyUpdateDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Request body is required." });

        try
        {
            var result = await _boneSpecialtyService.UpdateAsync(dto);
            if (result == null)
                return NotFound(new { message = "Bone specialty not found." });
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Delete a bone specialty</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var result = await _boneSpecialtyService.DeleteAsync(id);
            if (!result)
                return NotFound(new { message = "Bone specialty not found." });
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Toggle active status of a bone specialty</summary>
    [HttpPatch("{id:guid}/toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid id, [FromQuery] bool isActive = true)
    {
        var result = await _boneSpecialtyService.ToggleActiveAsync(id, isActive);
        if (!result)
            return NotFound(new { message = "Bone specialty not found." });
        return NoContent();
    }
}
