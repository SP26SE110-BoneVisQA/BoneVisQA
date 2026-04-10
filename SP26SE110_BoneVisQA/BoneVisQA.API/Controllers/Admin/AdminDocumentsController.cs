using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/documents")]
[Tags("Admin - Documents")]
public class AdminDocumentsController : ControllerBase
{
    private const long MaxDocumentUploadBytes = 10 * 1024 * 1024;
    private readonly IDocumentManagementService _documentManagementService;
    private readonly IDocumentQualityService _documentQualityService;
    private readonly IDocumentService _documentService;
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminDocumentsController(
        IDocumentManagementService documentManagementService,
        IDocumentQualityService documentQualityService,
        IDocumentService documentService,
        IDocumentProcessingService documentProcessingService,
        IUnitOfWork unitOfWork)
    {
        _documentManagementService = documentManagementService;
        _documentQualityService = documentQualityService;
        _documentService = documentService;
        _documentProcessingService = documentProcessingService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Lists all document categories for admin dropdowns (empty list if none).
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminCategoryListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminCategoryListItemDto>>> GetCategories()
    {
        var rows = await _unitOfWork.CategoryRepository.GetAllAsync();
        var list = rows
            .OrderBy(c => c.Name)
            .Select(c => new AdminCategoryListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description
            })
            .ToList();
        return Ok(list);
    }

    /// <summary>
    /// Lists all tags for admin dropdowns (empty list if none).
    /// </summary>
    [HttpGet("tags")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminTagListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminTagListItemDto>>> GetTags()
    {
        var rows = await _unitOfWork.TagRepository.GetAllAsync();
        var list = rows
            .OrderBy(t => t.Type)
            .ThenBy(t => t.Name)
            .Select(t => new AdminTagListItemDto
            {
                Id = t.Id,
                Name = t.Name,
                Type = t.Type
            })
            .ToList();
        return Ok(list);
    }

    [HttpGet("quality/most-referenced")]
    public async Task<IActionResult> GetMostReferenced([FromQuery] int top = 10)
    {
        var result = await _documentQualityService.GetMostReferencedDocumentsAsync(top);
        return Ok(new { Message = "Get most reference document successfully.", result });
    }

    [HttpGet("quality/negative-reviews")]
    public async Task<IActionResult> GetNegativeReviews()
    {
        var result = await _documentQualityService.GetDocumentsWithNegativeExpertReviewsAsync();
        return Ok(new { Message = "Get documents negative review successfully.", result });
    }

    [HttpGet("quality/flagged-for-review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocumentsFlaggedForReview()
    {
        var result = await _documentQualityService.GetDocumentsFlaggedForReviewAsync();
        return Ok(new { Message = "Get documents flagged for review successfully.", result });
    }

    [HttpGet("quality/outdated")]
    public async Task<IActionResult> GetOutdated([FromQuery] int yearsThreshold = 2)
    {
        var result = await _documentQualityService.GetOutdatedDocumentsAsync(yearsThreshold);
        return Ok(new { Message = "Get outdated document successfully.", result });
    }

    [HttpPut("{id:guid}/tags")]
    public async Task<IActionResult> UpdateTags(Guid id, [FromBody] UpdateDocumentTagsRequest request)
    {
        var result = await _documentManagementService.UpdateTagsAsync(id, request.TagIds);
        return Ok(new { Message = "Update document tags successfully.", result });
    }

    [HttpPut("{id:guid}/category/{categoryId:guid}")]
    public async Task<IActionResult> ChangeCategory(Guid id, Guid categoryId)
    {
        var result = await _documentManagementService.ChangeCategoryAsync(id, categoryId);
        return Ok(new { Message = "Change document category successfully.", result });
    }

    [HttpPut("{id:guid}/version")]
    public async Task<IActionResult> UploadNewVersion(Guid id)
    {
        var result = await _documentManagementService.UploadNewVersionAsync(id);
        return Ok(new { Message = "Upload document version successfully.", result });
    }

    [HttpPut("{id:guid}/outdated")]
    public async Task<IActionResult> MarkOutdated(Guid id, [FromBody] bool isOutdated)
    {
        var result = await _documentManagementService.MarkOutdatedAsync(id, isOutdated);
        return Ok(new { Message = "Mark document outdate successfully.", result });
    }

    /// <summary>
    /// Upload one PDF (legacy <c>File</c>) or multiple PDFs (<c>Files</c>). Same route as before for FE compatibility.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxDocumentUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxDocumentUploadBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentUploadResultItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upload([FromForm] DocumentUploadRequest request, CancellationToken cancellationToken)
    {
        var batch = request.Files?.Where(f => f.Length > 0).ToList() ?? new List<IFormFile>();
        if (request.File is { Length: > 0 })
            batch.Insert(0, request.File);

        if (batch.Count == 0)
            return BadRequest(new { message = "At least one PDF file is required." });

        foreach (var file in batch)
        {
            if (file.Length > MaxDocumentUploadBytes)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid request",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = $"File '{file.FileName}' vượt quá giới hạn 10MB.",
                    Instance = HttpContext.Request.Path
                });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".pdf")
                return BadRequest(new { message = $"Only PDF files are allowed ({file.FileName})." });
        }

        var metadata = new DocumentUploadDto
        {
            Title = request.Title,
            CategoryId = request.CategoryId,
            TagIds = request.TagIds
        };

        if (request.Files is { Count: > 0 } || batch.Count > 1)
        {
            var results = await _documentProcessingService.UploadDocumentsAsync(batch, metadata, cancellationToken);
            return Ok(results);
        }

        var document = await _documentService.UploadDocumentAsync(batch[0], metadata, cancellationToken);
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
            return NotFound(new { message = "Document not found." });

        return Ok(document);
    }

    /// <summary>
    /// Gets granular ingestion status/progress for a document.
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(DocumentIngestionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentIngestionStatusDto>> GetIngestionStatus(Guid id)
    {
        var status = await _documentService.GetIngestionStatusAsync(id);
        if (status == null)
            return NotFound(new { message = "Document not found." });

        return Ok(status);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _documentService.DeleteDocumentAsync(id);
        return success ? NoContent() : NotFound(new { message = "Document not found." });
    }

    [HttpPost("{id:guid}/reindex")]
    public async Task<IActionResult> Reindex(Guid id)
    {
        var success = await _documentService.TriggerReindexAsync(id);
        return success
            ? Ok(new { message = "Reindexing started." })
            : NotFound(new { message = "Document not found or has no file path." });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        if (!string.Equals(request.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Status, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only final states Completed or Failed are allowed." });
        }

        await _documentService.UpdateIndexingStatusAsync(id, request.Status);
        return Ok(new { message = "Status updated." });
    }
}

public class UpdateDocumentTagsRequest
{
    public List<Guid> TagIds { get; set; } = new();
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class DocumentUploadRequest
{
    /// <summary>Legacy single-file field (unchanged for existing clients).</summary>
    public IFormFile? File { get; set; }

    /// <summary>Multi-file upload (same category/tags/title prefix for all).</summary>
    public List<IFormFile>? Files { get; set; }

    public string Title { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public List<Guid> TagIds { get; set; } = new();
}
