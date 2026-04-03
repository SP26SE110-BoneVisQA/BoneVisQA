using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;

    public AdminUsersController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userManagementService.GetAllUsersAsync();
        return Ok(new
        {
            Message = "Get all users successfully.",
            Result = users
        });
    }

    [HttpGet("roles/{role}")]
    public async Task<IActionResult> GetUsersByRole(string role)
    {
        var users = await _userManagementService.GetUserByRoleAsync(role);
        return Ok(new
        {
            Message = "Get Users by role successfully.",
            users
        });
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var result = await _userManagementService.ActivateUserAccountAsync(id);
        return result == null
            ? NotFound(new { message = "Không tìm thấy người dùng." })
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
            ? NotFound(new { message = "Không tìm thấy người dùng." })
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
            return NotFound(new { message = "Không tìm thấy người dùng." });

        return Ok(result);
    }

    [HttpPost("{id:guid}/assign-role")]
    public async Task<IActionResult> AssignRole(Guid id, [FromQuery] string role)
    {
        var result = await _userManagementService.AssignRoleAsync(id, role);
        return result == null
            ? NotFound(new { message = "Không tìm thấy người dùng hoặc role." })
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
}

public class ToggleUserStatusRequestDto
{
    public bool? IsActive { get; set; }
}
