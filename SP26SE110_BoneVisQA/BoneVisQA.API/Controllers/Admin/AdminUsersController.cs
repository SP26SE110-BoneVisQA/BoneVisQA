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

    //[HttpPut("{id:guid}/toggle-status")]
    //public async Task<ActionResult<UserManagementDTO>> ToggleStatus(Guid id, [FromBody] ToggleUserStatusRequestDto? request)
    //{
    //    var result = await _userManagementService.ToggleUserStatusAsync(id, request?.IsActive);
    //    if (result == null)
    //        return NotFound(new { message = "Không tìm thấy người dùng." });

    //    return Ok(result);
    //}

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

    // ── CRUD ─────────────────────────────────────────────────────────────
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

    /// GET /api/admin/users/{id}  –  Get a single user
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var result = await _userManagementService.GetUserByIdAsync(id);
        return result == null
            ? NotFound(new { message = "Không tìm thấy người dùng." })
            : Ok(new { Message = "Get user successfully.", Result = result });
    }
    /// POST /api/admin/users  –  Create a new user
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request data.", errors = ModelState });

        try
        {
            var result = await _userManagementService.CreateUserAsync(request);
            return result == null
                ? BadRequest(new { message = "Failed to create user." })
                : CreatedAtAction(nameof(GetAllUsers), new { }, new
                {
                    Message = "User created successfully.",
                    Result = result
                });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// PUT /api/admin/users/{id}  –  Update user FullName / SchoolCohort
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request data.", errors = ModelState });

        var result = await _userManagementService.UpdateUserAsync(id, request);
        return result == null
            ? NotFound(new { message = "Không tìm thấy người dùng." })
            : Ok(new { Message = "User updated successfully.", Result = result });
    }

    /// DELETE /api/admin/users/{id}  –  Permanently delete a user
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var deleted = await _userManagementService.DeleteUserAsync(id);
        if (!deleted)
            return NotFound(new { message = "Không tìm thấy người dùng." });

        return Ok(new { Message = "User permanently deleted." });
    }

    // ── Class management ────────────────────────────────────────────────────────

    /// GET /api/admin/users/{userId}/classes  –  Lấy danh sách lớp của user
    [HttpGet("{userId:guid}/classes")]
    public async Task<IActionResult> GetUserClasses(Guid userId)
    {
        var classes = await _userManagementService.GetUserClassesAsync(userId);
        return Ok(new { Message = "Lấy danh sách lớp thành công.", Result = classes });
    }

    /// GET /api/admin/users/classes  –  Lấy tất cả lớp có sẵn
    [HttpGet("classes")]
    public async Task<IActionResult> GetAvailableClasses()
    {
        var classes = await _userManagementService.GetAvailableClassesAsync();
        return Ok(new { Message = "Lấy danh sách lớp thành công.", Result = classes });
    }

    /// POST /api/admin/users/{userId}/classes  –  Gán user vào một lớp
    [HttpPost("{userId:guid}/classes")]
    public async Task<IActionResult> AssignUserToClass(Guid userId, [FromBody] AssignUserToClassRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid request data.", errors = ModelState });

        var result = await _userManagementService.AssignUserToClassAsync(userId, request.ClassId);
        return result == null
            ? BadRequest(new { message = "Không thể gán user vào lớp. Kiểm tra lại vai trò hoặc lớp học." })
            : Ok(new { Message = "User đã được gán vào lớp.", Result = result });
    }

    /// DELETE /api/admin/users/{userId}/classes/{classId}  –  Xóa user khỏi một lớp
    [HttpDelete("{userId:guid}/classes/{classId:guid}")]
    public async Task<IActionResult> RemoveUserFromClass(Guid userId, Guid classId)
    {
        var removed = await _userManagementService.RemoveUserFromClassAsync(userId, classId);
        return !removed
            ? NotFound(new { message = "Không tìm thấy liên kết user-lớp." })
            : Ok(new { Message = "User đã được xóa khỏi lớp." });
    }
}

public class ToggleUserStatusRequestDto
{
    public bool? IsActive { get; set; }
}
