using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers
{
    [ApiController]
    [Route("api/expert")]
    public class ExpertController : ControllerBase
    {
        private readonly IExpertService _expertService;

        public ExpertController(IExpertService expertService)
        {
            _expertService = expertService;
        }

        [HttpPost("cases")]
        public async Task<IActionResult> CreateCase(CreateMedicalCaseDTO dto)
        {
            var caseId = await _expertService.CreateMedicalCaseAsync(dto);

            return Ok(new
            {
                message = "Medical case created successfully",
                caseId = caseId
            });
        }
    }
}
