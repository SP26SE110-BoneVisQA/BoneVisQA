using System;
using System.Threading.Tasks;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

public class VisualQAFormRequest
{
    public Guid StudentId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public IFormFile? CustomImage { get; set; }
    public string? Coordinates { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? AnnotationId { get; set; }
    public string? Language { get; set; }
    public string? ExistingImageUrl { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VisualQAController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IAIService _aiService;
    private readonly ISupabaseStorageService _storageService;

    public VisualQAController(
        IStudentService studentService,
        IAIService aiService,
        ISupabaseStorageService storageService)
    {
        _studentService = studentService;
        _aiService = aiService;
        _storageService = storageService;
    }

    [HttpPost("ask")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<VisualQAResponseDto>> Ask([FromForm] VisualQAFormRequest formRequest)
    {
        if (formRequest.StudentId == Guid.Empty)
        {
            return BadRequest(new { message = "StudentId is required." });
        }

        if (string.IsNullOrWhiteSpace(formRequest.QuestionText))
        {
            return BadRequest(new { message = "QuestionText is required." });
        }

        string? imageUrl = formRequest.ExistingImageUrl;

        if (formRequest.CustomImage != null && formRequest.CustomImage.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(formRequest.CustomImage.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Only JPG, PNG, and WebP images are allowed." });
            }

            imageUrl = await _storageService.UploadFileAsync(
                formRequest.CustomImage,
                "student_uploads",
                $"images/{formRequest.StudentId}");
        }

        var request = new VisualQARequestDto
        {
            StudentId = formRequest.StudentId,
            QuestionText = formRequest.QuestionText,
            ImageUrl = imageUrl,
            Coordinates = formRequest.Coordinates,
            CaseId = formRequest.CaseId,
            AnnotationId = formRequest.AnnotationId,
            Language = formRequest.Language
        };

        var question = await _studentService.CreateVisualQAQuestionAsync(formRequest.StudentId, request);
        var response = await _aiService.AskVisualQuestionAsync(request);
        await _studentService.SaveVisualQAAnswerAsync(question.Id, response);

        return Ok(response);
    }

    [HttpPost("ask-json")]
    public async Task<ActionResult<VisualQAResponseDto>> AskJson([FromBody] VisualQARequestDto request)
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
