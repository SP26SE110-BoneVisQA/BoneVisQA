using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LecturerController : ControllerBase
{
    private readonly ILecturerService _lecturerService;

    public LecturerController(ILecturerService lecturerService)
    {
        _lecturerService = lecturerService;
    }

    [HttpPost("classes")]
    public async Task<ActionResult<ClassDto>> CreateClass([FromBody] CreateClassRequestDto request)
    {
        var result = await _lecturerService.CreateClassAsync(request);
        return Ok(result);
    }

    [HttpGet("classes")]
    public async Task<ActionResult<IReadOnlyList<ClassDto>>> GetClasses([FromQuery] Guid lecturerId)
    {
        var result = await _lecturerService.GetClassesForLecturerAsync(lecturerId);
        return Ok(result);
    }

    [HttpPost("classes/{classId:guid}/enroll")]
    public async Task<IActionResult> EnrollStudent(Guid classId, [FromBody] EnrollStudentRequestDto request)
    {
        var created = await _lecturerService.EnrollStudentAsync(classId, request.StudentId);
        if (!created)
        {
            return Conflict(new { message = "Student đã có trong lớp này." });
        }

        return NoContent();
    }

    [HttpPost("classes/{classId:guid}/announcements")]
    public async Task<ActionResult<AnnouncementDto>> CreateAnnouncement(Guid classId, [FromBody] CreateAnnouncementRequestDto request)
    {
        var result = await _lecturerService.CreateAnnouncementAsync(classId, request);
        return Ok(result);
    }

    //[HttpPost("classes/{classId:guid}/quizzes")]
    //public async Task<ActionResult<QuizDto>> CreateQuiz(Guid classId, [FromBody] CreateQuizRequestDto request)
    //{
    //    var result = await _lecturerService.CreateQuizAsync(classId, request);
    //    return Ok(result);
    //}

    [HttpGet("classes/{classId:guid}/stats")]
    public async Task<ActionResult<ClassStatsDto>> GetClassStats(Guid classId)
    {
        var result = await _lecturerService.GetClassStatsAsync(classId);
        return Ok(result);
    }
}

