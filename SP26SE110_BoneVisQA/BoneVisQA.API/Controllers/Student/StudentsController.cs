using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BoneVisQA.API.Controllers.Student;

[ApiController]
[Route("api/[controller]")]
[Tags("Student - Legacy")]
[Authorize(Roles = "Student")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    /// <summary>
    /// ID sinh viên chỉ lấy từ JWT (sub / NameIdentifier), không tin cậy query string để tránh IDOR.
    /// </summary>
    private bool TryGetAuthenticatedStudentId(out Guid studentId)
    {
        studentId = default;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return !string.IsNullOrWhiteSpace(userIdStr) && Guid.TryParse(userIdStr, out studentId);
    }

    private UnauthorizedObjectResult StudentIdentityRequired()
        => Unauthorized(new { message = "Không xác định được sinh viên từ access token." });

    [HttpGet("cases")]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetCases()
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.GetCasesAsync(studentId);
        return Ok(result);
    }

    [HttpGet("cases/filter")]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetFilteredCases([FromQuery] CaseFilterRequestDto filter)
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.GetFilteredCasesAsync(studentId, filter);
        return Ok(result);
    }

    [HttpGet("cases/{caseId:guid}")]
    public async Task<ActionResult<CaseDetailDto>> GetCaseDetail(Guid caseId)
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.GetCaseDetailAsync(caseId, studentId);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("annotations")]
    public async Task<ActionResult<AnnotationDto>> CreateAnnotation([FromBody] CreateAnnotationRequestDto request)
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.CreateAnnotationAsync(studentId, request);
        return Ok(result);
    }

    [HttpPost("questions")]
    public async Task<ActionResult<StudentQuestionDto>> AskQuestion([FromBody] AskQuestionRequestDto request)
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.AskQuestionAsync(studentId, request);
        return Ok(result);
    }

    [HttpGet("questions")]
    public async Task<ActionResult<IReadOnlyList<StudentQuestionHistoryItemDto>>> GetQuestionHistory()
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.GetQuestionHistoryAsync(studentId);
        return Ok(result);
    }

    [HttpGet("announcements")]
    public async Task<ActionResult<IReadOnlyList<StudentAnnouncementDto>>> GetAnnouncements()
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.GetAnnouncementsAsync(studentId);
        return Ok(result);
    }

    [HttpGet("quizzes")]
    public async Task<ActionResult<IReadOnlyList<QuizListItemDto>>> GetQuizzes()
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.GetAvailableQuizzesAsync(studentId);
        return Ok(result);
    }

    [HttpPost("quizzes/{quizId:guid}/start")]
    public async Task<ActionResult<QuizSessionDto>> StartQuiz(Guid quizId)
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.StartQuizAsync(studentId, quizId);
        return Ok(result);
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitAnswer([FromBody] StudentSubmitQuestionDto submit)
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.SubmitQuizAsync(studentId, submit);
        return Ok(result);
    }

    [HttpGet("progress")]
    public async Task<ActionResult<StudentProgressDto>> GetProgress()
    {
        if (!TryGetAuthenticatedStudentId(out var studentId))
            return StudentIdentityRequired();
        var result = await _studentService.GetProgressAsync(studentId);
        return Ok(result);
    }
}
