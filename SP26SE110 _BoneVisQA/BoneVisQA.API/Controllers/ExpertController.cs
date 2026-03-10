using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExpertController : ControllerBase
    {
        private readonly IMedicalCaseService _medicalcaseService;
        private readonly IQuizService _quizService;
        private readonly ITagCaseService _tagCaseService;

        public ExpertController(IMedicalCaseService medicalService, IQuizService quizService)
        {
            _medicalcaseService = medicalService;
            _quizService = quizService;
        }

        [HttpPost("cases")]
        public async Task<IActionResult> CreateCase(CreateMedicalCaseDTO dto)
        {
            var caseId = await _medicalcaseService.CreateMedicalCaseAsync(dto);

            return Ok(new
            {
                message = "Medical case created successfully",
                caseId = caseId
            });
        }

        [HttpPost("quizzes")]
        public async Task<IActionResult> CreateQuiz([FromBody] QuizDTO request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            var result = await _quizService.CreateQuizAsync(request);

            return Ok(result);
        }

        [HttpPost("quizzes/{quizId}/questions")]
        public async Task<IActionResult> CreateQuestion(
            Guid quizId,
            [FromBody] QuizQuestionDTO request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            var result = await _quizService.CreateQuestionAsync(quizId, request);

            return Ok(result);
        }

        [HttpGet("classes/{classId}/quizzes")]
        public async Task<IActionResult> GetQuizzesByClass(Guid classId)
        {
            var result = await _quizService.GetQuizzesByClassAsync(classId);

            return Ok(result);
        }

        [HttpGet("quizzes/recommend")]
        public async Task<IActionResult> RecommendQuiz([FromQuery] string topic)
        {
            if (string.IsNullOrEmpty(topic))
            {
                return BadRequest("Topic is required");
            }

            var result = await _quizService.RecommendQuizAsync(topic);

            return Ok(result);
        }

        [HttpPost("add-tags")]
        public async Task<IActionResult> AddTags([FromBody] CaseTagDTO dto)
        {
            var result = await _tagCaseService.AddTagCasesAsync(dto);

            if (!result)
                return BadRequest();

            return Ok("Tags added successfully");
        }
    }
}
