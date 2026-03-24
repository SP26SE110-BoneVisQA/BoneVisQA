using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BoneVisQA.API.Controllers.Student;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Student")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpGet("cases")]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetCases([FromQuery] Guid studentId)
    {
        var result = await _studentService.GetCasesAsync(studentId);
        return Ok(result);
    }

    [HttpGet("cases/filter")]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetFilteredCases([FromQuery] Guid studentId, [FromQuery] CaseFilterRequestDto filter)
    {
        var result = await _studentService.GetFilteredCasesAsync(studentId, filter);
        return Ok(result);
    }

    [HttpGet("cases/{caseId:guid}")]
    public async Task<ActionResult<CaseDetailDto>> GetCaseDetail(Guid caseId, [FromQuery] Guid studentId)
    {
        var result = await _studentService.GetCaseDetailAsync(caseId, studentId);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("annotations")]
    public async Task<ActionResult<AnnotationDto>> CreateAnnotation([FromQuery] Guid studentId, [FromBody] CreateAnnotationRequestDto request)
    {
        var result = await _studentService.CreateAnnotationAsync(studentId, request);
        return Ok(result);
    }

    [HttpPost("questions")]
    public async Task<ActionResult<StudentQuestionDto>> AskQuestion([FromQuery] Guid studentId, [FromBody] AskQuestionRequestDto request)
    {
        var result = await _studentService.AskQuestionAsync(studentId, request);
        return Ok(result);
    }

    [HttpGet("questions")]
    public async Task<ActionResult<IReadOnlyList<StudentQuestionHistoryItemDto>>> GetQuestionHistory([FromQuery] Guid studentId)
    {
        var result = await _studentService.GetQuestionHistoryAsync(studentId);
        return Ok(result);
    }

    [HttpGet("announcements")]
    public async Task<ActionResult<IReadOnlyList<StudentAnnouncementDto>>> GetAnnouncements([FromQuery] Guid studentId)
    {
        var result = await _studentService.GetAnnouncementsAsync(studentId);
        return Ok(result);
    }

    [HttpGet("quizzes")]
    public async Task<ActionResult<IReadOnlyList<QuizListItemDto>>> GetQuizzes([FromQuery] Guid studentId)
    {
        var result = await _studentService.GetAvailableQuizzesAsync(studentId);
        return Ok(result);
    }


    [HttpPost("quizzes/{quizId:guid}/start")]
    public async Task<ActionResult<QuizSessionDto>> StartQuiz(Guid quizId, [FromQuery] Guid studentId)
    {
        var result = await _studentService.StartQuizAsync(studentId, quizId);
        return Ok(result);
    }

                                                            //  phan nam 
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitAnswer(
        [FromQuery] Guid studentId,
        [FromBody] StudentSubmitQuestionDTO submit)
    {
        var result = await _studentService.SubmitQuizAsync(studentId, submit);
        return Ok(result);
    }


                                                             //code tran
    //[HttpPost("quizzes/submit")]
    //public async Task<ActionResult<QuizResultDto>> SubmitQuiz([FromQuery] Guid? studentId, [FromBody] SubmitQuizRequestDto request)
    //{
    //    var effectiveStudentId = studentId ?? Guid.Empty;
    //    if (effectiveStudentId == Guid.Empty)
    //    {
    //        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
    //        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var id))
    //            return Unauthorized(new { message = "Không xác định được sinh viên. Truyền studentId hoặc đăng nhập với tài khoản sinh viên." });
    //        effectiveStudentId = id;
    //    }
    //    try
    //    {
    //        var result = await _studentService.SubmitQuizAsync(effectiveStudentId, request);
    //        return Ok(result);
    //    }
    //    catch (InvalidOperationException ex)
    //    {
    //        return BadRequest(new { message = ex.Message });
    //    }
    //}

    [HttpGet("progress")]
    public async Task<ActionResult<StudentProgressDto>> GetProgress([FromQuery] Guid studentId)
    {
        var result = await _studentService.GetProgressAsync(studentId);
        return Ok(result);
    }
}
