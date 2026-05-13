using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer/gradebook")]
[Tags("Lecturer - Gradebook")]
[Authorize(Roles = "Lecturer")]
public class LecturerGradebookController : ControllerBase
{
    private readonly ILecturerGradeBookService _gradeBookService;

    public LecturerGradebookController(ILecturerGradeBookService gradeBookService)
    {
        _gradeBookService = gradeBookService;
    }

    private Guid? GetLecturerId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    /// <summary>
    /// Get gradebook for a specific class.
    /// </summary>
    [HttpGet("classes/{classId:guid}")]
    public async Task<IActionResult> GetClassGradeBook(Guid classId)
    {
        try
        {
            var gradebook = await _gradeBookService.GetClassGradeBookAsync(classId);
            return Ok(gradebook);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get grade summary (distribution) for a class.
    /// </summary>
    [HttpGet("classes/{classId:guid}/summary")]
    public async Task<IActionResult> GetGradeSummary(Guid classId)
    {
        var summary = await _gradeBookService.GetGradeSummaryAsync(classId);
        return Ok(summary);
    }

    /// <summary>
    /// Get all student grades for a class.
    /// </summary>
    [HttpGet("classes/{classId:guid}/students")]
    public async Task<IActionResult> GetAllStudentGrades(Guid classId)
    {
        var grades = await _gradeBookService.GetAllStudentGradesAsync(classId);
        return Ok(grades);
    }

    /// <summary>
    /// Get grade for a specific student in a class.
    /// </summary>
    [HttpGet("classes/{classId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> GetStudentGrade(Guid classId, Guid studentId)
    {
        var grade = await _gradeBookService.GetStudentGradeAsync(classId, studentId);
        if (grade == null)
            return NotFound(new { message = "Student not found in this class." });
        return Ok(grade);
    }

    /// <summary>
    /// Export gradebook data for a class (structured format for frontend to render).
    /// </summary>
    [HttpGet("classes/{classId:guid}/export")]
    public async Task<IActionResult> ExportGradeBook(Guid classId)
    {
        try
        {
            var export = await _gradeBookService.ExportGradeBookAsync(classId);
            return Ok(export);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update student grade (override).
    /// </summary>
    [HttpPut("classes/{classId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> UpdateStudentGrade(
        Guid classId,
        Guid studentId,
        [FromBody] UpdateStudentGradeRequestDto request)
    {
        var updated = await _gradeBookService.UpdateStudentGradeAsync(classId, studentId, request);
        if (!updated)
            return NotFound(new { message = "Student not found or update failed." });
        return Ok(new { message = "Grade updated successfully." });
    }
}
