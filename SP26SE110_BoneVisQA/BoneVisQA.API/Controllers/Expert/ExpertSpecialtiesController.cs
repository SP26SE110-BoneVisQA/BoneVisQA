using System.Security.Claims;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Expert;

[Authorize(Roles = "Expert,Admin")]
[ApiController]
[Route("api/expert-specialties")]
[Tags("Expert Specialties")]
public class ExpertSpecialtiesController : ControllerBase
{
    private readonly IExpertSpecialtyService _expertSpecialtyService;

    public ExpertSpecialtiesController(IExpertSpecialtyService expertSpecialtyService)
    {
        _expertSpecialtyService = expertSpecialtyService;
    }

    private Guid GetCurrentExpertId()
    {
        var expertIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(expertIdStr, out var expertId) || expertId == Guid.Empty)
            throw new UnauthorizedAccessException("Token does not contain a valid user id.");
        return expertId;
    }

    /// <summary>Get all specialties of current expert</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(List<ExpertSpecialtyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySpecialties()
    {
        var expertId = GetCurrentExpertId();
        var result = await _expertSpecialtyService.GetMySpecialtiesAsync(expertId);
        return Ok(result);
    }

    /// <summary>Get all specialties of all experts (for Admin)</summary>
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(List<ExpertSpecialtyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSpecialties()
    {
        var result = await _expertSpecialtyService.GetAllSpecialtiesAsync();
        return Ok(result);
    }

    /// <summary>Get specialty by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExpertSpecialtyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _expertSpecialtyService.GetByIdAsync(id);
        if (result == null)
            return NotFound(new { message = "Expert specialty not found." });
        return Ok(result);
    }

    /// <summary>Create new expert specialty</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExpertSpecialtyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] ExpertSpecialtyCreateDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Request body is required." });

        if (dto.BoneSpecialtyId == Guid.Empty)
            return BadRequest(new { message = "BoneSpecialtyId is required." });

        if (dto.ProficiencyLevel < 1 || dto.ProficiencyLevel > 5)
            return BadRequest(new { message = "ProficiencyLevel must be between 1 and 5." });

        try
        {
            var expertId = GetCurrentExpertId();
            var result = await _expertSpecialtyService.CreateAsync(expertId, dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Update expert specialty</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ExpertSpecialtyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] ExpertSpecialtyUpdateDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Request body is required." });

        if (dto.Id == Guid.Empty)
            return BadRequest(new { message = "Id is required." });

        try
        {
            var expertId = GetCurrentExpertId();
            var result = await _expertSpecialtyService.UpdateAsync(expertId, dto);
            if (result == null)
                return NotFound(new { message = "Expert specialty not found." });
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Delete expert specialty (soft delete)</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var expertId = GetCurrentExpertId();
        var result = await _expertSpecialtyService.DeleteAsync(expertId, id);
        if (!result)
            return NotFound(new { message = "Expert specialty not found." });
        return NoContent();
    }

    /// <summary>Get suggested experts by bone specialty (for lecturers)</summary>
    [HttpGet("suggest")]
    [Authorize(Roles = "Lecturer,Admin")]
    [ProducesResponseType(typeof(List<ExpertSuggestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggestedExperts(
        [FromQuery] Guid boneSpecialtyId,
        [FromQuery] Guid? pathologyCategoryId = null)
    {
        if (boneSpecialtyId == Guid.Empty)
            return BadRequest(new { message = "boneSpecialtyId is required." });

        var result = await _expertSpecialtyService.GetSuggestedExpertsAsync(boneSpecialtyId, pathologyCategoryId);
        return Ok(result);
    }
}
