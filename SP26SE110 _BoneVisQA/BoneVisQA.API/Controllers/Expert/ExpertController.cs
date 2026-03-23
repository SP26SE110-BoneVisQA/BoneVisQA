using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Expert
{
    [Authorize(Roles = "Expert")]
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
        public async Task<IActionResult> CreateCase(MedicalCaseDTOResponse dto)
        {
            var caseId = await _medicalcaseService.CreateMedicalCaseAsync(dto);

            return Ok(new
            {
                message = "Medical case created successfully",
                caseId
            });
        }

        [HttpPost("images")]
        public async Task<IActionResult> AddImage([FromForm] AddMedicalImageDTOResponse dto)
        {
            var result = await _medicalcaseService.AddImageAsync(dto);
            return Ok(new
            {
                message = "Medical_Image created successfully",
                result
            });
        }

        [HttpPost("annotations")]
        public async Task<IActionResult> AddAnnotation([FromBody] AddAnnotationDTOResponse dto)
        {
            var result = await _medicalcaseService.AddAnnotationAsync(dto);
            return Ok(new
            {
                message = "Medical_Annotation created successfully",
                result
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
        public async Task<IActionResult> CreateQuestion(Guid quizId,CreateQuizQuestionDTO request)
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


        [HttpGet("{quizId}")]
        public async Task<IActionResult> GetQuizQuestions(Guid quizId)
        {
            var result = await _quizService.GetQuizQuestionsAsync(quizId);

            if (result == null || !result.Any())
            {
                return NotFound("No quiz questions found.");
            }

            return Ok(result);
        }

        [HttpPut("update-question/{questionId}")]
        public async Task<IActionResult> UpdateQuizQuestion(
            Guid questionId,
            [FromBody] UpdateQuizsQuestionRequestDto request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request data.");
            }

            var updated = await _quizService.UpdateQuizQuestionAsync(questionId, request);

            if (!updated)
            {
                return NotFound("Quiz question not found.");
            }

            return Ok("Update quiz question successfully.");
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

        // POST api/quizzes/submit
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitAnswer(
            [FromQuery] Guid studentId,
            [FromBody] StudentSubmitQuestionDTO submit)
        {
            var result = await _quizService.StudentSubmitQuestionsAsync(studentId, submit);
            return Ok(result);
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
