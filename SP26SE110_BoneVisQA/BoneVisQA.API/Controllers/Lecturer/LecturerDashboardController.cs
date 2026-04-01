using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer/dashboard")]
[Authorize(Roles = "Lecturer")]
public class LecturerDashboardController : ControllerBase
{
    private readonly ILecturerDashboardService _lecturerDashboardService;

    public LecturerDashboardController(ILecturerDashboardService lecturerDashboardService)
    {
        _lecturerDashboardService = lecturerDashboardService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<LecturerDashboardStatsDto>> GetStats()
    {
        var lecturerId = GetUserId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _lecturerDashboardService.GetDashboardStatsAsync(lecturerId.Value);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
