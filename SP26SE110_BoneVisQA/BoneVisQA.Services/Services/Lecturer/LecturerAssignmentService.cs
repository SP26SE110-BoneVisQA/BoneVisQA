using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerAssignmentService : ILecturerAssignmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LecturerAssignmentService> _logger;

    public LecturerAssignmentService(
        IUnitOfWork unitOfWork,
        IServiceScopeFactory scopeFactory,
        ILogger<LecturerAssignmentService> logger)
    {
        _unitOfWork = unitOfWork;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClassCaseAssignmentDto>> AssignCasesAsync(Guid lecturerId, Guid classId, AssignCasesRequestDto request)
    {
        var t0 = DateTime.UtcNow;
        _logger.LogInformation("[AssignCases] START - classId={ClassId}, caseCount={CaseCount}", classId, request.CaseIds.Count);

        var academicClass = await EnsureLecturerOwnsClassAsync(lecturerId, classId);
        var elapsed1 = (DateTime.UtcNow - t0).TotalSeconds;
        _logger.LogInformation("[AssignCases] Step1 EnsureOwnsClass done in {Elapsed}s", elapsed1);

        var caseIds = request.CaseIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (caseIds.Count == 0)
            throw new InvalidOperationException("caseIds phải chứa ít nhất một phần tử hợp lệ.");

        var medicalCases = await _unitOfWork.Context.MedicalCases
            .Where(c => caseIds.Contains(c.Id))
            .ToListAsync();
        var elapsed2 = (DateTime.UtcNow - t0).TotalSeconds;
        _logger.LogInformation("[AssignCases] Step2 LoadMedicalCases done in {Elapsed}s, found={Count}", elapsed2, medicalCases.Count);

        if (medicalCases.Count != caseIds.Count)
            throw new KeyNotFoundException("Một hoặc nhiều case không tồn tại.");

        var existingAssignments = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId && caseIds.Contains(cc.CaseId))
            .ToListAsync();
        var elapsed3 = (DateTime.UtcNow - t0).TotalSeconds;
        _logger.LogInformation("[AssignCases] Step3 LoadExisting done in {Elapsed}s, existing={Count}", elapsed3, existingAssignments.Count);

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
        var elapsed4 = (DateTime.UtcNow - t0).TotalSeconds;
        _logger.LogInformation("[AssignCases] Step4 SaveAsync done in {Elapsed}s", elapsed4);

        // Phải await phần đọc DB (DbContext scoped); SMTP vẫn chạy nền bên trong QueueAssignmentEmailsAsync
        await QueueAssignmentEmailsAsync(
            academicClass.Id,
            academicClass.ClassName,
            "Case Assignment",
            "Medical Case",
            request.DueDate);

        var elapsed5 = (DateTime.UtcNow - t0).TotalSeconds;
        _logger.LogInformation("[AssignCases] Step5 QueueEmails fired in {Elapsed}s", elapsed5);

        var result = medicalCases
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

        _logger.LogInformation("[AssignCases] DONE total={Elapsed}s", (DateTime.UtcNow - t0).TotalSeconds);
        return result;
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
        session.ShuffleQuestions = request.ShuffleQuestions;
        session.AllowRetake = request.AllowRetake;

        await _unitOfWork.SaveAsync();

        await QueueAssignmentEmailsAsync(
            academicClass.Id,
            academicClass.ClassName,
            quiz.Title,
            "Quiz",
            request.CloseTime);

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
            CreatedAt = session.CreatedAt,
            ShuffleQuestions = session.ShuffleQuestions,
            AllowRetake = session.AllowRetake,
            RetakeResetAt = session.RetakeResetAt
        };
    }

    /// <summary>
    /// Bật retake cho một attempt cụ thể — reset trạng thái để sinh viên làm lại.
    /// </summary>
    public async Task AllowRetakeForAttemptAsync(Guid lecturerId, Guid attemptId)
    {
        var attempt = await _unitOfWork.Context.QuizAttempts
            .Include(a => a.Quiz)
                .ThenInclude(q => q!.ClassQuizSessions)
                    .ThenInclude(cqs => cqs.Class)
            .FirstOrDefaultAsync(a => a.Id == attemptId)
            ?? throw new KeyNotFoundException("Không tìm thấy lần làm quiz.");

        var classSession = attempt.Quiz?.ClassQuizSessions?.FirstOrDefault();
        if (classSession == null)
            throw new InvalidOperationException("Quiz này không được gán qua lớp học.");

        // Kiểm tra lecturer sở hữu lớp
        var academicClass = classSession.Class;
        if (academicClass == null || academicClass.LecturerId != lecturerId)
            throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thao tác này.");

        if (!attempt.CompletedAt.HasValue)
            throw new InvalidOperationException("Sinh viên chưa nộp bài. Không cần bật retake.");

        // Xóa đáp án cũ
        var oldAnswers = await _unitOfWork.Context.StudentQuizAnswers
            .Where(a => a.AttemptId == attempt.Id)
            .ToListAsync();
        _unitOfWork.Context.StudentQuizAnswers.RemoveRange(oldAnswers);

        // Reset attempt
        attempt.CompletedAt = null;
        attempt.Score = null;
        attempt.StartedAt = DateTime.UtcNow;

        // Đánh dấu retake đã được bật
        classSession.RetakeResetAt = DateTime.UtcNow;

        await _unitOfWork.QuizAttemptRepository.UpdateAsync(attempt);
        await _unitOfWork.SaveAsync();
    }

    /// <summary>
    /// Bật retake cho toàn bộ sinh viên trong một lớp đã nộp quiz này.
    /// </summary>
    public async Task AllowRetakeAllAsync(Guid lecturerId, Guid classId, Guid quizId)
    {
        await EnsureLecturerOwnsClassAsync(lecturerId, classId);

        var session = await _unitOfWork.Context.ClassQuizSessions
            .FirstOrDefaultAsync(s => s.ClassId == classId && s.QuizId == quizId)
            ?? throw new KeyNotFoundException("Không tìm thấy phân công quiz cho lớp này.");

        var completedAttempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.QuizId == quizId && a.CompletedAt.HasValue)
            .Include(a => a.StudentQuizAnswers)
            .ToListAsync();

        foreach (var attempt in completedAttempts)
        {
            _unitOfWork.Context.StudentQuizAnswers.RemoveRange(attempt.StudentQuizAnswers);
            attempt.CompletedAt = null;
            attempt.Score = null;
            attempt.StartedAt = DateTime.UtcNow;
        }

        session.RetakeResetAt = DateTime.UtcNow;
        await _unitOfWork.SaveAsync();
    }

    /// <summary>
    /// Đọc danh sách email trong request (DbContext còn sống), rồi gửi SMTP ở background để API trả 200 ngay.
    /// Cấu hình SMTP là <c>Email:Username</c>/<c>Email:Password</c> (Gmail App Password, v.v.) — không liên quan Google OAuth Console.
    /// </summary>
    private async Task QueueAssignmentEmailsAsync(
        Guid classId,
        string className,
        string assignmentTitle,
        string assignmentType,
        DateTime? dueDate)
    {
        _logger.LogInformation("[AssignmentEmail] Queue started for class {ClassId}", classId);

        List<(string Email, string Name)> items;
        try
        {
            var rows = await _unitOfWork.Context.ClassEnrollments
                .AsNoTracking()
                .Where(e => e.ClassId == classId)
                .Select(e => new { e.Student!.Email, e.Student.FullName })
                .ToListAsync();

            items = rows
                .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                .Select(x => (x.Email!.Trim(), string.IsNullOrWhiteSpace(x.FullName) ? "Sinh viên" : x.FullName!.Trim()))
                .ToList();

            _logger.LogInformation("[AssignmentEmail] Loaded {Count} recipients for class {ClassId} — firing background send", items.Count, classId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AssignmentEmail] Failed to load recipients for class {ClassId}", classId);
            return;
        }

        if (items.Count == 0)
        {
            _logger.LogInformation("[AssignmentEmail] No student email for class {ClassId}", classId);
            return;
        }

        var dueDateDisplay = dueDate.HasValue
            ? dueDate.Value.ToString("dd/MM/yyyy HH:mm")
            : null;

        var nameCopy = className;
        var titleCopy = assignmentTitle;
        var typeCopy = assignmentType;
        var dueCopy = dueDate;
        var dueDisplayCopy = dueDateDisplay;
        var log = _logger;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                foreach (var (email, studentName) in items)
                {
                    try
                    {
                        var sent = await emailService.SendAssignmentEmailAsync(
                            email,
                            studentName,
                            nameCopy,
                            titleCopy,
                            typeCopy,
                            dueCopy,
                            dueDisplayCopy);
                        if (!sent)
                            log.LogWarning("[AssignmentEmail] SMTP returned false for {Email}", email);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "[AssignmentEmail] Error sending to {Email}", email);
                    }
                }

                log.LogInformation(
                    "[AssignmentEmail] Background send finished for class {ClassName}, {Count} recipients",
                    nameCopy,
                    items.Count);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "[AssignmentEmail] Background task failed for class {ClassId}", classId);
            }
        });
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
                .ThenInclude(sa => sa.Question)
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
            CorrectCount = a.StudentQuizAnswers.Count(sa =>
                sa.Question != null
                && QuizAnswerTextMatches(sa.Question.CorrectAnswer, sa.StudentAnswer)),
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
                    IsCorrect = sa.Question != null && QuizAnswerTextMatches(sa.Question.CorrectAnswer, sa.StudentAnswer),
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

    private static bool QuizAnswerTextMatches(string? correctAnswer, string? studentAnswer)
    {
        return string.Equals(
            studentAnswer?.Trim(),
            correctAnswer?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}
