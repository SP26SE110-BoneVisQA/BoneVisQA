using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Expert;

[ApiController]
[Route("api/expert/profile")]
[Authorize(Roles = "Expert")]
public class ExpertProfileController : ControllerBase
{
    private readonly IExpertProfileService _profileService;

    public ExpertProfileController(IExpertProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    public async Task<ActionResult<ExpertProfileDto>> GetProfile()
    {
        var expertId = GetUserId();
        if (expertId == null)
            return Unauthorized(new { message = "Token khong chua user id hop le." });

        try
        {
            var result = await _profileService.GetProfileAsync(expertId.Value);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<ActionResult<ExpertProfileDto>> UpdateProfile([FromBody] UpdateExpertProfileRequestDto request)
    {
        var expertId = GetUserId();
        if (expertId == null)
            return Unauthorized(new { message = "Token khong chua user id hop le." });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "FullName la bat buoc." });

        try
        {
            var result = await _profileService.UpdateProfileAsync(expertId.Value, request);
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
