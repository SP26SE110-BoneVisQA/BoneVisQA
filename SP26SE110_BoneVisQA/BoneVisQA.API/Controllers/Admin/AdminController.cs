using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using BoneVisQA.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin
{

    [Authorize(Roles = "Admin")]

    [ApiController]
    [Route("api/admin/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IUserManagementService _userservice;
        private readonly IDocumentManagementService _documentservice;
        private readonly IDocumentQualityService _qualityservice;
        private readonly ISystemMonitoringService _systemservice;
        private readonly IDocumentService _documentService;

        public AdminController(IUserManagementService userservice, IDocumentManagementService documentservice, IDocumentQualityService qualityservice, ISystemMonitoringService systemservice, IDocumentService documentService)
        {
            _userservice = userservice;
            _documentservice = documentservice;
            _qualityservice = qualityservice;
            _systemservice = systemservice;
            _documentService = documentService;
        }
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userservice.GetAllUsersAsync();
            return Ok(new
            {
                Message = "Get All Users  successfully.",
                users
            });
        }

        [HttpGet("role/{role}")]
        public async Task<IActionResult> GetUsersByRole(string role)
        {
            var users = await _userservice.GetUserByRoleAsync(role);
            return Ok(new
            {
                Message = "Get Users by role successfully.",
                users
            });
        }

        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            var result = await _userservice.ActivateUserAccountAsync(id);
            return Ok(new
            {
                Message = "Actice user successfully.",
                result
            });
        }

        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var result = await _userservice.DeactivateUserAccountAsync(id);
            return Ok(new
            {
                Message = "Deactive user successfully.",
                result
            });
        }

        [HttpPost("{id}/assign-role")]
        public async Task<IActionResult> AssignRole(Guid id, string role)
        {
            var result = await _userservice.AssignRoleAsync(id, role);
            return Ok(new
            {
                Message = "Assign user successfully.",
                result
            });
        }

        // PUT api/admin/users/{userId}/revoke-role
        [HttpPut("{userId}/revoke-role")]
        public async Task<IActionResult> RevokeRole(Guid userId)
        {
            var result = await _userservice.RevokeRoleAsync(userId);
            return Ok(result);
        }
        // ====================================================================================================================================


        // GET api/admin/documents/quality/most-referenced?top=10
        [HttpGet("most-referenced")]
        public async Task<IActionResult> GetMostReferenced([FromQuery] int top = 10)
        {
            var result = await _qualityservice.GetMostReferencedDocumentsAsync(top);

            return Ok(new
            {
                Message = "Get most reference document successfully.",
                result
            });
        }

        // GET api/admin/documents/quality/negative-reviews
        [HttpGet("negative-reviews")]
        public async Task<IActionResult> GetNegativeReviews()
        {
            var result = await _qualityservice.GetDocumentsWithNegativeExpertReviewsAsync();

            return Ok(new
            {
                Message = "Get documents negative review successfully.",
                result
            });
        }

        // GET api/admin/documents/quality/flagged-for-review
        [HttpGet("flagged-for-review")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDocumentsFlaggedForReview()
        {
            var result = await _qualityservice.GetDocumentsFlaggedForReviewAsync();

            return Ok(new
            {
                Message = "Get documents flagged for review successfully.",
                result
            });
        }

        // GET api/admin/documents/quality/outdated?yearsThreshold=2
        [HttpGet("outdated")]
        public async Task<IActionResult> GetOutdated([FromQuery] int yearsThreshold = 2)
        {
            var result = await _qualityservice.GetOutdatedDocumentsAsync(yearsThreshold);

            return Ok(new
            {
                Message = "Get outdated document successfully.",
                result
            });
        }

        //==========================================================================================================================================



        // PUT api/admin/documents/{id}/tags
        [HttpPut("tags")]
        public async Task<IActionResult> UpdateTags([FromQuery] Guid documentId, [FromQuery] List<Guid> tagIds)
        {
            var result = await _documentservice.UpdateTagsAsync(documentId, tagIds);
            return Ok(new
            {
                Message = "Update document tags successfully.",
                result
            });
        }

        // PUT api/admin/documents/{id}/category
        [HttpPut("{id}/category/{categoryId}")]
        public async Task<IActionResult> ChangeCategory(Guid id, [FromRoute] Guid categoryId)
        {
            var result = await _documentservice.ChangeCategoryAsync(id, categoryId);

            return Ok(new
            {
                Message = "Change document category successfully.",
                result
            });
        }

        // PUT api/admin/documents/{id}/version
        [HttpPut("{id}/version")]
        public async Task<IActionResult> UploadNewVersion(Guid id)
        {
            var result = await _documentservice.UploadNewVersionAsync(id);

            return Ok(new
            {
                Message = "Upload document version successfully.",
                result
            });
        }

        // PUT api/admin/documents/{id}/outdated
        [HttpPut("{id}/outdated")]
        public async Task<IActionResult> MarkOutdated(Guid id, [FromBody] bool isOutdated)
        {
            var result = await _documentservice.MarkOutdatedAsync(id, isOutdated);

            return Ok(new
            {
                Message = "Mark document outdate successfully.",
                result
            });
        }

        //===================================================================================================

        // GET api/admin/monitoring/users
        [HttpGet("users")]
        public async Task<IActionResult> GetUserStats()
        {
            var result = await _systemservice.GetUserStatsAsync();
            return Ok(new
            {
                Message = "Get user stat successfully.",
                result
            });
        }

        // GET api/admin/monitoring/activity?from=2024-01-01&to=2024-12-31
        [HttpGet("activity")]
        public async Task<IActionResult> GetActivityStats([FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            if (from > to)
                return BadRequest("Ngày bắt đầu phải nhỏ hơn ngày kết thúc.");

            var result = await _systemservice.GetActivityStatsAsync(from, to);
            return Ok(new
            {
                Message = "Get activity stat successfully.",
                result
            });
        }

        // GET api/admin/monitoring/rag
        [HttpGet("rag")]
        public async Task<IActionResult> GetRagStats()
        {
            var result = await _systemservice.GetRagStatsAsync();
            return Ok(new
            {
                Message = "Get rag stat successfully.",
                result
            });
        }

        // GET api/admin/monitoring/reviews
        [HttpGet("reviews")]
        public async Task<IActionResult> GetExpertReviewStats()
        {
            var result = await _systemservice.GetExpertReviewStatsAsync();
            return Ok(new
            {
                Message = "Get expert review successfully.",
                result
            });
        }

        //===========================================================================================================================================
        [HttpPost("document-upload")]
        [RequestSizeLimit(52428800)]
        [RequestFormLimits(MultipartBodyLengthLimit = 52428800)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<DocumentDto>> Upload([FromForm] DocumentUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new { message = "File is required." });
            }

            // Custom 50MB error response (framework may still return 413 for extreme oversize cases).
            if (request.File.Length > 52428800)
            {
                return BadRequest(new { message = "File tải lên vượt quá giới hạn 50MB. Vui lòng chọn file nhỏ hơn." });
            }

            var allowedExtensions = new[] { ".pdf" };
            var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Only PDF files are allowed." });
            }

            var metadata = new DocumentUploadDto
            {
                Title = request.Title,
                CategoryId = request.CategoryId,
                TagIds = request.TagIds
            };

            var document = await _documentService.UploadDocumentAsync(request.File, metadata);
            return CreatedAtAction(nameof(GetById), new { id = document.Id }, document);
        }

        [HttpGet("document")]
        public async Task<ActionResult<IEnumerable<DocumentDto>>> GetAll()
        {
            var documents = await _documentService.GetAllDocumentsAsync();
            return Ok(documents);
        }

        [HttpGet("document/{id:guid}")]
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

        [HttpPost("document/{id:guid}/reindex")]
        public async Task<IActionResult> Reindex(Guid id)
        {
            var success = await _documentService.TriggerReindexAsync(id);
            if (!success)
            {
                return NotFound(new { message = "Document not found or has no file path." });
            }
            return Ok(new { message = "Reindexing started." });
        }

        [HttpPatch("document/{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
        {
            await _documentService.UpdateIndexingStatusAsync(id, request.Status);
            return Ok(new { message = "Status updated." });
        }
    }
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

