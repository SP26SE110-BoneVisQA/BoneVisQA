using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

public class VisualQAFormRequest
{
    [DefaultValue("Nguyên nhân gây ra thoái hóa khớp là gì?")]
    public string QuestionText { get; set; } = string.Empty;
    public IFormFile? CustomImage { get; set; }

    [DefaultValue("{\"x\": 10, \"y\": 20, \"w\": 100, \"h\": 150}")]
    public string? Coordinates { get; set; }

    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid? CaseId { get; set; }

    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid? AnnotationId { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VisualQAController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IVisualQaAiService _visualQaAiService;
    private readonly ISupabaseStorageService _storageService;

    public VisualQAController(
        IStudentService studentService,
        IVisualQaAiService visualQaAiService,
        ISupabaseStorageService storageService)
    {
        _studentService = studentService;
        _visualQaAiService = visualQaAiService;
        _storageService = storageService;
    }

    [HttpPost("ask")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<VisualQAResponseDto>> Ask([FromForm] VisualQAFormRequest formRequest, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        if (string.IsNullOrWhiteSpace(formRequest.QuestionText))
            return BadRequest(new { message = "QuestionText is required." });

        if (formRequest.CustomImage == null || formRequest.CustomImage.Length == 0)
        {
            return BadRequest(new
            {
                message = "A medical image file is strictly required for this endpoint. Use /ask-json for existing images or text-only questions."
            });
        }

        string imageUrl;

        if (formRequest.CustomImage is { Length: > 0 })
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(formRequest.CustomImage.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Only JPG, PNG, and WebP images are allowed." });

            imageUrl = await _storageService.UploadFileAsync(
                formRequest.CustomImage,
                "student_uploads",
                $"images/{studentId}");
        }
        else
        {
            // Should be unreachable due to the strict validation above.
            return BadRequest(new { message = "A medical image file is strictly required for this endpoint." });
        }

        var request = new VisualQARequestDto
        {
            QuestionText = formRequest.QuestionText,
            ImageUrl = imageUrl,
            Coordinates = formRequest.Coordinates,
            CaseId = formRequest.CaseId,
            AnnotationId = formRequest.AnnotationId
        };

        var question = await _studentService.CreateVisualQAQuestionAsync(studentId, request);
        var response = await _visualQaAiService.RunPipelineAsync(request, cancellationToken);
        await _studentService.SaveVisualQAAnswerAsync(question.Id, response);

        return Ok(response);
    }

    [HttpPost("ask-json")]
    public async Task<ActionResult<VisualQAResponseDto>> AskJson([FromBody] VisualQARequestDto request, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized(new { message = "Invalid token." });

        if (string.IsNullOrWhiteSpace(request.QuestionText))
            return BadRequest(new { message = "QuestionText is required." });

        var question = await _studentService.CreateVisualQAQuestionAsync(studentId, request);
        var response = await _visualQaAiService.RunPipelineAsync(request, cancellationToken);
        await _studentService.SaveVisualQAAnswerAsync(question.Id, response);

        return Ok(response);
    }
}
