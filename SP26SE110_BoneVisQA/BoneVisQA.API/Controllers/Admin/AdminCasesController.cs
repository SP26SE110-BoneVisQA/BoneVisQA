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
    [Obsolete("Admin case management is read-only; experts own CRUD via /api/expert/cases.")]
    public IActionResult UpdateCase([FromRoute] Guid id, [FromBody] UpdateMedicalCaseDTORequest request) =>
        StatusCode(StatusCodes.Status403Forbidden, new
        {
            message = "Admin cannot modify medical cases. Case library is expert-owned.",
            code = "ADMIN_CASES_READ_ONLY"
        });

    [HttpDelete("{id:guid}")]
    [Obsolete("Admin case management is read-only; experts delete via DELETE /api/expert/cases/{id}.")]
    public IActionResult DeleteCase([FromRoute] Guid id) =>
        StatusCode(StatusCodes.Status403Forbidden, new
        {
            message = "Admin cannot delete medical cases. Case library is expert-owned.",
            code = "ADMIN_CASES_READ_ONLY"
        });
}
