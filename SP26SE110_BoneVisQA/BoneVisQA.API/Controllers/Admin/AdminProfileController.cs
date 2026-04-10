using System.Security.Claims;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin;

[ApiController]
[Route("api/admin/profile")]
[Authorize(Roles = "Admin")]
public class AdminProfileController : ControllerBase
{
    private readonly IAdminProfileService _adminProfileService;

    public AdminProfileController(IAdminProfileService adminProfileService)
    {
        _adminProfileService = adminProfileService;
    }

    [HttpGet]
    public async Task<ActionResult<AdminProfileDto>> GetProfile()
    {
        var adminId = GetUserId();
        if (adminId == null)
            return Unauthorized(new { message = "Token khong chua user id hop le." });

        var profile = await _adminProfileService.GetProfileAsync(adminId.Value);
        return Ok(profile);
    }

    [HttpPut]
    public async Task<ActionResult<AdminProfileDto>> UpdateProfile([FromBody] UpdateAdminProfileRequestDto request)
    {
        var adminId = GetUserId();
        if (adminId == null)
            return Unauthorized(new { message = "Token khong chua user id hop le." });

        var profile = await _adminProfileService.UpdateProfileAsync(adminId.Value, request);
        return Ok(profile);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
