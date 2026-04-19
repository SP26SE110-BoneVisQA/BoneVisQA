using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Models.Quiz;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer")]
[Tags("Lecturer - Classes")]
[Authorize(Roles = "Lecturer")]
public class LecturersController : ControllerBase
{
    private readonly ILecturerService _lecturerService;
    private readonly IAIQuizService _aiQuizService;
    private readonly IQuizsService _quizService;

    public LecturersController(ILecturerService lecturerService, IAIQuizService aiQuizService, IQuizsService quizService)
    {
        _lecturerService = lecturerService;
        _aiQuizService = aiQuizService;
        _quizService = quizService;
    }

    private static Guid? TryGetJwtUserId(ClaimsPrincipal user)
    {
        var s = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(s, out var id) && id != Guid.Empty ? id : null;
    }

    #region Class Management

    private Guid? GetLecturerId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    // Class CRUD + roster: Admin only (Phòng Đào Tạo). Lecturer: read-only class list + students.

    [HttpGet("classes/{classId:guid}")]
    public async Task<ActionResult<ClassDto>> GetClassById(Guid classId)
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _lecturerService.GetClassByIdAsync(lecturerId.Value, classId);
        if (result == null)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền truy cập lớp học này." });
        return Ok(result);
    }

    [HttpGet("classes")]
    public async Task<ActionResult<IReadOnlyList<ClassDto>>> GetClasses()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _lecturerService.GetClassesForLecturerAsync(lecturerId.Value);
        return Ok(result);
    }

    // Roster changes: Admin only — POST/PUT/DELETE .../api/admin/classes/enrollments

    [HttpGet("classes/{classId:guid}/students")]
    public async Task<ActionResult<IReadOnlyList<StudentEnrollmentDto>>> GetStudentsInClass(Guid classId)
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        IReadOnlyList<StudentEnrollmentDto> result;
        try
        {
            result = await _lecturerService.GetStudentsInClassAsync(lecturerId.Value, classId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        return Ok(result);
    }

    // DISABLED: GetAvailableStudents — Lecturer không thêm student
    // [HttpGet("classes/{classId:guid}/students/available")]
    // public async Task<ActionResult<IReadOnlyList<StudentEnrollmentDto>>> GetAvailableStudents(Guid classId)
    // {
    //     var lecturerId = GetLecturerId();
    //     if (lecturerId == null)
    //         return Unauthorized(new { message = "Token không chứa user id hợp lệ." });
    //
    //     IReadOnlyList<StudentEnrollmentDto> result;
    //     try
    //     {
    //         result = await _lecturerService.GetAvailableStudentsAsync(lecturerId.Value, classId);
    //     }
    //     catch (UnauthorizedAccessException ex)
    //     {
    //         return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
    //     }
    //     return Ok(result);
    // }

    // DISABLED: ImportStudentsFromExcel — Lecturer không thêm student
    // [HttpPost("classes/{classId:guid}/import-students")]
    // [Consumes("multipart/form-data")]
    // public async Task<ActionResult<ImportStudentsSummaryDto>> ImportStudentsFromExcel(Guid classId, IFormFile file)
    // {
    //     if (file == null || file.Length == 0)
    //         return BadRequest(new { message = "File không được để trống." });
    //
    //     var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    //     if (extension != ".xlsx" && extension != ".xls")
    //         return BadRequest(new { message = "Chỉ chấp nhận file .xlsx hoặc .xls." });
    //
    //     await using var stream = file.OpenReadStream();
    //     var result = await _lecturerService.ImportStudentsFromExcelAsync(classId, stream, file.FileName);
    //     return Ok(result);
    // }

    [HttpPost("classes/{classId:guid}/announcements")]
    public async Task<ActionResult<AnnouncementDto>> CreateAnnouncement(Guid classId, [FromBody] CreateAnnouncementRequestDto request)
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _lecturerService.CreateAnnouncementAsync(lecturerId.Value, classId, request);
        return Ok(result);
    }

    [HttpGet("classes/{classId:guid}/stats")]
    public async Task<ActionResult<ClassStatsDto>> GetClassStats(Guid classId)
    {
        var result = await _lecturerService.GetClassStatsAsync(classId);
        return Ok(result);
    }

    [HttpGet("classes/{classId:guid}/announcements")]
    public async Task<ActionResult<IReadOnlyList<AnnouncementDto>>> GetClassAnnouncements(Guid classId)
    {
        var result = await _lecturerService.GetClassAnnouncementsAsync(classId);
        return Ok(result);
    }

    [HttpGet("classes/{classId:guid}/assignments")]
    public async Task<ActionResult<List<ClassAssignmentDto>>> GetClassAssignments(Guid classId)
    {
        try
        {
            var result = await _lecturerService.GetClassAssignmentsAsync(classId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Lấy tất cả assignments (case + quiz) của tất cả lớp thuộc giảng viên hiện tại.</summary>
    [HttpGet("assignments")]
    public async Task<ActionResult<List<ClassAssignmentDto>>> GetAllAssignmentsForLecturer()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _lecturerService.GetAllAssignmentsForLecturerAsync(lecturerId.Value);
        return Ok(result);
    }

    /// <summary>Không dùng :guid trên route — nếu id sai sẽ trả 400 thay vì 404 do router.</summary>
    [HttpPut("classes/{classId}/announcements/{announcementId}")]
    public async Task<ActionResult<AnnouncementDto>> UpdateAnnouncement(
        string classId,
        string announcementId,
        [FromBody] UpdateAnnouncementRequestDto request)
    {
        if (!Guid.TryParse(classId, out var cId) || !Guid.TryParse(announcementId, out var aId))
            return BadRequest(new { message = "Invalid class ID or announcement ID." });
        try
        {
            var result = await _lecturerService.UpdateAnnouncementAsync(cId, aId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("classes/{classId}/announcements/{announcementId}")]
    public async Task<IActionResult> DeleteAnnouncement(string classId, string announcementId)
    {
        if (!Guid.TryParse(classId, out var cId) || !Guid.TryParse(announcementId, out var aId))
            return BadRequest(new { message = "Invalid class ID or announcement ID." });
        var deleted = await _lecturerService.DeleteAnnouncementAsync(cId, aId);
        if (!deleted)
            return NotFound(new { message = "Announcement does not exist." });
        return NoContent();
    }

    #endregion

    #region Quiz Management

    /// <summary>Tạo quiz. Không cần gửi id (server tự sinh). Gửi classId để gán quiz vào lớp.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateQuiz([FromBody] CreateQuizRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var creatorId = TryGetJwtUserId(User);
            var result = await _lecturerService.CreateQuizAsync(request, creatorId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Danh sách quiz gắn với mọi lớp của giảng viên.</summary>
    [HttpGet("quizzes")]
    public async Task<ActionResult<IReadOnlyList<ClassQuizDto>>> GetQuizzesForLecturer()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _lecturerService.GetQuizzesByLecturerAsync(lecturerId.Value);
        return Ok(result);
    }

    /// <summary>Danh sách quiz của một lớp.</summary>
    [HttpGet("classes/{classId:guid}/quizzes")]
    public async Task<ActionResult<IReadOnlyList<QuizDto>>> GetQuizzesForClass(Guid classId)
    {
        var result = await _lecturerService.GetQuizzesForClassAsync(classId);
        return Ok(result);
    }

    /// <summary>Nhiều quiz theo danh sách id (query: quizIds=id1&amp;quizIds=id2).</summary>
    [HttpGet("quizzes/batch")]
    public async Task<ActionResult<IReadOnlyList<QuizDto>>> GetQuizzesByIds([FromQuery] Guid[] quizIds)
    {
        if (quizIds == null || quizIds.Length == 0)
            return Ok(Array.Empty<QuizDto>());
        var result = await _lecturerService.GetQuizzesByIdsAsync(quizIds);
        return Ok(result);
    }

    /// <summary>Lấy thông tin một quiz (không kèm câu hỏi).</summary>
    [HttpGet("quizzes/{quizId:guid}")]
    public async Task<ActionResult<QuizDto>> GetQuiz(Guid quizId)
    {
        var result = await _lecturerService.GetQuizByIdAsync(quizId);
        if (result == null)
            return NotFound(new { message = "Quiz không tồn tại." });
        return Ok(result);
    }

    /// <summary>Cập nhật thông tin quiz.</summary>
    [HttpPut("quizzes/{quizId:guid}")]
    public async Task<ActionResult<QuizDto>> UpdateQuiz(Guid quizId, [FromBody] UpdateQuizRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _lecturerService.UpdateQuizAsync(quizId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Lấy danh sách câu hỏi của một quiz.</summary>
    [HttpGet("quizzes/{quizId:guid}/questions")]
    public async Task<IActionResult> GetQuizQuestions(Guid quizId)
    {
        var result = await _lecturerService.GetQuizQuestionsAsync(quizId);
        if (result == null || !result.Any())
            return NotFound(new { message = "No questions found." });
        return Ok(result);
    }

    /// <summary>Lấy thông tin một câu hỏi theo id.</summary>
    [HttpGet("quizzes/questions/{questionId:guid}")]
    public async Task<ActionResult<QuizQuestionDto>> GetQuizQuestionById(Guid questionId)
    {
        var result = await _lecturerService.GetQuizQuestionByIdAsync(questionId);
        if (result == null)
            return NotFound(new { message = "Câu hỏi không tồn tại." });
        return Ok(result);
    }

    [HttpPost("quizzes/{quizId:guid}/questions")]
    public async Task<IActionResult> AddQuizQuestion(Guid quizId, [FromBody] CreateQuizQuestionDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _lecturerService.AddQuizQuestionAsync(quizId, request);
        return Ok(result);
    }

    [HttpPost("quizzes/{quizId:guid}/questions/batch")]
    public async Task<IActionResult> AddQuizQuestionsBatch(Guid quizId, [FromBody] List<CreateQuizQuestionDto> requests)
    {
        if (requests == null || requests.Count == 0)
            return BadRequest(new { message = "No questions provided." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var results = await _lecturerService.AddQuizQuestionsBatchAsync(quizId, requests);
            return Ok(results);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    [HttpPut("quizzes/questions/{questionId:guid}")]
    public async Task<IActionResult> UpdateQuizQuestion(Guid questionId, [FromBody] UpdateQuizsQuestionRequestDto request)
    {
        if (request == null)
            return BadRequest("Invalid request data.");

        var updated = await _lecturerService.UpdateQuizQuestionAsync(questionId, request);
            return Ok(new { message = "Question updated successfully." });
    }

    [HttpDelete("quizzes/questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteQuizQuestion(Guid questionId)
    {
        var deleted = await _lecturerService.DeleteQuizQuestionAsync(questionId);
        if (!deleted)
            return NotFound(new { message = "Câu hỏi không tồn tại." });
        return NoContent();
    }

    [HttpDelete("quizzes/{quizId:guid}")]
    public async Task<IActionResult> DeleteQuiz(Guid quizId)
    {
        try
        {
            var lecturerId = GetLecturerId();
            if (lecturerId == null)
                return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

            var deleted = await _lecturerService.DeleteQuizAsync(quizId);
            if (!deleted)
                return NotFound(new { message = "Quiz không tồn tại." });
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = "Không thể xóa", status = 400, detail = ex.Message });
        }
    }

    [HttpPost("classes/{classId:guid}/quizzes/{quizId:guid}")]
    public async Task<ActionResult<ClassQuizDto>> AssignQuizToClass(Guid classId, Guid quizId)
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            var result = await _lecturerService.AssignQuizToClassAsync(classId, quizId);
            return Ok(result);
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

    [HttpDelete("classes/{classId:guid}/quizzes/{quizId:guid}")]
    public async Task<IActionResult> RemoveQuizFromClass(Guid classId, Guid quizId)
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        try
        {
            await _lecturerService.RemoveQuizFromClassAsync(classId, quizId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get quizzes created by the lecturer that are NOT assigned to any class.
    /// Used for "My Quizzes" tab.
    /// </summary>
    [HttpGet("quizzes/my")]
    public async Task<ActionResult<IReadOnlyList<QuizDto>>> GetMyQuizzes()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _lecturerService.GetUnassignedLecturerQuizzesAsync(lecturerId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Get all quizzes created by the lecturer with their assigned classes.
    /// Shows ALL quizzes (including assigned ones) for the "My Quizzes" tab.
    /// Each quiz includes a list of classes it has been assigned to.
    /// </summary>
    [HttpGet("quizzes/my-with-classes")]
    public async Task<ActionResult<IReadOnlyList<MyQuizWithClassesDto>>> GetMyQuizzesWithClasses()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _lecturerService.GetMyQuizzesWithClassesAsync(lecturerId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Get all quizzes available to lecturer (own quizzes + expert library quizzes),
    /// excluding AI-generated quizzes. Used for "My Quizzes" tab with full library.
    /// </summary>
    [HttpGet("quizzes/all")]
    public async Task<ActionResult<IReadOnlyList<QuizDto>>> GetAllLecturerQuizzes()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _lecturerService.GetAllLecturerQuizzesAsync(lecturerId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Get all quiz assignments (ClassQuizSession) for lecturer's classes.
    /// Includes both lecturer-created and expert library quizzes that have been assigned.
    /// Used for "Assigned Quizzes" tab.
    /// </summary>
    [HttpGet("quizzes/assigned")]
    public async Task<ActionResult<IReadOnlyList<AssignedQuizDto>>> GetAssignedQuizzes()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId == null)
            return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

        var result = await _lecturerService.GetAssignedQuizzesAsync(lecturerId.Value);
        return Ok(result);
    }

    #endregion

    #region AI Quiz Management

    /// <summary>
    /// AI Auto-Generate Quiz: Tạo quiz tự động từ topic
    /// </summary>
    [HttpPost("ai/generate-quiz")]
    public async Task<ActionResult<AIQuizGenerationResultDto>> GenerateQuiz([FromBody] AIAutoGenerateQuizRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { message = "Topic là bắt buộc." });

        var result = await _aiQuizService.GenerateQuizQuestionsAsync(
            request.Topic,
            request.QuestionCount,
            request.Difficulty);

        return Ok(result);
    }

    /// <summary>
    /// AI Suggest Questions: Gợi ý câu hỏi từ các cases đã chọn
    /// </summary>
    [HttpPost("ai/suggest-questions")]
    public async Task<ActionResult<AIQuizGenerationResultDto>> SuggestQuestions([FromBody] AISuggestQuestionsRequestDto request)
    {
        if (request.Cases == null || request.Cases.Count == 0)
            return BadRequest(new { message = "At least 1 case is required." });

        var result = await _aiQuizService.SuggestQuestionsFromCasesAsync(
            request.Cases,
            request.QuestionsPerCase);

        return Ok(result);
    }

    /// <summary>
    /// Tạo quiz hoàn chỉnh từ AI (Auto-Generate + Save)
    /// </summary>
    [HttpPost("ai/create-quiz")]
    public async Task<IActionResult> CreateQuizFromAI([FromBody] AIAutoGenerateQuizRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Title là bắt buộc." });

        if (string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { message = "Topic là bắt buộc." });

        try
        {
            // 1. Tạo quiz
            var createRequest = new CreateQuizRequestDto
            {
                Title = request.Title,
                Topic = request.Topic,
                Difficulty = request.Difficulty,
                Classification = request.Classification,
                IsAiGenerated = true,
                ClassId = request.ClassId ?? Guid.Empty,
                OpenTime = request.OpenTime,
                CloseTime = request.CloseTime,
                TimeLimit = request.TimeLimit,
                PassingScore = request.PassingScore
            };

            var creatorId = TryGetJwtUserId(User);
            var quiz = await _lecturerService.CreateQuizAsync(createRequest, creatorId);

            // 2. Tạo questions từ AI
            var questionsResult = await _aiQuizService.GenerateQuizQuestionsAsync(
                request.Topic,
                request.QuestionCount,
                request.Difficulty);

            if (questionsResult.Success && questionsResult.Questions.Count > 0)
            {
                foreach (var q in questionsResult.Questions)
                {
                    var questionRequest = new CreateQuizQuestionDto
                    {
                        QuizId = quiz.Id,
                        CaseId = q.CaseId,
                        QuestionText = q.QuestionText,
                        Type = q.Type,
                        OptionA = q.OptionA,
                        OptionB = q.OptionB,
                        OptionC = q.OptionC,
                        OptionD = q.OptionD,
                        CorrectAnswer = q.CorrectAnswer
                    };

                    await _lecturerService.AddQuizQuestionAsync(quiz.Id, questionRequest);
                }
            }

            return Ok(new
            {
                quizId = quiz.Id,
                title = quiz.Title,
                questionsCreated = questionsResult.Questions.Count,
                message = $"Quiz created with {questionsResult.Questions.Count} questions"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region Cases & Student Questions

    [HttpGet("cases")]
    public async Task<ActionResult<IReadOnlyList<CaseDto>>> GetAllCases()
    {
        var result = await _lecturerService.GetAllCasesAsync();
        return Ok(result);
    }

    [HttpPut("cases/{caseId:guid}/approve")]
    public async Task<IActionResult> ApproveCase(Guid caseId, [FromBody] ApproveCaseRequestDto request)
    {
        var updated = await _lecturerService.ApproveCaseAsync(caseId, request);
        if (!updated)
            return NotFound(new { message = "Case không tồn tại." });
        return NoContent();
    }

    #endregion

    #region Assignment CRUD

    /// <summary>Lấy chi tiết một assignment theo ID.</summary>
    [HttpGet("assignments/{assignmentId:guid}")]
    public async Task<ActionResult<AssignmentDetailDto>> GetAssignmentById(Guid assignmentId)
    {
        try
        {
            var result = await _lecturerService.GetAssignmentByIdAsync(assignmentId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Cập nhật thông tin assignment.</summary>
    [HttpPut("assignments/{assignmentId:guid}")]
    public async Task<ActionResult<AssignmentDetailDto>> UpdateAssignment(Guid assignmentId, [FromBody] UpdateAssignmentRequestDto request)
    {
        try
        {
            var result = await _lecturerService.UpdateAssignmentAsync(assignmentId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Xóa một assignment.</summary>
    [HttpDelete("assignments/{assignmentId:guid}")]
    public async Task<IActionResult> DeleteAssignment(Guid assignmentId)
    {
        try
        {
            await _lecturerService.DeleteAssignmentAsync(assignmentId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Lấy danh sách submissions của một assignment.</summary>
    [HttpGet("assignments/{assignmentId:guid}/submissions")]
    public async Task<ActionResult<IReadOnlyList<AssignmentSubmissionDto>>> GetAssignmentSubmissions(Guid assignmentId)
    {
        try
        {
            var result = await _lecturerService.GetAssignmentSubmissionsAsync(assignmentId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Cập nhật điểm cho nhiều submissions.</summary>
    [HttpPut("assignments/{assignmentId:guid}/submissions")]
    public async Task<ActionResult<IReadOnlyList<AssignmentSubmissionDto>>> UpdateAssignmentSubmissions(
        Guid assignmentId, [FromBody] UpdateSubmissionsRequestDto request)
    {
        try
        {
            var result = await _lecturerService.UpdateAssignmentSubmissionsAsync(assignmentId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    #endregion

    #region QA Triage

    /// <summary>Danh sách câu trả lời cần triage cho một lớp (cho trang QA Triage).</summary>
    [HttpGet("triage")]
    public async Task<ActionResult<IReadOnlyList<LecturerTriageRowDto>>> GetTriageList([FromQuery] Guid classId)
    {
        try
        {
            var result = await _lecturerService.GetTriageListAsync(classId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("classes/{classId:guid}/questions/{questionId:guid}")]
    public async Task<ActionResult<LectStudentQuestionDetailDto>> GetQuestionDetail(Guid classId, Guid questionId)
    {
        var result = await _lecturerService.GetQuestionDetailAsync(classId, questionId);
        if (result == null)
            return NotFound(new { message = "Câu hỏi không tồn tại." });
        return Ok(result);
    }

    [HttpPut("classes/{classId:guid}/questions/{questionId:guid}/respond")]
    public async Task<ActionResult<LecturerAnswerDto>> RespondToQuestion(
        Guid classId,
        Guid questionId,
        [FromBody] RespondToQuestionRequestDto request)
    {
        try
        {
            var result = await _lecturerService.RespondToQuestionAsync(classId, questionId, request);
            return Ok(result);
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

    /// <summary>Tiến độ học tập của tất cả sinh viên trong một lớp.</summary>
    [HttpGet("classes/{classId:guid}/student-progress")]
    public async Task<ActionResult<IReadOnlyList<ClassStudentProgressDto>>> GetClassStudentProgress(Guid classId)
    {
        try
        {
            var result = await _lecturerService.GetClassStudentProgressAsync(classId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("classes/{classId:guid}/questions")]
    public async Task<ActionResult<IReadOnlyList<LectStudentQuestionDto>>> GetStudentQuestions(Guid classId, [FromQuery] Guid? caseId, [FromQuery] Guid? studentId)
    {
        var result = await _lecturerService.GetStudentQuestionsAsync(classId, caseId, studentId);
        return Ok(result);
    }

    #endregion

    #region Expert Quiz Library - Lecturer lấy quiz từ Expert

    // ================================================================================
    // LUỒNG HOẠT ĐỘNG:
    // 1. Lecturer gọi GET /expert-quizzes → Xem danh sách quiz của Expert
    // 2. Lecturer chọn quiz cụ thể → Gọi GET /expert-quizzes/{id}/questions → Xem trước câu hỏi
    // 3. Lecturer quyết định gán quiz → Gọi POST /classes/{classId}/expert-quizzes/{quizId}
    // 4. Hệ thống tự động gán TẤT CẢ câu hỏi trong quiz đó vào lớp (không chọn số lượng)
    // ================================================================================

    /// <summary>
    /// Bước 1: Lấy danh sách quiz từ thư viện của Expert.
    /// 
    /// Mô tả luồng:
    /// - Expert đã tạo các bộ quiz với số lượng câu hỏi khác nhau (vd: 5 câu, 10 câu)
    /// - Lecturer muốn xem thư viện quiz của Expert để chọn quiz phù hợp cho lớp mình
    /// - API này trả về danh sách quiz kèm số câu hỏi trong mỗi quiz
    /// 
    /// Query params:
    /// - topic: Lọc theo chủ đề (vd: "Lower Limb", "Chest X-Ray")
    /// - difficulty: Lọc theo độ khó (Easy, Medium, Hard)
    /// - classification: Lọc theo phân loại (vd: "Year 1", "Year 2")
    /// - pageIndex, pageSize: Phân trang kết quả
    /// 
    /// Trả về:
    /// - Danh sách quiz với thông tin: tiêu đề, chủ đề, độ khó, số câu hỏi, tên Expert đã tạo
    /// </summary>
    [HttpGet("expert-quizzes")]
    public async Task<ActionResult<PagedResult<ExpertQuizForLecturerDto>>> GetExpertQuizzes(
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
    /// Bước 2 (optional): Lấy thông tin chi tiết một quiz cụ thể từ thư viện Expert.
    /// 
    /// Mô tả luồng:
    /// - Lecturer đã xem danh sách quiz, muốn xem chi tiết một quiz cụ thể
    /// - API này trả về thông tin đầy đủ của quiz đó
    /// 
    /// Trả về:
    /// - Thông tin quiz: tiêu đề, thời gian mở/đóng, thời gian làm bài, điểm đạt, số câu hỏi
    /// </summary>
    [HttpGet("expert-quizzes/{quizId:guid}")]
    public async Task<ActionResult<ExpertQuizForLecturerDto>> GetExpertQuizById(Guid quizId)
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
    /// Bước 3 (optional): Xem trước câu hỏi trong quiz trước khi gán vào lớp.
    ///
    /// Mô tả luồng:
    /// - Lecturer đã chọn được quiz phù hợp
    /// - Trước khi gán vào lớp, Lecturer muốn xem trước các câu hỏi
    /// - API này trả về danh sách câu hỏi với các lựa chọn (A, B, C, D)
    /// - CÓ trả về đáp án đúng (CorrectAnswer) vì Expert đã tạo kèm đáp án
    ///
    /// Trả về:
    /// - Danh sách câu hỏi với: nội dung câu hỏi, các lựa chọn, đáp án đúng, case liên quan
    /// </summary>
    [HttpGet("expert-quizzes/{quizId:guid}/questions")]
    public async Task<IActionResult> GetExpertQuizQuestions(Guid quizId)
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
    /// Bước 4: Gán quiz vào lớp học - TỰ ĐỘNG TẠO BẢN SAO từ quiz Expert.
    /// 
    /// Mô tả luồng:
    /// - Lecturer đã xem và chọn được quiz phù hợp cho lớp mình
    /// - Lecturer quyết định gán quiz này vào lớp
    /// - Hệ thống sẽ TỰ ĐỘNG tạo bản sao (copy) của quiz Expert
    /// - Sau đó gán bản sao đó vào ClassQuizSession
    /// 
    /// TẠI SAO CẦN COPY:
    /// - Tránh việc chỉnh sửa câu hỏi của Lecturer ảnh hưởng đến quiz gốc của Expert
    /// - Quiz gốc của Expert luôn được bảo toàn
    /// - Lecturer có thể tùy chỉnh bản sao (đổi tên, sửa câu hỏi) mà không ảnh hưởng ai
    /// 
    /// Ví dụ:
    /// - Quiz "Lower Limb Module" của Expert → Hệ thống tạo "Copy of Lower Limb Module"
    /// - "Copy of Lower Limb Module" được gán vào lớp
    /// - Lecturer có thể edit "Copy of Lower Limb Module" tùy ý
    /// 
    /// Body (optional):
    /// - openTime, closeTime: Thời gian mở/đóng quiz
    /// - timeLimitMinutes: Thời gian làm bài (override quiz gốc)
    /// - passingScore: Điểm đạt (override quiz gốc)
    /// 
    /// Trả về:
    /// - Thông báo thành công kèm số câu hỏi đã gán
    /// - Có thông tin về bản sao đã tạo
    /// </summary>
    [HttpPost("classes/{classId:guid}/expert-quizzes/{quizId:guid}")]
    public async Task<IActionResult> AssignExpertQuizToClass(
        Guid classId,
        Guid quizId,
        [FromBody] AssignExpertQuizRequestDto? request = null)
    {
        try
        {
            var lecturerId = GetLecturerId();
            if (lecturerId == null)
                return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

            // ============================================
            // BƯỚC 1: Lấy quiz từ Expert Library để kiểm tra tồn tại
            // ============================================
            var quiz = await _quizService.GetExpertQuizzesForLecturerAsync(
                pageIndex: 1,
                pageSize: 1000,
                topic: null,
                difficulty: null,
                classification: null);
            
            var expertQuiz = quiz.Items.FirstOrDefault(q => q.Id == quizId);
            if (expertQuiz == null)
                return NotFound(new { message = "Quiz không tồn tại trong thư viện Expert hoặc quiz này không phải do Expert tạo." });

            // ============================================
            // BƯỚC 2: TẠO BẢN SAO của quiz Expert
            // Quiz mới sẽ không có CreatedByExpertId
            // Lecturer có thể edit bản sao này
            // ============================================
            var copiedQuiz = await _quizService.CopyExpertQuizForLecturerAsync(
                expertQuizId: quizId,
                lecturerId: lecturerId.Value,
                newTitle: request?.TitleOverride // Cho phép đổi tên nếu muốn
            );

            // ============================================
            // BƯỚC 3: Gán bản sao vào lớp
            // ============================================
            var assignRequest = new AssignQuizRequestDTO
            {
                ClassId = classId,
                QuizId = copiedQuiz.NewQuizId, // Gán quiz BẢN SAO, không phải quiz gốc
                AssignedExpertId = null,
                OpenTime = request?.OpenTime,
                CloseTime = request?.CloseTime,
                PassingScore = request?.PassingScore,
                TimeLimitMinutes = request?.TimeLimitMinutes
            };

            var result = await _quizService.AssignQuizToClassAsync(assignRequest);

            return Ok(new
            {
                message = $"Đã gán quiz '{copiedQuiz.NewQuizTitle}' vào lớp thành công.",
                result = result,
                questionCount = copiedQuiz.QuestionCount,
                copiedQuiz = new
                {
                    newQuizId = copiedQuiz.NewQuizId,
                    newQuizTitle = copiedQuiz.NewQuizTitle,
                    originalQuizId = copiedQuiz.OriginalQuizId,
                    originalQuizTitle = copiedQuiz.OriginalQuizTitle
                },
                note = $"Đã tạo bản sao từ quiz '{expertQuiz.Title}' của Expert. Bạn có thể chỉnh sửa bản sao này."
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
        catch (Exception ex)
        {
            return BadRequest(new { message = "Đã xảy ra lỗi: " + ex.Message });
        }
    }

    #endregion

    /// <summary>Danh sách expert (read-only; gán expert vào lớp do Admin thực hiện).</summary>
    [HttpGet("experts")]
    public async Task<ActionResult<IReadOnlyList<ExpertOptionDto>>> GetExperts()
    {
        var result = await _lecturerService.GetExpertsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Tạo bản copy của một Expert Quiz để Lecturer có thể tùy chỉnh.
    /// Quiz mới sẽ không có CreatedByExpertId và có thể edit câu hỏi.
    /// </summary>
    /// <param name="expertQuizId">ID của expert quiz cần copy</param>
    /// <param name="title">Tiêu đề mới cho quiz (optional)</param>
    [HttpPost("expert-quizzes/{expertQuizId:guid}/copy")]
    public async Task<IActionResult> CopyExpertQuiz(Guid expertQuizId, [FromBody] CopyExpertQuizRequestDto? request = null)
    {
        try
        {
            var lecturerId = GetLecturerId();
            if (lecturerId == null)
                return Unauthorized(new { message = "Token không chứa user id hợp lệ." });

            var result = await _quizService.CopyExpertQuizForLecturerAsync(expertQuizId, lecturerId.Value, request?.Title);

            return Ok(new
            {
                message = "Đã tạo bản copy quiz thành công. Bạn có thể chỉnh sửa câu hỏi.",
                result
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã xảy ra lỗi: " + ex.Message });
        }
    }

}
