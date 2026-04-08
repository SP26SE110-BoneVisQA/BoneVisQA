using BoneVisQA.Services.Interfaces.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/monitoring")]
[Tags("Admin - Monitoring")]
public class AdminMonitoringController : ControllerBase
{
    private readonly ISystemMonitoringService _systemMonitoringService;

    public AdminMonitoringController(ISystemMonitoringService systemMonitoringService)
    {
        _systemMonitoringService = systemMonitoringService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUserStats()
    {
        var result = await _systemMonitoringService.GetUserStatsAsync();
        return Ok(new { Message = "Get user stat successfully.", result });
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivityStats([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (from > to)
            return BadRequest("Ngày bắt đầu phải nhỏ hơn ngày kết thúc.");

        var result = await _systemMonitoringService.GetActivityStatsAsync(from, to);
        return Ok(new { Message = "Get activity stat successfully.", result });
    }

    [HttpGet("rag")]
    public async Task<IActionResult> GetRagStats()
    {
        var result = await _systemMonitoringService.GetRagStatsAsync();
        return Ok(new { Message = "Get rag stat successfully.", result });
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> GetExpertReviewStats()
    {
        var result = await _systemMonitoringService.GetExpertReviewStatsAsync();
        return Ok(new { Message = "Get expert review successfully.", result });
    }
}
