using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

public class DocumentUploadRequest
{
    public IFormFile File { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(104857600)]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] DocumentUploadRequest request)
    {
        Console.WriteLine("=> [DEBUG] Starting file upload processing...");

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { message = "File is required." });
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (extension != ".pdf")
        {
            return BadRequest(new
            {
                message = "Security Policy: Only standard .pdf documents are allowed in the BoneVisQA medical knowledge base."
            });
        }

        Console.WriteLine("--> [DEBUG] File extension validated...");

        var metadata = new DocumentUploadDto
        {
            Title = request.Title,
            CategoryId = request.CategoryId
        };

        Console.WriteLine("--> [DEBUG] Passing to DocumentService...");

        var document = await _documentService.UploadDocumentAsync(request.File, metadata);
        return CreatedAtAction(nameof(GetById), new { id = document.Id }, document);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> GetAll()
    {
        var documents = await _documentService.GetAllDocumentsAsync();
        return Ok(documents);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id)
    {
        var document = await _documentService.GetDocumentByIdAsync(id);
        if (document == null)
        {
            return NotFound(new { message = "Document not found." });
        }
        return Ok(document);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _documentService.DeleteDocumentAsync(id);
        if (!success)
        {
            return NotFound(new { message = "Document not found." });
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/reindex")]
    public async Task<IActionResult> Reindex(Guid id)
    {
        var success = await _documentService.TriggerReindexAsync(id);
        if (!success)
        {
            return NotFound(new { message = "Document not found or has no file path." });
        }
        return Ok(new { message = "Reindexing started." });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        await _documentService.UpdateIndexingStatusAsync(id, request.Status);
        return Ok(new { message = "Status updated." });
    }
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
