using System;
using System.Threading.Tasks;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VisualQAController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IAIService _aiService;

    public VisualQAController(IStudentService studentService, IAIService aiService)
    {
        _studentService = studentService;
        _aiService = aiService;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<VisualQAResponseDto>> Ask([FromBody] VisualQARequestDto request)
    {
        if (request.StudentId == Guid.Empty)
        {
            return BadRequest(new { message = "StudentId is required." });
        }

        var question = await _studentService.CreateVisualQAQuestionAsync(request.StudentId, request);
        var response = await _aiService.AskVisualQuestionAsync(request);
        await _studentService.SaveVisualQAAnswerAsync(question.Id, response);

        return Ok(response);
    }
}
