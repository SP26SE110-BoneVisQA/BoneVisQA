using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin
{
  //  [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IUserManagementService _userservice;
        private readonly IDocumentManagementService _documentservice;
        private readonly IDocumentQualityService _qualityservice;

        public AdminController(IUserManagementService userservice, IDocumentManagementService documentservice, IDocumentQualityService qualityservice)
        {
            _userservice = userservice;
            _documentservice = documentservice;
            _qualityservice = qualityservice;
        }

        [HttpGet("role/{role}")]
        public async Task<IActionResult> GetUsersByRole(string role)
        {
            var users = await _userservice.GetUserByRoleAsync(role);
            return Ok(users);
        }

        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            await _userservice.ActivateUserAccountAsync(id);
            return Ok();
        }

        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            await _userservice.DeactivateUserAccountAsync(id);
            return Ok();
        }

        [HttpPost("{id}/assign-role")]
        public async Task<IActionResult> AssignRole(Guid id, string role)
        {
            await _userservice.AssignRoleAsync(id, role);
            return Ok();
        }

        [HttpDelete("{id}/revoke-role")]
        public async Task<IActionResult> RevokeRole(Guid id, string role)
        {
            await _userservice.RevokeRoleAsync(id, role);
            return Ok();
        }
        // ====================================================================================================================================


        // GET api/admin/documents/quality/most-referenced?top=10
        [HttpGet("most-referenced")]
        public async Task<IActionResult> GetMostReferenced([FromQuery] int top = 10)
        {
            var result = await _qualityservice.GetMostReferencedDocumentsAsync(top);
            return Ok(result);
        }

        // GET api/admin/documents/quality/negative-reviews
        [HttpGet("negative-reviews")]
        public async Task<IActionResult> GetNegativeReviews()
        {
            var result = await _qualityservice.GetDocumentsWithNegativeExpertReviewsAsync();
            return Ok(result);
        }

        // GET api/admin/documents/quality/high-question-rate?minQuestionCount=5
        [HttpGet("high-question-rate")]
        public async Task<IActionResult> GetHighQuestionRate([FromQuery] int minQuestionCount = 5)
        {
            var result = await _qualityservice.GetDocumentsWithHighStudentQuestionRateAsync(minQuestionCount);
            return Ok(result);
        }

        // GET api/admin/documents/quality/outdated?yearsThreshold=2
        [HttpGet("outdated")]
        public async Task<IActionResult> GetOutdated([FromQuery] int yearsThreshold = 2)
        {
            var result = await _qualityservice.GetOutdatedDocumentsAsync(yearsThreshold);
            return Ok(result);
        }

        // GET api/admin/documents/quality/require-review
        [HttpGet("require-review")]
        public async Task<IActionResult> GetRequireReview()
        {
            var result = await _qualityservice.GetDocumentsRequireReviewAsync();
            return Ok(result);
        }


        //==========================================================================================================================================

        // POST api/admin/documents
        [HttpPost("documents")]
        public async Task<IActionResult> Save([FromForm] SaveDocumentDTO dto)
        {
            var result = await _documentservice.SaveAsync(dto);
            return Ok(result);
        }

        // PUT api/admin/documents/{id}/tags
        [HttpPut("{id}/tags")]
        public async Task<IActionResult> UpdateTags(Guid id, [FromBody] List<Guid> tagIds)
        {
            await _documentservice.UpdateTagsAsync(id, tagIds);
            return NoContent();
        }

        // PUT api/admin/documents/{id}/category
        [HttpPut("{id}/category/{categoryId}")]
        public async Task<IActionResult> ChangeCategory(Guid id, Guid categoryId)
        {
            await _documentservice.ChangeCategoryAsync(id, categoryId);
            return NoContent();
        }

        // PUT api/admin/documents/{id}/version
        [HttpPut("{id}/version")]
        public async Task<IActionResult> UploadNewVersion(Guid id,[FromForm] UploadNewVersionRequestDTO request)
        {
            var result = await _documentservice.UploadNewVersionAsync(id, request.File);
            return Ok(result);
        }

        // PUT api/admin/documents/{id}/outdated
        [HttpPut("{id}/outdated")]
        public async Task<IActionResult> MarkOutdated(Guid id, [FromBody] bool isOutdated)
        {
            await _documentservice.MarkOutdatedAsync(id, isOutdated);
            return NoContent();
        }
    }
}
