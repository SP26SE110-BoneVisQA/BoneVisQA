using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer/profile")]
[Authorize(Roles = "Lecturer")]
public class LecturerProfileController : ControllerBase
{
    private readonly ILecturerProfileService _profileService;

    public LecturerProfileController(ILecturerProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    public async Task<ActionResult<LecturerProfileDto>> GetProfile()
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token khong chua user id hop le." });

        try
        {
            var result = await _profileService.GetProfileAsync(lecturerId.Value);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<ActionResult<LecturerProfileDto>> UpdateProfile([FromBody] UpdateLecturerProfileRequestDto request)
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token khong chua user id hop le." });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "FullName la bat buoc." });

        try
        {
            var result = await _profileService.UpdateProfileAsync(lecturerId.Value, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
