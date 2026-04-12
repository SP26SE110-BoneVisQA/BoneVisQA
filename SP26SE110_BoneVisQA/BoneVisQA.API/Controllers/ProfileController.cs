using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/profile")]
[Tags("Profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(StudentProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<StudentProfileDto>> GetProfile()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var profile = await _profileService.GetProfileAsync(userId.Value);
        return Ok(profile);
    }

    [HttpPut]
    [ProducesResponseType(typeof(StudentProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<StudentProfileDto>> UpdateProfile([FromBody] UpdateStudentProfileRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var profile = await _profileService.UpdateProfileAsync(userId.Value, request);
        return Ok(profile);
    }

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> UploadAvatar(IFormFile? file, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        if (file == null)
            return BadRequest(new { message = "Avatar file is required." });

        var avatarUrl = await _profileService.UploadAvatarAsync(userId.Value, file, cancellationToken);
        return Ok(new { avatarUrl });
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
