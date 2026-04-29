using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/pathology-categories")]
[Tags("Admin - Pathology Categories")]
public class AdminPathologyCategoriesController : ControllerBase
{
    private readonly IPathologyCategoryService _pathologyCategoryService;

    public AdminPathologyCategoriesController(IPathologyCategoryService pathologyCategoryService)
    {
        _pathologyCategoryService = pathologyCategoryService;
    }

    /// <summary>Get all pathology categories</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<PathologyCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? boneSpecialtyId = null,
        [FromQuery] bool? isActive = null)
    {
        var query = new PathologyCategoryQueryDto
        {
            BoneSpecialtyId = boneSpecialtyId,
            IsActive = isActive
        };

        var result = await _pathologyCategoryService.GetAllAsync(query);
        return Ok(result);
    }

    /// <summary>Get pathology category by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PathologyCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _pathologyCategoryService.GetByIdAsync(id);
        if (result == null)
            return NotFound(new { message = "Pathology category not found." });
        return Ok(result);
    }

    /// <summary>Create a new pathology category</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PathologyCategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] PathologyCategoryCreateDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Request body is required." });

        try
        {
            var result = await _pathologyCategoryService.CreateAsync(dto);
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

    /// <summary>Update a pathology category</summary>
    [HttpPut]
    [ProducesResponseType(typeof(PathologyCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] PathologyCategoryUpdateDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Request body is required." });

        try
        {
            var result = await _pathologyCategoryService.UpdateAsync(dto);
            if (result == null)
                return NotFound(new { message = "Pathology category not found." });
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

    /// <summary>Delete a pathology category</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _pathologyCategoryService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = "Pathology category not found." });
        return NoContent();
    }

    /// <summary>Toggle active status of a pathology category</summary>
    [HttpPatch("{id:guid}/toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid id, [FromQuery] bool isActive = true)
    {
        var result = await _pathologyCategoryService.ToggleActiveAsync(id, isActive);
        if (!result)
            return NotFound(new { message = "Pathology category not found." });
        return NoContent();
    }
}
