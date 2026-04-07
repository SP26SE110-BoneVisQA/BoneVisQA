using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Models.Quiz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BoneVisQA.API.Controllers.Lecturer;

[ApiController]
[Route("api/lecturer")]
[Route("api/Lecturers")] // alias: FE cũ / cache bundle có thể vẫn gọi /api/Lecturers/...
[Authorize(Roles = "Lecturer")]
public class LecturersController : ControllerBase
{
    private readonly ILecturerService _lecturerService;
    private readonly IAIQuizService _aiQuizService;

    public LecturersController(ILecturerService lecturerService, IAIQuizService aiQuizService)
    {
        _lecturerService = lecturerService;
        _aiQuizService = aiQuizService;
    }

    private static Guid? TryGetJwtUserId(ClaimsPrincipal user)
    {
        var s = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(s, out var id) && id != Guid.Empty ? id : null;
    }

    #region Class Management

    [HttpPost("classes")]
    public async Task<ActionResult<ClassDto>> CreateClass([FromBody] CreateClassRequestDto request)
    {
        var result = await _lecturerService.CreateClassAsync(request);
        return Ok(result);
    }

    [HttpGet("classes/{classId:guid}")]
    public async Task<ActionResult<ClassDto>> GetClassById(Guid classId)
    {
        var result = await _lecturerService.GetClassByIdAsync(classId);
        if (result == null)
            return NotFound(new { message = "Lớp không tồn tại." });
        return Ok(result);
    }

    [HttpPut("classes/{classId:guid}")]
    public async Task<ActionResult<ClassDto>> UpdateClass(Guid classId, [FromBody] UpdateClassRequestDto request)
    {
        try
        {
            var result = await _lecturerService.UpdateClassAsync(classId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("classes/{classId:guid}")]
    public async Task<IActionResult> DeleteClass(Guid classId)
    {
        var deleted = await _lecturerService.DeleteClassAsync(classId);
        if (!deleted)
            return NotFound(new { message = "Lớp không tồn tại." });
        return NoContent();
    }

    [HttpGet("classes")]
    public async Task<ActionResult<IReadOnlyList<ClassDto>>> GetClasses([FromQuery] Guid lecturerId)
    {
        var result = await _lecturerService.GetClassesForLecturerAsync(lecturerId);
        return Ok(result);
    }

    [HttpPost("classes/{classId:guid}/enroll")]
    public async Task<IActionResult> EnrollStudent(Guid classId, [FromBody] EnrollStudentRequestDto request)
    {
        var created = await _lecturerService.EnrollStudentAsync(classId, request.StudentId);
        if (!created)
            return Conflict(new { message = "Student đã có trong lớp này." });
        return NoContent();
    }

    [HttpPost("classes/{classId:guid}/enrollmany")]
    public async Task<ActionResult<IReadOnlyList<StudentEnrollmentDto>>> EnrollManyStudents(Guid classId, [FromBody] EnrollStudentsRequestDto request)
    {
        var result = await _lecturerService.EnrollStudentsAsync(classId, request);
        return Ok(result);
    }

    [HttpDelete("classes/{classId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> RemoveStudent(Guid classId, Guid studentId)
    {
        var removed = await _lecturerService.RemoveStudentAsync(classId, studentId);
        if (!removed)
            return NotFound(new { message = "Student không tồn tại trong lớp này." });
        return NoContent();
    }

    [HttpGet("classes/{classId:guid}/students")]
    public async Task<ActionResult<IReadOnlyList<StudentEnrollmentDto>>> GetStudentsInClass(Guid classId)
    {
        var result = await _lecturerService.GetStudentsInClassAsync(classId);
        return Ok(result);
    }

    [HttpGet("classes/{classId:guid}/students/available")]
    public async Task<ActionResult<IReadOnlyList<StudentEnrollmentDto>>> GetAvailableStudents(Guid classId)
    {
        var result = await _lecturerService.GetAvailableStudentsAsync(classId);
        return Ok(result);
    }

    [HttpPost("classes/{classId:guid}/import-students")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportStudentsSummaryDto>> ImportStudentsFromExcel(Guid classId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File không được để trống." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx" && extension != ".xls")
            return BadRequest(new { message = "Chỉ chấp nhận file .xlsx hoặc .xls." });

        await using var stream = file.OpenReadStream();
        var result = await _lecturerService.ImportStudentsFromExcelAsync(classId, stream, file.FileName);
        return Ok(result);
    }

    [HttpPost("classes/{classId:guid}/announcements")]
    public async Task<ActionResult<AnnouncementDto>> CreateAnnouncement(Guid classId, [FromBody] CreateAnnouncementRequestDto request)
    {
        var result = await _lecturerService.CreateAnnouncementAsync(classId, request);
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
    public async Task<ActionResult<List<ClassAssignmentDto>>> GetAllAssignmentsForLecturer([FromQuery] Guid lecturerId)
    {
        if (lecturerId == Guid.Empty)
            return BadRequest(new { message = "lecturerId là bắt buộc." });
        var result = await _lecturerService.GetAllAssignmentsForLecturerAsync(lecturerId);
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
            return BadRequest(new { message = "Mã lớp hoặc thông báo không hợp lệ." });
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
            return BadRequest(new { message = "Mã lớp hoặc thông báo không hợp lệ." });
        var deleted = await _lecturerService.DeleteAnnouncementAsync(cId, aId);
        if (!deleted)
            return NotFound(new { message = "Thông báo không tồn tại." });
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
    public async Task<ActionResult<IReadOnlyList<ClassQuizDto>>> GetQuizzesForLecturer([FromQuery] Guid lecturerId)
    {
        if (lecturerId == Guid.Empty)
            return BadRequest(new { message = "lecturerId là bắt buộc." });
        var result = await _lecturerService.GetQuizzesByLecturerAsync(lecturerId);
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
            return NotFound(new { message = "Không tìm thấy câu hỏi nào." });
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

    [HttpPut("quizzes/questions/{questionId:guid}")]
    public async Task<IActionResult> UpdateQuizQuestion(Guid questionId, [FromBody] UpdateQuizsQuestionRequestDto request)
    {
        if (request == null)
            return BadRequest("Invalid request data.");

        var updated = await _lecturerService.UpdateQuizQuestionAsync(questionId, request);
        return Ok(new { message = "Cập nhật câu hỏi thành công." });
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
        var deleted = await _lecturerService.DeleteQuizAsync(quizId);
        if (!deleted)
            return NotFound(new { message = "Quiz không tồn tại." });
        return NoContent();
    }

    [HttpPost("classes/{classId:guid}/quizzes/{quizId:guid}")]
    public async Task<ActionResult<ClassQuizDto>> AssignQuizToClass(Guid classId, Guid quizId)
    {
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
            return BadRequest(new { message = "Cần chọn ít nhất 1 case." });

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
                message = $"Đã tạo quiz với {questionsResult.Questions.Count} câu hỏi"
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

    [HttpPost("classes/{classId:guid}/cases")]
    public async Task<ActionResult<IReadOnlyList<CaseDto>>> AssignCasesToClass(Guid classId, [FromBody] AssignCasesToClassRequestDto request)
    {
        var result = await _lecturerService.AssignCasesToClassAsync(classId, request);
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
}
