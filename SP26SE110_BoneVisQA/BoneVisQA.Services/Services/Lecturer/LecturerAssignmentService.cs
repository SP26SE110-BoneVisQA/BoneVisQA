using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerAssignmentService : ILecturerAssignmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<LecturerAssignmentService> _logger;

    public LecturerAssignmentService(IUnitOfWork unitOfWork, IEmailService emailService, ILogger<LecturerAssignmentService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClassCaseAssignmentDto>> AssignCasesAsync(Guid lecturerId, Guid classId, AssignCasesRequestDto request)
    {
        var academicClass = await EnsureLecturerOwnsClassAsync(lecturerId, classId);

        var caseIds = request.CaseIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (caseIds.Count == 0)
            throw new InvalidOperationException("caseIds phải chứa ít nhất một phần tử hợp lệ.");

        var medicalCases = await _unitOfWork.Context.MedicalCases
            .Where(c => caseIds.Contains(c.Id))
            .ToListAsync();

        if (medicalCases.Count != caseIds.Count)
            throw new KeyNotFoundException("Một hoặc nhiều case không tồn tại.");

        var existingAssignments = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId && caseIds.Contains(cc.CaseId))
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var caseId in caseIds)
        {
            var existing = existingAssignments.FirstOrDefault(x => x.CaseId == caseId);
            if (existing == null)
            {
                existing = new ClassCase
                {
                    ClassId = classId,
                    CaseId = caseId,
                    AssignedAt = now
                };
                await _unitOfWork.ClassCaseRepository.AddAsync(existing);
            }

            existing.DueDate = request.DueDate.HasValue
                ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc)
                : null;
            existing.IsMandatory = request.IsMandatory;
        }

        await _unitOfWork.SaveAsync();

        // Gửi email thông báo cho sinh viên trong lớp
        await NotifyStudentsAsync(academicClass, "Case Assignment", "Medical Case", request.DueDate);

        return medicalCases
            .OrderBy(c => c.Title)
            .Select(c =>
            {
                var assignment = existingAssignments.FirstOrDefault(x => x.CaseId == c.Id);
                return new ClassCaseAssignmentDto
                {
                    ClassId = academicClass.Id,
                    CaseId = c.Id,
                    CaseTitle = c.Title,
                    AssignedAt = assignment?.AssignedAt ?? now,
                    DueDate = request.DueDate,
                    IsMandatory = request.IsMandatory
                };
            })
            .ToList();
    }

    public async Task<ClassQuizSessionDto> AssignQuizSessionAsync(Guid lecturerId, Guid classId, AssignQuizSessionRequestDto request)
    {
        var academicClass = await EnsureLecturerOwnsClassAsync(lecturerId, classId);

        if (request.QuizId == Guid.Empty)
            throw new InvalidOperationException("quizId là bắt buộc.");

        if (request.OpenTime.HasValue && request.CloseTime.HasValue && request.OpenTime > request.CloseTime)
            throw new InvalidOperationException("openTime phải nhỏ hơn hoặc bằng closeTime.");

        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(request.QuizId)
            ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

        var session = await _unitOfWork.Context.ClassQuizSessions
            .FirstOrDefaultAsync(x => x.ClassId == classId && x.QuizId == request.QuizId);

        if (session == null)
        {
            session = new ClassQuizSession
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                QuizId = request.QuizId,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.ClassQuizSessionRepository.AddAsync(session);
        }

        session.OpenTime = request.OpenTime.HasValue
            ? DateTime.SpecifyKind(request.OpenTime.Value, DateTimeKind.Utc)
            : null;
        session.CloseTime = request.CloseTime.HasValue
            ? DateTime.SpecifyKind(request.CloseTime.Value, DateTimeKind.Utc)
            : null;
        session.TimeLimitMinutes = request.TimeLimitMinutes;
        session.PassingScore = request.PassingScore;

        await _unitOfWork.SaveAsync();

        // Gửi email thông báo cho sinh viên trong lớp
        await NotifyStudentsAsync(academicClass, quiz.Title, "Quiz", request.CloseTime);

        return new ClassQuizSessionDto
        {
            Id = session.Id,
            ClassId = session.ClassId,
            QuizId = session.QuizId,
            QuizTitle = quiz.Title,
            OpenTime = session.OpenTime,
            CloseTime = session.CloseTime,
            TimeLimitMinutes = session.TimeLimitMinutes,
            PassingScore = session.PassingScore,
            CreatedAt = session.CreatedAt
        };
    }

    private async Task NotifyStudentsAsync(AcademicClass academicClass, string assignmentTitle, string assignmentType, DateTime? dueDate)
    {
        try
        {
            var students = await _unitOfWork.Context.ClassEnrollments
                .AsNoTracking()
                .Where(e => e.ClassId == academicClass.Id)
                .Include(e => e.Student)
                .ToListAsync();

            var dueDateDisplay = dueDate.HasValue
                ? dueDate.Value.ToString("dd/MM/yyyy HH:mm")
                : null;

            foreach (var enrollment in students)
            {
                if (string.IsNullOrWhiteSpace(enrollment.Student.Email))
                    continue;

                var sent = await _emailService.SendAssignmentEmailAsync(
                    enrollment.Student.Email,
                    enrollment.Student.FullName ?? "Sinh viên",
                    academicClass.ClassName,
                    assignmentTitle,
                    assignmentType,
                    dueDate,
                    dueDateDisplay);

                if (!sent)
                    _logger.LogWarning("[NotifyStudentsAsync] Email failed to send to {Email}", enrollment.Student.Email);
            }

            _logger.LogInformation("[NotifyStudentsAsync] Sent {Count} assignment notification emails for class {ClassName}",
                students.Count, academicClass.ClassName);
        }
        catch (Exception ex)
        {
            // Không throw — email thất bại không nên roll back assignment đã lưu
            _logger.LogError(ex, "[NotifyStudentsAsync] Error sending assignment emails for class {ClassId}", academicClass.Id);
        }
    }

    private async Task<AcademicClass> EnsureLecturerOwnsClassAsync(Guid lecturerId, Guid classId)
    {
        return await _unitOfWork.Context.AcademicClasses
            .FirstOrDefaultAsync(c => c.Id == classId && c.LecturerId == lecturerId)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp học thuộc quyền giảng viên.");
    }

    // ── Quiz Review Methods ───────────────────────────────────────────────────────

    /// <summary>Lấy danh sách tất cả bài quiz attempts của sinh viên trong một class + quiz cụ thể.</summary>
    public async Task<IReadOnlyList<StudentQuizAttemptDto>> GetClassQuizAttemptsAsync(
        Guid lecturerId, Guid classId, Guid quizId)
    {
        await EnsureQuizSessionExistsForClassAsync(lecturerId, classId, quizId);

        var attempts = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.QuizId == quizId)
            .Include(a => a.Student)
            .Include(a => a.StudentQuizAnswers)
            .OrderBy(a => a.Student.FullName)
            .ThenBy(a => a.StartedAt)
            .ToListAsync();

        var questionCounts = await _unitOfWork.Context.QuizQuestions
            .Where(q => q.QuizId == quizId)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count() })
            .FirstOrDefaultAsync();

        var totalQuestions = questionCounts?.Count ?? 0;

        return attempts.Select(a => new StudentQuizAttemptDto
        {
            AttemptId = a.Id,
            StudentId = a.StudentId,
            StudentName = a.Student.FullName ?? "Sinh viên",
            StudentEmail = a.Student.Email ?? "",
            Score = a.Score,
            StartedAt = a.StartedAt,
            CompletedAt = a.CompletedAt,
            TotalQuestions = totalQuestions,
            CorrectCount = a.StudentQuizAnswers.Count(sa => sa.IsCorrect == true),
            IsGraded = a.Score.HasValue
        }).ToList();
    }

    /// <summary>Lấy chi tiết 1 bài quiz: câu hỏi + câu trả lời sinh viên.</summary>
    public async Task<QuizAttemptDetailDto> GetQuizAttemptDetailAsync(
        Guid lecturerId, Guid classId, Guid quizId, Guid attemptId)
    {
        await EnsureQuizSessionExistsForClassAsync(lecturerId, classId, quizId);

        var attempt = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.Id == attemptId && a.QuizId == quizId)
            .Include(a => a.Student)
            .Include(a => a.StudentQuizAnswers).ThenInclude(sa => sa.Question)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy bài làm của sinh viên.");

        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId)
            ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

        var session = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ClassId == classId && s.QuizId == quizId);

        return new QuizAttemptDetailDto
        {
            AttemptId = attempt.Id,
            QuizId = attempt.QuizId,
            QuizTitle = quiz.Title,
            StudentId = attempt.StudentId,
            StudentName = attempt.Student.FullName ?? "Sinh viên",
            Score = attempt.Score,
            StartedAt = attempt.StartedAt,
            CompletedAt = attempt.CompletedAt,
            PassingScore = session?.PassingScore,
            Questions = attempt.StudentQuizAnswers
                .OrderBy(sa => sa.Question.QuestionText)
                .Select(sa => new QuestionWithAnswerDto
                {
                    QuestionId = sa.QuestionId,
                    QuestionText = sa.Question.QuestionText,
                    Type = sa.Question.Type,
                    OptionA = sa.Question.OptionA,
                    OptionB = sa.Question.OptionB,
                    OptionC = sa.Question.OptionC,
                    OptionD = sa.Question.OptionD,
                    CorrectAnswer = sa.Question.CorrectAnswer,
                    StudentAnswer = sa.StudentAnswer,
                    IsCorrect = sa.IsCorrect,
                    AnswerId = sa.Id
                }).ToList()
        };
    }

    /// <summary>Lecturer chỉnh sửa điểm / câu trả lời của 1 quiz attempt.</summary>
    public async Task<QuizAttemptDetailDto> UpdateQuizAttemptAsync(
        Guid lecturerId, Guid classId, Guid quizId, Guid attemptId, UpdateQuizAttemptRequestDto request)
    {
        await EnsureQuizSessionExistsForClassAsync(lecturerId, classId, quizId);

        var attempt = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.Id == attemptId && a.QuizId == quizId)
            .Include(a => a.Student)
            .Include(a => a.StudentQuizAnswers).ThenInclude(sa => sa.Question)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy bài làm của sinh viên.");

        // Cập nhật điểm nếu có
        if (request.Score.HasValue)
            attempt.Score = request.Score.Value;

        // Cập nhật câu trả lời nếu có
        if (request.Answers.Count > 0)
        {
            var answerMap = attempt.StudentQuizAnswers.ToDictionary(a => a.Id);
            foreach (var update in request.Answers)
            {
                if (answerMap.TryGetValue(update.AnswerId, out var answer))
                {
                    if (update.StudentAnswer != null)
                        answer.StudentAnswer = update.StudentAnswer;
                    if (update.IsCorrect.HasValue)
                        answer.IsCorrect = update.IsCorrect.Value;
                }
            }
        }

        await _unitOfWork.SaveAsync();

        return await GetQuizAttemptDetailAsync(lecturerId, classId, quizId, attemptId);
    }

    private async Task EnsureQuizSessionExistsForClassAsync(Guid lecturerId, Guid classId, Guid quizId)
    {
        await EnsureLecturerOwnsClassAsync(lecturerId, classId);

        var session = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ClassId == classId && s.QuizId == quizId);

        if (session == null)
            throw new KeyNotFoundException("Quiz này chưa được gắn cho lớp học.");
    }
}
