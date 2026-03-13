using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Lecturer")]
public class LecturersController : ControllerBase
{
    private readonly ILecturerService _lecturerService;

    public LecturersController(ILecturerService lecturerService)
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

    [HttpPost("classes/{classId:guid}/enrollmany")]
    public async Task<ActionResult<IReadOnlyList<StudentEnrollmentDto>>> EnrollManyStudents(Guid classId, [FromBody] EnrollStudentsRequestDto request)
    {
        var result = await _lecturerService.EnrollStudentsAsync(classId, request);
        return Ok(result);
    }

    [HttpDelete("classes/{classId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> RemoveStudent(Guid classId, Guid studentId)
    {
        var removed = await _lecturerService.RemoveStudentAsync(classId, studentId);
        if (!removed)
        {
            return NotFound(new { message = "Student không tồn tại trong lớp này." });
        }

        return NoContent();
    }

    [HttpGet("classes/{classId:guid}/students")]
    public async Task<ActionResult<IReadOnlyList<StudentEnrollmentDto>>> GetStudentsInClass(Guid classId)
    {
        var result = await _lecturerService.GetStudentsInClassAsync(classId);
        return Ok(result);
    }

    [HttpGet("classes/{classId:guid}/students/available")]
    public async Task<ActionResult<IReadOnlyList<StudentEnrollmentDto>>> GetAvailableStudents(Guid classId)
    {
        var result = await _lecturerService.GetAvailableStudentsAsync(classId);
        return Ok(result);
    }

    [HttpPost("classes/{classId:guid}/announcements")]
    public async Task<ActionResult<AnnouncementDto>> CreateAnnouncement(Guid classId, [FromBody] CreateAnnouncementRequestDto request)
    {
        var result = await _lecturerService.CreateAnnouncementAsync(classId, request);
        return Ok(result);
    }

    [HttpPost("classes/{classId:guid}/quizzes")]
    public async Task<ActionResult<QuizDto>> CreateQuiz(Guid classId, [FromBody] CreateQuizRequestDto request)
    {
        var result = await _lecturerService.CreateQuizAsync(classId, request);
        return Ok(result);
    }

    [HttpGet("classes/{classId:guid}/stats")]
    public async Task<ActionResult<ClassStatsDto>> GetClassStats(Guid classId)
    {
        var result = await _lecturerService.GetClassStatsAsync(classId);
        return Ok(result);
    }

    [HttpPost("quizzes/{quizId:guid}/questions")]
    public async Task<ActionResult<QuizQuestionDto>> AddQuizQuestion(Guid quizId, [FromBody] CreateQuizQuestionRequestDto request)
    {
        request.QuizId = quizId;
        var result = await _lecturerService.AddQuizQuestionAsync(request);
        return Ok(result);
    }

    [HttpGet("quizzes/{quizId:guid}/questions")]
    public async Task<ActionResult<IReadOnlyList<QuizQuestionDto>>> GetQuizQuestions(Guid quizId)
    {
        var result = await _lecturerService.GetQuizQuestionsAsync(quizId);
        return Ok(result);
    }

    [HttpPut("quizzes/questions/{questionId:guid}")]
    public async Task<IActionResult> UpdateQuizQuestion(Guid questionId, [FromBody] UpdateQuizQuestionRequestDto request)
    {
        var updated = await _lecturerService.UpdateQuizQuestionAsync(questionId, request);
        if (!updated)
        {
            return NotFound(new { message = "Câu hỏi không tồn tại." });
        }
        return NoContent();
    }

    [HttpDelete("quizzes/questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteQuizQuestion(Guid questionId)
    {
        var deleted = await _lecturerService.DeleteQuizQuestionAsync(questionId);
        if (!deleted)
        {
            return NotFound(new { message = "Câu hỏi không tồn tại." });
        }
        return NoContent();
    }

    [HttpGet("cases")]
    public async Task<ActionResult<IReadOnlyList<CaseDto>>> GetAllCases()
    {
        var result = await _lecturerService.GetAllCasesAsync();
        return Ok(result);
    }

    [HttpPost("classes/{classId:guid}/cases")]
    public async Task<ActionResult<IReadOnlyList<CaseDto>>> AssignCasesToClass(Guid classId, [FromBody] AssignCasesToClassRequestDto request)
    {
        var result = await _lecturerService.AssignCasesToClassAsync(classId, request);
        return Ok(result);
    }

    [HttpPut("cases/{caseId:guid}/approve")]
    public async Task<IActionResult> ApproveCase(Guid caseId, [FromBody] ApproveCaseRequestDto request)
    {
        var updated = await _lecturerService.ApproveCaseAsync(caseId, request);
        if (!updated)
        {
            return NotFound(new { message = "Case không tồn tại." });
        }
        return NoContent();
    }

    [HttpGet("classes/{classId:guid}/questions")]
    public async Task<ActionResult<IReadOnlyList<LectStudentQuestionDto>>> GetStudentQuestions(Guid classId, [FromQuery] Guid? caseId, [FromQuery] Guid? studentId)
    {
        var result = await _lecturerService.GetStudentQuestionsAsync(classId, caseId, studentId);
        return Ok(result);
    }

    [HttpGet("classes/{classId:guid}/announcements")]
    public async Task<ActionResult<IReadOnlyList<AnnouncementDto>>> GetClassAnnouncements(Guid classId)
    {
        var result = await _lecturerService.GetClassAnnouncementsAsync(classId);
        return Ok(result);
    }
}

