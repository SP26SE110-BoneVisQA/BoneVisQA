using System.Security.Claims;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Services;
using BoneVisQA.Services.Services.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        private readonly ISupabaseStorageService _storageService;

        public ExpertController(
            IMedicalCaseService medicalService,
            IQuizsService quizService,
            ITagCaseService tagCaseService,
            ISupabaseStorageService storageService)
        {
            _medicalcaseService = medicalService;
            _quizService = quizService;
            _tagCaseService = tagCaseService;
            _storageService = storageService;
        }
        
        [HttpGet("cases")]
        [ProducesResponseType(typeof(PagedResult<GetMedicalCaseDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllMedicalCases(int pageIndex = 1,int pageSize = 10)
        {
            var result = await _medicalcaseService.GetAllMedicalCasesAsync(pageIndex, pageSize);
            return Ok(result);
        }

        [HttpGet("cases/{id:guid}")]
        [ProducesResponseType(typeof(GetExpertMedicalCaseDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMedicalCaseById([FromRoute] Guid id)
        {
            var result = await _medicalcaseService.GetMedicalCaseByIdAsync(id);
            if (result == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = "The requested medical case was not found.",
                    Instance = HttpContext.Request.Path.Value ?? HttpContext.Request.Path.ToString()
                });
            }

            return Ok(result);
        }
     
        [Consumes("application/json")]
        public async Task<IActionResult> CreateCase([FromBody] CreateExpertMedicalCaseJsonRequest body, CancellationToken cancellationToken)
        {
            if (body == null)
                return BadRequest(new { message = "Request body is required." });

            var expertIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(expertIdStr, out var expertId) || expertId == Guid.Empty)
                return Unauthorized(new { message = "Token does not contain a valid user id." });

            var created = await _medicalcaseService.CreateMedicalCaseWithImagesJsonAsync(body, expertId, cancellationToken);
            return Ok(new
            {
                message = "Medical case created successfully",
                caseId = created.Id,
                result = created
            });
        }

        [HttpPut("cases/{id:guid}")]
        [Consumes("application/json")]
        public async Task<IActionResult> UpdateMedicalCase([FromRoute] Guid id, [FromBody] UpdateMedicalCaseDTORequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Request body is required." });

            var result = await _medicalcaseService.UpdateMedicalCaseAsync(id, request);

            if (result == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = "The requested medical case was not found.",
                    Instance = HttpContext.Request.Path.Value ?? HttpContext.Request.Path.ToString()
                });
            }

            return Ok(result);
        }

        [HttpDelete("cases/{id}")]
        public async Task<IActionResult> DeleteMedicalCase(Guid id)
        {
            var result = await _medicalcaseService.DeleteMedicalCaseAsync(id);

            if (!result)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = "The requested medical case was not found.",
                    Instance = HttpContext.Request.Path.Value ?? HttpContext.Request.Path.ToString()
                });
            }

            return Ok(new { message = "Medical case deleted successfully." });
        }

        //=====================================================   IMAGE & ANNOTATION  ==========================================================

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

        [HttpDelete("images/{imageId:guid}")]
        public async Task<IActionResult> DeleteImage([FromRoute] Guid imageId)
        {
            var deleted = await _medicalcaseService.DeleteMedicalImageAsync(imageId);
            if (!deleted)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = "Medical image not found."
                });
            }
            return Ok(new { message = "Medical image deleted successfully." });
        }

        [HttpPost("quiz-questions/upload-image")]
        [RequestSizeLimit(10485760)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadQuizQuestionImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required." });

            if (file.Length > 10485760)
                return BadRequest(new { message = "File size exceeds 10MB limit." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Only JPG, PNG, GIF, WEBP files are allowed." });

            try
            {
                var url = await _storageService.UploadFileAsync(file, "medical-cases", "expert-workbench");
                return Ok(new { url });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Upload failed: {ex.Message}" });
            }
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
            var result = await _quizService.GetQuizAsync(pageIndex, pageSize);

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

            // Lấy ExpertId từ JWT token và gán vào request
            var expertIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(expertIdStr, out var expertId) || expertId == Guid.Empty)
                return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

            // Gán CreatedByExpertId để quiz được hiển thị ở phía Lecturer
            request.CreatedByExpertId = expertId;

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
        [HttpPatch("questions")]
        public async Task<IActionResult> PatchQuestion(UpdateQuizQuestionRequestDTO request)
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
        [HttpPatch("questions/{questionId}")]
        public async Task<IActionResult> PatchQuestionById(Guid questionId, [FromBody] UpdateQuizQuestionRequestDTO request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            request.QuestionId = questionId;
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
        // Assign quiz to class - using /api/expert/quizzes/assign (matches frontend)
        [HttpPost("quizzes/assign")]
        public async Task<IActionResult> AssignToClassViaQuizzes(AssignQuizRequestDTO dto)
        {
            var result = await _quizService.AssignQuizToClassAsync(dto);
            return Ok(new
            {
                Message = "AssignQuiz successfully.",
                result
            });
        }

        // Keep original /api/expert/assign for backward compatibility
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
            var result = await _tagCaseService.GetAllTag(pageIndex, pageSize);

            return Ok(result);
        }

        [HttpGet("tags")]
        public Task<IActionResult> GetAllTags(int pageIndex = 1, int pageSize = 10) =>
            GetAllTag(pageIndex, pageSize);

        //================================================================================================================
        // Deep Classification - Lấy dữ liệu cho dropdown trong Create/Edit Quiz
        //================================================================================================================

        [HttpGet("bone-specialties/tree")]
        public async Task<IActionResult> GetBoneSpecialtiesTree()
        {
            try
            {
                var result = await _quizService.GetBoneSpecialtiesTreeAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("pathology-categories")]
        public async Task<IActionResult> GetPathologyCategories()
        {
            try
            {
                var result = await _quizService.GetPathologyCategoriesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("image")]
        public async Task<IActionResult> GetAllImage( int pageIndex = 1,int pageSize = 10)
        {
            var result = await _medicalcaseService.GetAllImage(pageIndex, pageSize);

            return Ok(result);
        }

        [HttpGet("annotation")]
        public async Task<IActionResult> GetAllAnnotation(int pageIndex = 1, int pageSize = 10)
        {
            var result = await _medicalcaseService.GetAllAnnotation(pageIndex, pageSize);

            return Ok(result);
        }

        [HttpGet("assign")]
        public async Task<IActionResult> GetAssignQuizList(int pageIndex = 1,int pageSize = 10)
        {
            var result = await _quizService.GetAssignQuizDTO(pageIndex, pageSize);

            return Ok(result);
        }

        #region Expert Quiz Library - Expert xem và assign quiz từ thư viện

        /// <summary>
        /// Expert xem thư viện quiz từ tất cả Experts (bao gồm quiz của các Expert khác).
        /// Dùng để chọn quiz để assign vào lớp học.
        /// </summary>
        [HttpGet("library/quizzes")]
        public async Task<ActionResult<PagedResult<ExpertQuizForLecturerDto>>> GetExpertQuizLibrary(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? topic = null,
            [FromQuery] string? difficulty = null,
            [FromQuery] string? classification = null)
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _quizService.GetExpertQuizzesForLecturerAsync(
                pageIndex, pageSize, topic, difficulty, classification);

            return Ok(result);
        }

        /// <summary>
        /// Xem chi tiết một quiz trong thư viện Expert.
        /// </summary>
        [HttpGet("library/quizzes/{quizId:guid}")]
        public async Task<ActionResult<ExpertQuizForLecturerDto>> GetExpertQuizFromLibrary(Guid quizId)
        {
            try
            {
                var result = await _quizService.GetExpertQuizzesForLecturerAsync(
                    pageIndex: 1,
                    pageSize: 1,
                    topic: null,
                    difficulty: null,
                    classification: null);

                var quiz = result.Items.FirstOrDefault(q => q.Id == quizId);
                if (quiz == null)
                    return NotFound(new { message = "Quiz không tồn tại trong thư viện Expert." });

                return Ok(quiz);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Xem trước câu hỏi của quiz trong thư viện Expert.
        /// </summary>
        [HttpGet("library/quizzes/{quizId:guid}/questions")]
        public async Task<IActionResult> GetExpertQuizQuestionsFromLibrary(Guid quizId)
        {
            try
            {
                var result = await _quizService.GetExpertQuizQuestionsAsync(quizId);
                return Ok(new
                {
                    message = "Lấy câu hỏi thành công",
                    quizId = quizId,
                    questionCount = result.Count,
                    questions = result
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Expert gán quiz từ thư viện vào lớp học của mình.
        /// Hệ thống tự động gán TẤT CẢ câu hỏi trong quiz đó vào lớp.
        /// </summary>
        [HttpPost("classes/{classId:guid}/library-quizzes/{quizId:guid}")]
        public async Task<IActionResult> AssignExpertQuizFromLibraryToClass(
            Guid classId,
            Guid quizId,
            [FromBody] AssignExpertQuizRequestDto? request = null)
        {
            try
            {
                // Verify quiz exists in library
                var quiz = await _quizService.GetExpertQuizzesForLecturerAsync(
                    pageIndex: 1, pageSize: 1);

                var expertQuiz = quiz.Items.FirstOrDefault(q => q.Id == quizId);
                if (expertQuiz == null)
                    return NotFound(new { message = "Quiz không tồn tại trong thư viện Expert." });

                // Convert to AssignQuizRequestDTO
                var assignRequest = new AssignQuizRequestDTO
                {
                    ClassId = classId,
                    QuizId = quizId,
                    AssignedExpertId = null,
                    OpenTime = request?.OpenTime,
                    CloseTime = request?.CloseTime,
                    PassingScore = request?.PassingScore,
                    TimeLimitMinutes = request?.TimeLimitMinutes
                };

                var result = await _quizService.AssignQuizToClassAsync(assignRequest);

                return Ok(new
                {
                    message = $"Đã gán quiz '{expertQuiz.Title}' vào lớp thành công.",
                    result = result,
                    questionCount = expertQuiz.QuestionCount,
                    note = "Quiz này có " + expertQuiz.QuestionCount + " câu hỏi. Tất cả câu hỏi đã được gán cho lớp."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Kiểm tra xem một quiz đã được gán vào lớp nào chưa.
        /// Dùng để hiển thị warning cho Expert khi edit quiz.
        /// </summary>
        [HttpGet("quizzes/{quizId:guid}/assignment-status")]
        public async Task<IActionResult> GetQuizAssignmentStatus(Guid quizId)
        {
            try
            {
                var (isAssigned, count) = await _quizService.IsQuizAssignedAsync(quizId);

                return Ok(new
                {
                    isAssigned = isAssigned,
                    assignedClassCount = count,
                    message = isAssigned
                        ? $"Quiz này đã được gán vào {count} lớp. Thay đổi timeLimit/score sẽ KHÔNG ảnh hưởng đến các lớp đã gán."
                        : "Quiz chưa được gán vào lớp nào."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion
    }
}
