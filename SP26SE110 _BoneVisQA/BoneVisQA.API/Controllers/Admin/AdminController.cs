using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IUserManagementService _userservice;
        private readonly IDocumentManagementService _documentservice;
        private readonly IDocumentQualityService _qualityservice;
        private readonly ISystemMonitoringService _systemservice;

        public AdminController(IUserManagementService userservice, IDocumentManagementService documentservice, IDocumentQualityService qualityservice, ISystemMonitoringService systemservice)
        {
            _userservice = userservice;
            _documentservice = documentservice;
            _qualityservice = qualityservice;
            _systemservice = systemservice;
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

        // GET api/admin/documents/{id}
        [HttpGet("documents/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDocumentById(Guid id)
        {
            var result = await _documentservice.GetDocumentByIdAsync(id);
            return Ok(new
            {
                Message = "Get document by id successfully.",
                result
            });
        }

        // POST api/admin/documents
        [HttpPost("documents")]
        public async Task<IActionResult> CreateDocument([FromForm] SaveDocumentDTO dto)
        {
            var result = await _documentservice.CreateDocumentAsync(dto);
            return Ok(new 
            { 
                Message = "Tạo tài liệu thành công.", result }
            );
        }

        // PUT api/admin/documents/{id}
        [HttpPut("documents/{id}")]
        public async Task<IActionResult> UpdateDocument(Guid id, [FromForm] SaveDocumentDTO dto)
        {
            var result = await _documentservice.UpdateDocumentAsync(id, dto);
            return Ok(new 
            { 
                Message = "Cập nhật tài liệu thành công.", result 
            });
        }

        // PUT api/admin/documents/{id}/tags
        [HttpPut("tags")]
        public async Task<IActionResult> UpdateTags(
     [FromQuery] Guid documentId,
     [FromQuery] List<Guid> tagIds)
        {
            var result = await _documentservice.UpdateTagsAsync(documentId, tagIds);
            return Ok(result);
        }

        // PUT api/admin/documents/{id}/category
        [HttpPut("{id}/category/{categoryId}")]
        public async Task<IActionResult> ChangeCategory(Guid id, [FromHeader] Guid categoryId)
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
    }
}
