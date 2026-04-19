using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Tags("Admin - Users Management")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;

    public AdminUsersController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        if (page.HasValue || pageSize.HasValue)
        {
            var p = Math.Max(1, page ?? 1);
            var ps = pageSize.HasValue ? Math.Clamp(pageSize.Value, 1, 100) : 10;
            var paged = await _userManagementService.GetUsersPagedAsync(p, ps);
            return Ok(new
            {
                Message = "Get users successfully.",
                Result = paged.Items,
                Items = paged.Items,
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize
            });
        }

        var users = await _userManagementService.GetAllUsersAsync();
        return Ok(new
        {
            Message = "Get all users successfully.",
            Result = users,
            Items = users,
            TotalCount = users.Count,
            Page = 1,
            PageSize = users.Count
        });
    }


    [HttpGet("roles/{role}")]
    [HttpGet("/api/Admin/role/{role}")]
    public async Task<IActionResult> GetUsersByRole(string role)
    {
        var users = await _userManagementService.GetUserByRoleAsync(role);
        return Ok(new
        {
            Message = "Get users by role successfully.",
            Result = users,
            Items = users,
            TotalCount = users.Count
        });
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var result = await _userManagementService.ActivateUserAccountAsync(id);
        return result == null
            ? NotFound(new { message = "User not found." })
            : Ok(new
            {
                Message = "Account activated successfully.",
                result
            });
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var result = await _userManagementService.DeactivateUserAccountAsync(id);
        return result == null
            ? NotFound(new { message = "User not found." })
            : Ok(new
            {
                Message = "Deactive user successfully.",
                result
            });
    }

    [HttpPut("{id:guid}/toggle-status")]
    public async Task<ActionResult<UserManagementDTO>> ToggleStatus(Guid id, [FromBody] ToggleUserStatusRequestDto? request)
    {
        var result = await _userManagementService.ToggleUserStatusAsync(id, request?.IsActive);
        if (result == null)
            return NotFound(new { message = "User not found." });

        return Ok(result);
    }

    [HttpPost("{id:guid}/assign-role")]
    public async Task<IActionResult> AssignRole(Guid id, [FromQuery] string role)
    {
        var result = await _userManagementService.AssignRoleAsync(id, role);
        return result == null
            ? NotFound(new { message = "User or role not found." })
            : Ok(new
            {
                Message = "Assign user successfully.",
                result
            });
    }

    [HttpPut("{userId:guid}/revoke-role")]
    public async Task<IActionResult> RevokeRole(Guid userId)
    {
        var result = await _userManagementService.RevokeRoleAsync(userId);
        return Ok(result);
    }

    // ── CRUD ─────────────────────────────────────────────────────────────

    /// GET /api/admin/users/{id}  –  Get a single user
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var result = await _userManagementService.GetUserByIdAsync(id);
        return result == null
            ? NotFound(new { message = "User not found." })
            : Ok(new { Message = "Get user successfully.", Result = result });
    }
    /// POST /api/admin/users  –  Create a new user
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request data.", errors = ModelState });

        var result = await _userManagementService.CreateUserAsync(request);
        return result == null
            ? BadRequest(new { message = "Failed to create user." })
            : CreatedAtAction(nameof(GetAllUsers), new { }, new
            {
                Message = "User created successfully.",
                Result = result
            });
    }

    /// PUT /api/admin/users/{id}  –  Update user FullName / SchoolCohort
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request data.", errors = ModelState });

        var result = await _userManagementService.UpdateUserAsync(id, request);
        return result == null
            ? NotFound(new { message = "User not found." })
            : Ok(new { Message = "User updated successfully.", Result = result });
    }

    /// DELETE /api/admin/users/{id}  –  Permanently delete a user
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var deleted = await _userManagementService.DeleteUserAsync(id);
        if (!deleted)
            return NotFound(new { message = "User not found." });

        return Ok(new { Message = "User permanently deleted." });
    }  

    // ── Medical Student Verification ─────────────────────────────────────────────

    /// GET /api/admin/users/verifications/pending  –  Lấy danh sách chờ xác minh
    [HttpGet("verifications/pending")]
    public async Task<IActionResult> GetPendingVerifications()
    {
        var pending = await _userManagementService.GetPendingVerificationsAsync();
        return Ok(new { Message = "Verification list retrieved successfully.", Result = pending });
    }

    /// PUT /api/admin/users/{id}/verifications/approve  –  Duyệt xác minh sinh viên y khoa
    [HttpPut("{id:guid}/verifications/approve")]
    public async Task<IActionResult> ApproveMedicalVerification(Guid id, [FromBody] ApproveMedicalVerificationRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request data.", errors = ModelState });

        var adminId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

        var result = await _userManagementService.ApproveMedicalVerificationAsync(id, request.IsApproved, request.Notes, adminId);
        return result == null
            ? NotFound(new { message = "User not found." })
            : Ok(new { Message = request.IsApproved ? "Verification approved." : "Verification rejected.", Result = result });
    }
}

public class ToggleUserStatusRequestDto
{
    public bool? IsActive { get; set; }
}
