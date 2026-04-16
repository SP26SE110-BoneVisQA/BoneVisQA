using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/documents")]
[Tags("Documents")]
public class DocumentsController : ControllerBase
{
    private const long MaxDocumentUploadBytes = 50 * 1024 * 1024;
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpPost("{id:guid}/update-version")]
    [RequestSizeLimit(MaxDocumentUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxDocumentUploadBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateVersion(Guid id, [FromForm] DocumentVersionUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!request.ReindexOnly && (request.File == null || request.File.Length == 0))
            return BadRequest(new { message = "A PDF file is required unless reindexOnly is true." });

        if (request.ReindexOnly)
        {
            try
            {
                var doc = await _documentService.UpdateDocumentVersionAsync(id, null, isNewFile: false, cancellationToken);
                return Ok(new { message = "Re-index queued (same file).", document = doc });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Document not found." });
            }
        }

        if (request.File!.Length > MaxDocumentUploadBytes)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "File exceeds the 50MB limit.",
                Instance = HttpContext.Request.Path
            });
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (extension != ".pdf")
            return BadRequest(new { message = $"Only PDF files are allowed ({request.File.FileName})." });

        try
        {
            var document = await _documentService.UpdateDocumentVersionAsync(id, request.File, isNewFile: true, cancellationToken);
            return Ok(new { message = "Document update-version queued for indexing.", document });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Document not found." });
        }
    }
}

public class DocumentVersionUpdateRequest
{
    /// <summary>When true, re-embed the same PDF (patch version) without uploading a file.</summary>
    public bool ReindexOnly { get; set; }

    public IFormFile? File { get; set; }
}
