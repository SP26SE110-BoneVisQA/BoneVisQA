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
public class AdminDocumentsController : ControllerBase
{
    private readonly IDocumentManagementService _documentManagementService;
    private readonly IDocumentQualityService _documentQualityService;
    private readonly IDocumentService _documentService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminDocumentsController(
        IDocumentManagementService documentManagementService,
        IDocumentQualityService documentQualityService,
        IDocumentService documentService,
        IUnitOfWork unitOfWork)
    {
        _documentManagementService = documentManagementService;
        _documentQualityService = documentQualityService;
        _documentService = documentService;
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

    [HttpPost("upload")]
    [RequestSizeLimit(52428800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52428800)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] DocumentUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (request.File.Length > 52428800)
            return BadRequest(new { message = "File size exceeds the 50MB limit." });

        var allowedExtensions = new[] { ".pdf" };
        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Only PDF files are allowed." });

        var metadata = new DocumentUploadDto
        {
            Title = request.Title,
            CategoryId = request.CategoryId,
            TagIds = request.TagIds
        };

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
    public IFormFile File { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public List<Guid> TagIds { get; set; } = new();
}
