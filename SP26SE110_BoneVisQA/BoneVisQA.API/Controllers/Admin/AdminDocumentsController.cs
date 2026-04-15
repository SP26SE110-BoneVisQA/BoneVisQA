using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Controllers.Admin;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/documents")]
[Tags("Admin - Documents")]
public class AdminDocumentsController : ControllerBase
{
    private const long MaxDocumentUploadBytes = 50 * 1024 * 1024;
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

    /// <summary>
    /// Lightweight operational endpoint to inspect rows stuck in pending_document_chunks.
    /// </summary>
    [HttpGet("ops/pending-chunks")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingChunkInspectionItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PendingChunkInspectionItemDto>>> InspectPendingChunks(
        [FromQuery] Guid? documentId = null,
        [FromQuery] int top = 100,
        CancellationToken cancellationToken = default)
    {
        var safeTop = Math.Clamp(top, 1, 500);
        var rows = await _unitOfWork.Context.Documents
            .AsNoTracking()
            .Where(d => !documentId.HasValue || d.Id == documentId.Value)
            .Where(d => _unitOfWork.Context.PendingDocumentChunks.Any(p => p.DocId == d.Id))
            .Select(d => new PendingChunkInspectionItemDto
            {
                DocumentId = d.Id,
                IndexingStatus = d.IndexingStatus,
                PendingChunkCount = _unitOfWork.Context.PendingDocumentChunks.Count(p => p.DocId == d.Id),
                MinChunkOrder = _unitOfWork.Context.PendingDocumentChunks
                    .Where(p => p.DocId == d.Id)
                    .Min(p => p.ChunkOrder),
                MaxChunkOrder = _unitOfWork.Context.PendingDocumentChunks
                    .Where(p => p.DocId == d.Id)
                    .Max(p => p.ChunkOrder)
            })
            .OrderByDescending(x => x.PendingChunkCount)
            .Take(safeTop)
            .ToListAsync(cancellationToken);

        if (!documentId.HasValue)
        {
            var orphanRows = await _unitOfWork.Context.PendingDocumentChunks
                .AsNoTracking()
                .Where(p => !_unitOfWork.Context.Documents.Any(d => d.Id == p.DocId))
                .GroupBy(p => p.DocId)
                .Select(g => new PendingChunkInspectionItemDto
                {
                    DocumentId = g.Key,
                    IndexingStatus = "Missing",
                    PendingChunkCount = g.Count(),
                    MinChunkOrder = g.Min(p => p.ChunkOrder),
                    MaxChunkOrder = g.Max(p => p.ChunkOrder)
                })
                .OrderByDescending(x => x.PendingChunkCount)
                .Take(safeTop)
                .ToListAsync(cancellationToken);

            rows.AddRange(orphanRows);
            rows = rows
                .OrderByDescending(x => x.PendingChunkCount)
                .Take(safeTop)
                .ToList();
        }

        return Ok(rows);
    }

    /// <summary>
    /// Lightweight operational endpoint to clean stuck rows from pending_document_chunks.
    /// If documentId is omitted, all pending rows are removed.
    /// </summary>
    [HttpDelete("ops/pending-chunks")]
    [ProducesResponseType(typeof(PendingChunkCleanupResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PendingChunkCleanupResultDto>> CleanupPendingChunks(
        [FromQuery] Guid? documentId = null,
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            "Reindexing",
            "Indexing",
            "Pending",
            "Processing"
        };

        var query = _unitOfWork.Context.PendingDocumentChunks
            .Where(pc => !_unitOfWork.Context.Documents.Any(d =>
                d.Id == pc.DocId && activeStatuses.Contains(d.IndexingStatus)));

        if (documentId.HasValue)
            query = query.Where(x => x.DocId == documentId.Value);

        var deletedRows = await query.ExecuteDeleteAsync(cancellationToken);
        return Ok(new PendingChunkCleanupResultDto
        {
            DocumentId = documentId,
            DeletedRows = deletedRows
        });
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
                    Detail = $"File '{file.FileName}' exceeds the 50MB limit.",
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

    /// <summary>
    /// Upload a new PDF for an existing document. If the SHA-256 content hash changes, old vectors are removed and the document is queued for re-indexing.
    /// </summary>
    [HttpPut("{id:guid}/file")]
    [RequestSizeLimit(MaxDocumentUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxDocumentUploadBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDocumentFile(Guid id, [FromForm] DocumentFileUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { message = "A PDF file is required." });

        if (request.File.Length > MaxDocumentUploadBytes)
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
            var document = await _documentService.UpdateDocumentVersionAsync(id, request.File, cancellationToken);
            return Ok(new { message = "Document updated.", document });
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

    [HttpPost("{id:guid}/reindex")]
    public async Task<IActionResult> Reindex(Guid id)
    {
        try
        {
            var success = await _documentService.TriggerReindexAsync(id);
            return success
                ? Ok(new { message = "Reindexing queued." })
                : NotFound(new { message = "Document not found or has no file path." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("analytics/chunk-citation-frequency")]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentChunkCitationFrequencyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DocumentChunkCitationFrequencyDto>>> GetChunkCitationFrequency(
        [FromQuery] Guid? documentId = null,
        [FromQuery] int top = 100,
        CancellationToken cancellationToken = default)
    {
        var rows = await _documentService.GetChunkCitationFrequencyAsync(documentId, top, cancellationToken);
        return Ok(rows);
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

/// <summary>Multipart body for <c>PUT /api/admin/documents/{id}/file</c>.</summary>
public class DocumentFileUpdateRequest
{
    public IFormFile File { get; set; } = null!;
    public string? Title { get; set; }
    public Guid? CategoryId { get; set; }
    public List<Guid>? TagIds { get; set; }
}

public class PendingChunkInspectionItemDto
{
    public Guid DocumentId { get; set; }
    public int PendingChunkCount { get; set; }
    public int MinChunkOrder { get; set; }
    public int MaxChunkOrder { get; set; }
    public string IndexingStatus { get; set; } = "Unknown";
}

public class PendingChunkCleanupResultDto
{
    public Guid? DocumentId { get; set; }
    public int DeletedRows { get; set; }
}
