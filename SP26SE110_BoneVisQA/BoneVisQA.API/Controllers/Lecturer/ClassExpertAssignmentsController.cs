using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Lecturer;

[Authorize(Roles = "Lecturer,Admin")]
[ApiController]
[Route("api/class-expert-assignments")]
[Tags("Class Expert Assignments")]
public class ClassExpertAssignmentsController : ControllerBase
{
    private readonly IClassExpertAssignmentService _classExpertAssignmentService;

    public ClassExpertAssignmentsController(IClassExpertAssignmentService classExpertAssignmentService)
    {
        _classExpertAssignmentService = classExpertAssignmentService;
    }

    /// <summary>Get all expert assignments for a class</summary>
    [HttpGet("class/{classId:guid}")]
    [ProducesResponseType(typeof(List<ClassExpertAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByClass(Guid classId)
    {
        var result = await _classExpertAssignmentService.GetByClassAsync(classId);
        return Ok(result);
    }

    /// <summary>Get all class assignments for an expert</summary>
    [HttpGet("expert/{expertId:guid}")]
    [Authorize(Roles = "Expert,Admin")]
    [ProducesResponseType(typeof(List<ClassExpertAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByExpert(Guid expertId)
    {
        var result = await _classExpertAssignmentService.GetByExpertAsync(expertId);
        return Ok(result);
    }

    /// <summary>Get assignment by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClassExpertAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _classExpertAssignmentService.GetByIdAsync(id);
        if (result == null)
            return NotFound(new { message = "Assignment not found." });
        return Ok(result);
    }

    /// <summary>Assign an expert to a class</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClassExpertAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] ClassExpertAssignmentCreateDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Request body is required." });

        if (dto.ClassId == Guid.Empty || dto.ExpertId == Guid.Empty || dto.BoneSpecialtyId == Guid.Empty)
            return BadRequest(new { message = "ClassId, ExpertId, and BoneSpecialtyId are required." });

        try
        {
            var result = await _classExpertAssignmentService.CreateAsync(dto);
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

    /// <summary>Update expert assignment</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ClassExpertAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] ClassExpertAssignmentUpdateDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Request body is required." });

        if (dto.Id == Guid.Empty)
            return BadRequest(new { message = "Id is required." });

        var result = await _classExpertAssignmentService.UpdateAsync(dto);
        if (result == null)
            return NotFound(new { message = "Assignment not found." });
        return Ok(result);
    }

    /// <summary>Remove expert from class (soft delete)</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _classExpertAssignmentService.DeleteAsync(id);
        if (!result)
            return NotFound(new { message = "Assignment not found." });
        return NoContent();
    }
}
