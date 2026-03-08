using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuizController : ControllerBase
    {
        private readonly IQuizService _quizService;

        public QuizController(IQuizService quizService)
        {
            _quizService = quizService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateQuiz([FromBody] QuizDTO dto)
        {
            var result = await _quizService.CreateQuizAsync(dto);
            return Ok(result);
        }

        [HttpPost("add-question")]
        public async Task<IActionResult> AddQuestion([FromBody] QuizQuestionDTO dto)
        {
            var result = await _quizService.CreateQuizQuestionAsync(dto);
            return Ok(result);
        }

        [HttpGet("class/{classId}")]
        public async Task<IActionResult> GetQuizForClass(Guid classId)
        {
            var result = await _quizService.GetQuizForClassAsync(classId);
            return Ok(result);
        }

        [HttpPost("submit-answer")]
        public async Task<IActionResult> SubmitAnswer([FromBody] StudentQuizAnswerDTO dto)
        {
            var result = await _quizService.SubmitAnswerAsync(dto);
            return Ok(result);
        }

        [HttpPost("grade/{attemptId}")]
        public async Task<IActionResult> GradeQuiz(Guid attemptId)
        {
            var score = await _quizService.GradeQuizAttemptAsync(attemptId);
            return Ok(new { Score = score });
        }
    }
}
