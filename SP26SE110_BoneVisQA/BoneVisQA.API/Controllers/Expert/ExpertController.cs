using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Expert
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExpertController : ControllerBase
    {
        private readonly IMedicalCaseService _medicalcaseService;
        private readonly IQuizService _quizService;
        private readonly ITagCaseService _tagCaseService;

        public ExpertController(IMedicalCaseService medicalService, IQuizService quizService, ITagCaseService tagCaseService)
        {
            _medicalcaseService = medicalService;
            _quizService = quizService;
            _tagCaseService = tagCaseService;
        }

        [HttpPost("cases")]
        public async Task<IActionResult> CreateCase(CreateMedicalCaseDTO dto)
        {
            var caseId = await _medicalcaseService.CreateMedicalCaseAsync(dto);

            return Ok(new
            {
                message = "Medical case created successfully",
                caseId
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

            return Ok(new
            {
                message = "Quiz created successfully",
                result
            });
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

            return Ok(new
            {
                message = "Quiz_Question created successfully",
                result
            });
        }

        [HttpPost("class/{classId}/assign/{quizId}")]
        public async Task<IActionResult> AssignToClass(Guid classId, Guid quizId)
        { 
            var result = await _quizService.AssignQuizToClassAsync(classId, quizId);
            return Ok(new
            { 
                Message = "AssignQuiz successfully.",
                result
            });
        }

        [HttpPost("attempts/{attemptId}/score")]
        public async Task<IActionResult> CalculateScore(Guid attemptId)
        {
            var result = await _quizService.CalculateScoreAsync(attemptId);
            return Ok(new
            {
                Message = "Calculate score successfully.", 
                result
            });
        }
        [HttpPost("case-tag")]
        public async Task<IActionResult> AddTags([FromBody] CaseTagDTO dto)
        {
            var result = await _tagCaseService.AddTagCasesAsync(dto);

            return Ok(new
            {
                Message = "Tags added successfully",
                result
            });
        }
    }
}
