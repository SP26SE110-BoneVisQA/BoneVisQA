using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpGet("cases")]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetCases([FromQuery] Guid studentId)
    {
        var result = await _studentService.GetCasesAsync(studentId);
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

    [HttpPost("quizzes/submit")]
    public async Task<ActionResult<QuizResultDto>> SubmitQuiz([FromQuery] Guid studentId, [FromBody] SubmitQuizRequestDto request)
    {
        var result = await _studentService.SubmitQuizAsync(studentId, request);
        return Ok(result);
    }

    [HttpGet("progress")]
    public async Task<ActionResult<StudentProgressDto>> GetProgress([FromQuery] Guid studentId)
    {
        var result = await _studentService.GetProgressAsync(studentId);
        return Ok(result);
    }
}

