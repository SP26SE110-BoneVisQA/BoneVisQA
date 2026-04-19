using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin;

/// <summary>
/// Quản lý medical cases cho Admin (cùng payload/DTO với <c>/api/expert/cases</c>).
/// </summary>
[ApiController]
[Route("api/admin/cases")]
[Tags("Admin - Medical cases")]
[Authorize(Roles = "Admin")]
public class AdminCasesController : ControllerBase
{
    private readonly IMedicalCaseService _medicalCaseService;

    public AdminCasesController(IMedicalCaseService medicalCaseService)
    {
        _medicalCaseService = medicalCaseService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<GetMedicalCaseDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCases([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
    {
        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _medicalCaseService.GetAllMedicalCasesAsync(pageIndex, pageSize);
        return Ok(new
        {
            message = "Get medical cases successfully.",
            data = result,
            result
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetExpertMedicalCaseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCaseById([FromRoute] Guid id)
    {
        var detail = await _medicalCaseService.GetMedicalCaseByIdAsync(id);
        if (detail == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "The requested medical case was not found.",
                Instance = HttpContext.Request.Path.Value
            });
        }

        return Ok(new
        {
            message = "Get medical case successfully.",
            data = detail,
            result = detail
        });
    }

    /// <summary>Tạo case thay mặt expert — truyền <c>expertUserId</c> (user id của expert).</summary>
    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> CreateCaseForExpert(
        [FromBody] CreateExpertMedicalCaseJsonRequest body,
        [FromQuery] Guid expertUserId,
        CancellationToken cancellationToken)
    {
        if (body == null)
            return BadRequest(new { message = "Request body is required." });
        if (expertUserId == Guid.Empty)
            return BadRequest(new { message = "Query parameter expertUserId is required and must be a non-empty GUID." });

        var created = await _medicalCaseService.CreateMedicalCaseWithImagesJsonAsync(body, expertUserId, cancellationToken);
        return Ok(new
        {
            message = "Medical case created successfully",
            caseId = created.Id,
            data = created,
            result = created
        });
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    public async Task<IActionResult> UpdateCase([FromRoute] Guid id, [FromBody] UpdateMedicalCaseDTORequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Request body is required." });

        var updated = await _medicalCaseService.UpdateMedicalCaseAsync(id, request);
        if (updated == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "The requested medical case was not found.",
                Instance = HttpContext.Request.Path.Value
            });
        }

        return Ok(new
        {
            message = "Medical case updated successfully.",
            data = updated,
            result = updated
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCase([FromRoute] Guid id)
    {
        var ok = await _medicalCaseService.DeleteMedicalCaseAsync(id);
        if (!ok)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "The requested medical case was not found.",
                Instance = HttpContext.Request.Path.Value
            });
        }

        return Ok(new { message = "Medical case deleted successfully." });
    }
}
