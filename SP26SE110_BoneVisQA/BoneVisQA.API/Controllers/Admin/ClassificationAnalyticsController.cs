using BoneVisQA.Services.Interfaces.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Admin
{
    /// <summary>
    /// Classification Analytics Controller - Thống kê theo Classification
    /// </summary>
    [ApiController]
    [Route("api/admin/classification-analytics")]
    [Tags("Admin - Classification Analytics")]
    [Authorize(Roles = "Admin")]
    public class ClassificationAnalyticsController : ControllerBase
    {
        private readonly IClassificationAnalyticsService _analyticsService;

        public ClassificationAnalyticsController(IClassificationAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>
        /// Lấy tổng hợp Classification Analytics
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetClassificationAnalytics()
        {
            var result = await _analyticsService.GetClassificationAnalyticsAsync();
            return Ok(result);
        }
    }
}
