using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/analytics/lecturer")]
[Tags("Lecturer Analytics")]
[Authorize(Roles = "Lecturer,Admin")]
public class LecturerAnalyticsController : ControllerBase
{
    private readonly LecturerAnalyticsService _lecturerAnalyticsService;
    private readonly IUnitOfWork _unitOfWork;

    public LecturerAnalyticsController(LecturerAnalyticsService lecturerAnalyticsService, IUnitOfWork unitOfWork)
    {
        _lecturerAnalyticsService = lecturerAnalyticsService;
        _unitOfWork = unitOfWork;
    }

    private Guid GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out var userId) ? userId : Guid.Empty;
    }

    [HttpGet("class/{classId}/overview")]
    public async Task<IActionResult> GetClassOverview(Guid classId)
    {
        var overview = await _lecturerAnalyticsService.GetClassAnalyticsOverviewAsync(classId);
        if (overview == null) return NotFound();
        return Ok(overview);
    }

    [HttpGet("class/{classId}/students")]
    public async Task<IActionResult> GetClassStudents(Guid classId)
    {
        var students = await _lecturerAnalyticsService.GetClassStudentAnalyticsAsync(classId);
        return Ok(students);
    }

    [HttpGet("student/{studentId}")]
    public async Task<IActionResult> GetStudentAnalytics(Guid studentId)
    {
        var analytics = await _lecturerAnalyticsService.GetStudentAnalyticsDetailAsync(studentId);
        if (analytics == null) return NotFound();
        return Ok(analytics);
    }

    [HttpGet("class/{classId}/at-risk")]
    public async Task<IActionResult> GetAtRiskStudents(Guid classId)
    {
        var students = await _lecturerAnalyticsService.GetAtRiskStudentsAsync(classId);
        return Ok(students);
    }

    [HttpGet("class/{classId}/error-distribution")]
    public async Task<IActionResult> GetErrorDistribution(Guid classId)
    {
        var distribution = await _lecturerAnalyticsService.GetClassErrorDistributionAsync(classId);
        return Ok(distribution);
    }

    [HttpGet("class/{classId}/competency-matrix")]
    public async Task<IActionResult> GetCompetencyMatrix(Guid classId)
    {
        var matrix = await _lecturerAnalyticsService.GetCompetencyMatrixAsync(classId);
        return Ok(matrix);
    }

    [HttpGet("my-classes")]
    public async Task<IActionResult> GetMyClasses()
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var classes = await _unitOfWork.AcademicClassRepository
            .GetQueryable()
            .Where(c => c.LecturerId == userId)
            .ToListAsync();

        return Ok(classes.Select(c => new
        {
            id = c.Id,
            className = c.ClassName,
            semester = c.Semester,
            studentCount = c.ClassEnrollments?.Count ?? 0
        }));
    }
}
