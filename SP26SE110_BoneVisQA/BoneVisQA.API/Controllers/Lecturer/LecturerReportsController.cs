using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer/reports")]
[Tags("Lecturer - Reports")]
[Authorize(Roles = "Lecturer")]
public class LecturerReportsController : ControllerBase
{
    private readonly ILecturerReportService _reportService;

    public LecturerReportsController(ILecturerReportService reportService)
    {
        _reportService = reportService;
    }

    private Guid? GetLecturerId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    /// <summary>
    /// Get overall report for all classes of the lecturer.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverallReport()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var report = await _reportService.GetOverallReportAsync(lecturerId.Value);
        return Ok(report);
    }

    /// <summary>
    /// Get summary report for all classes.
    /// </summary>
    [HttpGet("classes")]
    public async Task<IActionResult> GetClassesReport()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token does not contain a valid user id." });

        var reports = await _reportService.GetClassesReportAsync(lecturerId.Value);
        return Ok(reports);
    }

    /// <summary>
    /// Get detailed report for a specific class.
    /// </summary>
    [HttpGet("classes/{classId:guid}")]
    public async Task<IActionResult> GetClassDetailedReport(Guid classId)
    {
        try
        {
            var report = await _reportService.GetClassDetailedReportAsync(classId);
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get quiz reports for a class.
    /// </summary>
    [HttpGet("classes/{classId:guid}/quizzes")]
    public async Task<IActionResult> GetClassQuizReports(Guid classId)
    {
        var reports = await _reportService.GetClassQuizReportsAsync(classId);
        return Ok(reports);
    }

    /// <summary>
    /// Get AI quality report for a class.
    /// </summary>
    [HttpGet("classes/{classId:guid}/ai-quality")]
    public async Task<IActionResult> GetAIQualityReport(Guid classId)
    {
        try
        {
            var report = await _reportService.GetAIQualityReportAsync(classId);
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get activity report for a class within a date range.
    /// </summary>
    [HttpGet("classes/{classId:guid}/activity")]
    public async Task<IActionResult> GetActivityReport(
        Guid classId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        try
        {
            var report = await _reportService.GetActivityReportAsync(classId, fromDate, toDate);
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get report for a specific student in a class.
    /// </summary>
    [HttpGet("classes/{classId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> GetStudentReport(Guid classId, Guid studentId)
    {
        var report = await _reportService.GetStudentReportAsync(classId, studentId);
        if (report == null)
            return NotFound(new { message = "Student not found in this class." });
        return Ok(report);
    }

    /// <summary>
    /// Get reports for all students in a class.
    /// </summary>
    [HttpGet("classes/{classId:guid}/students")]
    public async Task<IActionResult> GetClassStudentsReport(Guid classId)
    {
        var reports = await _reportService.GetClassStudentsReportAsync(classId);
        return Ok(reports);
    }

    /// <summary>
    /// Get report for a specific quiz session.
    /// </summary>
    [HttpGet("quizzes/{quizSessionId:guid}")]
    public async Task<IActionResult> GetQuizReport(Guid quizSessionId)
    {
        try
        {
            var report = await _reportService.GetQuizReportAsync(quizSessionId);
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
