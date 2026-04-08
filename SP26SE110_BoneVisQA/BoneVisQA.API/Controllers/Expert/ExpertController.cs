using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Services;
using BoneVisQA.Services.Services.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoneVisQA.API.Controllers.Expert
{
    [Authorize(Roles = "Expert")]
    [ApiController]
    [Route("api/expert")]
    [Tags("Expert")]
    public class ExpertController : ControllerBase
    {
        private readonly IMedicalCaseService _medicalcaseService;
        private readonly IQuizsService _quizService;
        private readonly ITagCaseService _tagCaseService;

        public ExpertController(IMedicalCaseService medicalService, IQuizsService quizService, ITagCaseService tagCaseService)
        {
            _medicalcaseService = medicalService;
            _quizService = quizService;
            _tagCaseService = tagCaseService;
        }
        [HttpGet("cases")]
        public async Task<IActionResult> GetAllMedicalCases(int pageIndex = 1,int pageSize = 10)
        {
            var result = await _medicalcaseService.GetAllMedicalCasesAsync(pageIndex, pageSize);

            return Ok(result);
        }

        [HttpPost("cases")]
        public async Task<IActionResult> CreateCase(CreateMedicalCaseRequestDTO dto)
        {
            var caseId = await _medicalcaseService.CreateMedicalCaseAsync(dto);

            return Ok(new
            {
                message = "Medical case created successfully",
                caseId
            });
        }

        [HttpPut("cases/{id}")]
        public async Task<IActionResult> UpdateMedicalCase(Guid id,UpdateMedicalCaseDTORequest request)
        {
            var result = await _medicalcaseService.UpdateMedicalCaseAsync(id, request);

            if (result == null)
                return NotFound("Medical case not found");

            return Ok(result);
        }

        [HttpDelete("cases/{id}")]
        public async Task<IActionResult> DeleteMedicalCase(Guid id)
        {
            var result = await _medicalcaseService.DeleteMedicalCaseAsync(id);

            if (!result)
                return NotFound("Medical case not found");

            return Ok("Delete medical case successfully");
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
        //=====================================================   QUIZ  ==========================================================
        [HttpGet("quizzes")]
        public async Task<IActionResult> GetQuizzes(int pageIndex = 1, int pageSize = 10)
        {
            var result = await _quizService.GetQuizDTO(pageIndex, pageSize);

            return Ok(new
            {
                message = "Get quizzes successfully",
                result
            });
        }
        [HttpPost("quizzes")]
        public async Task<IActionResult> CreateQuiz([FromBody] CreateQuizRequestDTO request)
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
        [HttpPut("quizzes")]
        public async Task<IActionResult> UpdateQuiz(UpdateQuizRequestDTO request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            var result = await _quizService.UpdateQuizAsync(request);

            return Ok(new
            {
                message = "Quiz updated successfully",
                result
            });
        }
        [HttpDelete("quizzes/{quizId}")]
        public async Task<IActionResult> DeleteQuiz(Guid quizId)
        {
            var deleted = await _quizService.DeleteQuizAsync(quizId);

            if (!deleted)
            {
                return NotFound("Quiz not found");
            }

            return Ok(new
            {
                message = "Quiz deleted successfully"
            });
        }

        //=====================================================   QUESTION  ==========================================================

        [HttpGet("quizzes/{quizId}/questions")]
        public async Task<IActionResult> GetQuestionsByQuiz(Guid quizId)
        {
            var result = await _quizService.GetQuizQuestionDTO(quizId);

            return Ok(new
            {
                message = "Get quiz questions successfully",
                result
            });
        }
        [HttpPost("quizzes/{quizId}/questions")]
        public async Task<IActionResult> CreateQuestion(Guid quizId, CreateQuizQuestionRequestDTO request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            var result = await _quizService.CreateQuizQuestionAsync(quizId, request);

            return Ok(new
            {
                message = "Quiz_Question created successfully",
                result
            });
        }
        [HttpPut("questions")]
        public async Task<IActionResult> UpdateQuestion(UpdateQuizQuestionRequestDTO request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            var result = await _quizService.UpdateQuizQuestionAsync(request);

            return Ok(new
            {
                message = "Quiz question updated successfully",
                result
            });
        }
        [HttpDelete("questions/{questionId}")]
        public async Task<IActionResult> DeleteQuestion(Guid questionId)
        {
            var deleted = await _quizService.DeleteQuizQuestionAsync(questionId);

            if (!deleted)
            {
                return NotFound("Question not found");
            }

            return Ok(new
            {
                message = "Quiz question deleted successfully"
            });
        }
        //================================================================================================================
        [HttpPost("assign")]
        public async Task<IActionResult> AssignToClass(AssignQuizRequestDTO dto)
        {
            var result = await _quizService.AssignQuizToClassAsync(dto);
            return Ok(new
            {
                Message = "AssignQuiz successfully.",
                result
            });
        }

        [HttpGet("attempts/{quizId}")]
        public async Task<IActionResult> GetAttempts(Guid quizId)
        {
            var result = await _quizService.GetAttemptsByQuizAsync(quizId);

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

        //==================================================================================================
       
        [HttpGet("category")]
        public async Task<IActionResult> GetCategories(int pageIndex = 1, int pageSize = 10)
        {
            var result = await _medicalcaseService.GetAllCategory(pageIndex, pageSize);

            return Ok(result);
        }
        [HttpGet("class")]
        public async Task<IActionResult> GetAllClass(int pageIndex = 1,int pageSize = 10)
        {
            var result = await _quizService.GetAllClass(pageIndex, pageSize);

            return Ok(result);
        }
        [HttpGet("expert")]
        public async Task<IActionResult> GetAllExpert(int pageIndex = 1,int pageSize = 10)
        {
            var result = await _quizService.GetAllExpert(pageIndex, pageSize);

            return Ok(result);
        }
        [HttpGet("tag")]
        public async Task<IActionResult> GetAllTag(int pageIndex = 1,int pageSize = 10)
        {
            var result = await _medicalcaseService.GetAllTag(pageIndex, pageSize);

            return Ok(result);
        }

        [HttpGet("image")]
        public async Task<IActionResult> GetAllImage( int pageIndex = 1,int pageSize = 10)
        {
            var result = await _medicalcaseService.GetAllImage(pageIndex, pageSize);

            return Ok(result);
        }

        [HttpGet("assign")]
        public async Task<IActionResult> GetAssignQuizList(int pageIndex = 1,int pageSize = 10)
        {
            var result = await _quizService.GetAssignQuizDTO(pageIndex, pageSize);

            return Ok(result);
        }
    }
}
