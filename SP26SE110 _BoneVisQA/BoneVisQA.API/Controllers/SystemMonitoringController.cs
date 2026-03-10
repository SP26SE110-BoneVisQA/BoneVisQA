using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize(Roles = "Admin")]
    public class SystemMonitoringController : ControllerBase
    {
        private readonly ISystemMonitoringService _service;

        public SystemMonitoringController(ISystemMonitoringService service)
        {
            _service = service;
        }

        // GET api/admin/monitoring/overview
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
        {
            var result = await _service.GetOverviewAsync();
            return Ok(result);
        }

        // GET api/admin/monitoring/users
        [HttpGet("users")]
        public async Task<IActionResult> GetUserStats()
        {
            var result = await _service.GetUserStatsAsync();
            return Ok(result);
        }

        // GET api/admin/monitoring/activity?from=2024-01-01&to=2024-12-31
        [HttpGet("activity")]
        public async Task<IActionResult> GetActivityStats(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to)
        {
            if (from > to)
                return BadRequest("Ngày bắt đầu phải nhỏ hơn ngày kết thúc.");

            var result = await _service.GetActivityStatsAsync(from, to);
            return Ok(result);
        }

        // GET api/admin/monitoring/rag
        [HttpGet("rag")]
        public async Task<IActionResult> GetRagStats()
        {
            var result = await _service.GetRagStatsAsync();
            return Ok(result);
        }

        // GET api/admin/monitoring/reviews
        [HttpGet("reviews")]
        public async Task<IActionResult> GetExpertReviewStats()
        {
            var result = await _service.GetExpertReviewStatsAsync();
            return Ok(result);
        }
    }
}
