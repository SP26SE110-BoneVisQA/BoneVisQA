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
    private readonly INotificationService _notificationService;

    private static DateTime? ToUtc(DateTime? dt)
    {
        if (!dt.HasValue) return null;
        return dt.Value.Kind switch
        {
            DateTimeKind.Utc => dt.Value,
            DateTimeKind.Local => dt.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt.Value, DateTimeKind.Local).ToUniversalTime()
        };
    }

    public LecturerAssignmentService(
        IUnitOfWork unitOfWork,
        IServiceScopeFactory scopeFactory,
        ILogger<LecturerAssignmentService> logger,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Passing score is always on a 100-point scale (0-100). Identity function.
    /// </summary>
    private static int? NormalizePassingScore(int? passingScore, bool isAiGenerated)
    {
        return passingScore;
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
            throw new InvalidOperationException("caseIds must contain at least one valid element.");

        var medicalCases = await _unitOfWork.Context.MedicalCases
            .Where(c => caseIds.Contains(c.Id))
            .ToListAsync();
        var elapsed2 = (DateTime.UtcNow - t0).TotalSeconds;
        _logger.LogInformation("[AssignCases] Step2 LoadMedicalCases done in {Elapsed}s, found={Count}", elapsed2, medicalCases.Count);

        if (medicalCases.Count != caseIds.Count)
            throw new KeyNotFoundException("One or more cases not found.");

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

            existing.DueDate = ToUtc(request.DueDate);
            existing.IsMandatory = request.IsMandatory;
        }

        await _unitOfWork.SaveAsync();
        var elapsed4 = (DateTime.UtcNow - t0).TotalSeconds;
        _logger.LogInformation("[AssignCases] Step4 SaveAsync done in {Elapsed}s", elapsed4);

        // Must await the DB read part (DbContext scoped); SMTP still runs in background inside QueueAssignmentEmailsAsync
        // Gửi email riêng cho từng case để có đủ thông tin (title + description)
        foreach (var medicalCase in medicalCases)
        {
            await QueueAssignmentEmailsAsync(
                academicClass.Id,
                academicClass.ClassName,
                medicalCase.Title,
                "Medical Case",
                request.DueDate,
                medicalCase.Description);
        }

        // Send SignalR notification to students
        await QueueAssignmentNotificationsAsync(
            academicClass.Id,
            academicClass.ClassName,
            "Case Assignment",
            "Clinical Case",
            "/student/cases");

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
            throw new InvalidOperationException("quizId is required.");

        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(request.QuizId)
            ?? throw new KeyNotFoundException("Quiz not found.");

        var warnings = new List<string>();
        DateTime? effectiveOpenTime = request.OpenTime;
        DateTime? effectiveCloseTime = request.CloseTime;

        // If lecturer wants to use Expert's timing
        if (request.UseExpertTime)
        {
            effectiveOpenTime = quiz.OpenTime;
            effectiveCloseTime = quiz.CloseTime;
            warnings.Add("Using Expert's open/close time.");
        }
        else
        {
            // Clamp open time: if earlier than Expert's, use Expert's time
            if (request.OpenTime.HasValue && quiz.OpenTime.HasValue && request.OpenTime.Value < quiz.OpenTime.Value)
            {
                warnings.Add($"Quiz open time adjusted from {request.OpenTime.Value:HH:mm} to {quiz.OpenTime.Value:HH:mm} (Expert's open time).");
                effectiveOpenTime = quiz.OpenTime;
            }

            // Clamp close time: if later than Expert's, use Expert's time
            if (request.CloseTime.HasValue && quiz.CloseTime.HasValue && request.CloseTime.Value > quiz.CloseTime.Value)
            {
                warnings.Add($"Quiz close time adjusted from {request.CloseTime.Value:HH:mm} to {quiz.CloseTime.Value:HH:mm} (Expert's close time).");
                effectiveCloseTime = quiz.CloseTime;
            }
        }

        // Validate openTime <= closeTime (after clamping)
        if (effectiveOpenTime.HasValue && effectiveCloseTime.HasValue && effectiveOpenTime > effectiveCloseTime)
            throw new InvalidOperationException("openTime must be less than or equal to closeTime.");

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

        // ============================================
        // TIME LIMIT & PASSING SCORE CLAMP LOGIC
        // ============================================
        // Expert đặt giá trị gốc trên Quiz. Lecturer có thể điều chỉnh nhưng bị giới hạn:
        // - TimeLimit: ±50% so với Expert (min 5, max 180)
        // - PassingScore: ±10% so với Expert (min 0, max 100)
        // Nếu Lecturer để trống → dùng giá trị Expert
        // ============================================

        // TIME LIMIT CLAMP
        int? effectiveTimeLimit = request.TimeLimitMinutes;
        if (quiz.TimeLimit.HasValue)
        {
            var expertTimeLimit = quiz.TimeLimit.Value;
            var minTimeLimit = Math.Max(5, (int)(expertTimeLimit * 0.5));
            var maxTimeLimit = Math.Min(180, (int)(expertTimeLimit * 1.5));

            if (request.TimeLimitMinutes.HasValue)
            {
                // Lecturer đã nhập giá trị → clamp vào khoảng cho phép
                if (request.TimeLimitMinutes.Value < minTimeLimit)
                {
                    effectiveTimeLimit = minTimeLimit;
                    warnings.Add($"Time limit adjusted to {minTimeLimit} min (minimum allowed for this quiz).");
                }
                else if (request.TimeLimitMinutes.Value > maxTimeLimit)
                {
                    effectiveTimeLimit = maxTimeLimit;
                    warnings.Add($"Time limit adjusted to {maxTimeLimit} min (maximum allowed for this quiz).");
                }
                else
                {
                    effectiveTimeLimit = request.TimeLimitMinutes.Value;
                }
            }
            else
            {
                // Lecturer không nhập → dùng giá trị Expert
                effectiveTimeLimit = expertTimeLimit;
            }
        }
        else
        {
            // Quiz không có TimeLimit từ Expert → dùng giá trị Lecturer (hoặc default 30)
            effectiveTimeLimit = request.TimeLimitMinutes ?? 30;
        }

        // PASSING SCORE CLAMP
        int? effectivePassingScore = request.PassingScore;
        if (quiz.PassingScore.HasValue)
        {
            var expertPassingScore = quiz.PassingScore.Value;
            var minPassingScore = Math.Max(0, expertPassingScore - 10);
            var maxPassingScore = Math.Min(100, expertPassingScore + 10);

            if (request.PassingScore.HasValue)
            {
                // Lecturer đã nhập giá trị → clamp vào khoảng cho phép
                if (request.PassingScore.Value < minPassingScore)
                {
                    effectivePassingScore = minPassingScore;
                    warnings.Add($"Passing score adjusted to {minPassingScore}% (minimum allowed for this quiz).");
                }
                else if (request.PassingScore.Value > maxPassingScore)
                {
                    effectivePassingScore = maxPassingScore;
                    warnings.Add($"Passing score adjusted to {maxPassingScore}% (maximum allowed for this quiz).");
                }
                else
                {
                    effectivePassingScore = request.PassingScore.Value;
                }
            }
            else
            {
                // Lecturer không nhập → dùng giá trị Expert
                effectivePassingScore = expertPassingScore;
            }
        }
        else
        {
            // Quiz không có PassingScore từ Expert → dùng giá trị Lecturer (hoặc default 70)
            effectivePassingScore = request.PassingScore ?? 70;
        }

        session.OpenTime = ToUtc(effectiveOpenTime);
        session.CloseTime = ToUtc(effectiveCloseTime);
        session.TimeLimitMinutes = effectiveTimeLimit;
        session.PassingScore = effectivePassingScore;
        session.ShuffleQuestions = request.ShuffleQuestions;
        session.AllowRetake = request.AllowRetake;
        session.AllowLate = request.AllowLate;
        session.ShowResultsAfterSubmission = request.ShowResultsAfterSubmission;

        await _unitOfWork.SaveAsync();

        await QueueAssignmentEmailsAsync(
            academicClass.Id,
            academicClass.ClassName,
            quiz.Title,
            "Quiz",
            request.CloseTime,
            quiz.Topic);  // ← thêm Topic làm description

        // Send SignalR notification to students
        await QueueAssignmentNotificationsAsync(
            academicClass.Id,
            academicClass.ClassName,
            quiz.Title,
            "Quiz",
            "/student/quizzes");

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
            AllowLate = session.AllowLate,
            ShowResultsAfterSubmission = session.ShowResultsAfterSubmission,
            RetakeResetAt = session.RetakeResetAt,
            Warning = warnings.Count > 0 ? string.Join(" ", warnings) : null
        };
    }

    /// <summary>
    /// Enable retake for a specific attempt — reset state to allow student to retake.
    /// </summary>
    public async Task AllowRetakeForAttemptAsync(Guid lecturerId, Guid attemptId)
    {
        var attempt = await _unitOfWork.Context.QuizAttempts
            .Include(a => a.Quiz)
                .ThenInclude(q => q!.ClassQuizSessions)
                    .ThenInclude(cqs => cqs.Class)
            .FirstOrDefaultAsync(a => a.Id == attemptId)
            ?? throw new KeyNotFoundException("Quiz attempt not found.");

        var classSession = attempt.Quiz?.ClassQuizSessions?.FirstOrDefault();
        if (classSession == null)
            throw new InvalidOperationException("This quiz is not assigned to a class.");

        // Check if lecturer owns the class
        var academicClass = classSession.Class;
        if (academicClass == null || academicClass.LecturerId != lecturerId)
            throw new UnauthorizedAccessException("You do not have permission to perform this action.");

        if (!attempt.CompletedAt.HasValue)
            throw new InvalidOperationException("Student has not submitted. No need to enable retake.");

        // Remove old answers
        var oldAnswers = await _unitOfWork.Context.StudentQuizAnswers
            .Where(a => a.AttemptId == attempt.Id)
            .ToListAsync();
        _unitOfWork.Context.StudentQuizAnswers.RemoveRange(oldAnswers);

        // Reset attempt
        attempt.CompletedAt = null;
        attempt.Score = null;
        attempt.StartedAt = DateTime.UtcNow;

        // Mark retake as enabled
        classSession.RetakeResetAt = DateTime.UtcNow;

        await _unitOfWork.QuizAttemptRepository.UpdateAsync(attempt);
        await _unitOfWork.SaveAsync();
    }

    /// <summary>
    /// Enable retake for all students in a class who have submitted this quiz.
    /// </summary>
    public async Task AllowRetakeAllAsync(Guid lecturerId, Guid classId, Guid quizId)
    {
        await EnsureLecturerOwnsClassAsync(lecturerId, classId);

        var session = await _unitOfWork.Context.ClassQuizSessions
            .FirstOrDefaultAsync(s => s.ClassId == classId && s.QuizId == quizId)
            ?? throw new KeyNotFoundException("Quiz assignment not found for this class.");

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
    /// Read email list from request (DbContext still alive), then send SMTP in background so API returns 200 immediately.
    /// SMTP configuration is <c>Email:Username</c>/<c>Email:Password</c> (Gmail App Password, etc.) — unrelated to Google OAuth Console.
    /// </summary>
    private async Task QueueAssignmentEmailsAsync(
        Guid classId,
        string className,
        string assignmentTitle,
        string assignmentType,
        DateTime? dueDate,
        string? description)  // ← thêm parameter
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
                .Select(x => (x.Email!.Trim(), string.IsNullOrWhiteSpace(x.FullName) ? "Student" : x.FullName!.Trim()))
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
        var descriptionCopy = description;  // ← thêm này
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
                            dueDisplayCopy,
                            descriptionCopy);  // ← truyền description
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

    private async Task QueueAssignmentUpdateEmailsAsync(
        Guid classId,
        string className,
        string assignmentTitle,
        string assignmentType,
        DateTime? dueDate)
    {
        _logger.LogInformation("[AssignmentUpdateEmail] Queue started for class {ClassId}", classId);

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
                .Select(x => (x.Email!.Trim(), string.IsNullOrWhiteSpace(x.FullName) ? "Student" : x.FullName!.Trim()))
                .ToList();

            _logger.LogInformation("[AssignmentUpdateEmail] Loaded {Count} recipients for class {ClassId}", items.Count, classId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AssignmentUpdateEmail] Failed to load recipients for class {ClassId}", classId);
            return;
        }

        if (items.Count == 0)
        {
            _logger.LogInformation("[AssignmentUpdateEmail] No student email for class {ClassId}", classId);
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
                        await emailService.SendAssignmentUpdateEmailAsync(
                            email,
                            studentName,
                            nameCopy,
                            titleCopy,
                            typeCopy,
                            dueCopy,
                            dueDisplayCopy);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "[AssignmentUpdateEmail] Error sending to {Email}", email);
                    }
                }

                log.LogInformation(
                    "[AssignmentUpdateEmail] Background send finished for class {ClassName}, {Count} recipients",
                    nameCopy,
                    items.Count);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "[AssignmentUpdateEmail] Background task failed for class {ClassId}", classId);
            }
        });
    }

    /// <summary>
    /// Send SignalR notification to all students in the class about newly assigned assignment.
    /// </summary>
    private async Task QueueAssignmentNotificationsAsync(
        Guid classId,
        string className,
        string assignmentTitle,
        string assignmentType,
        string targetUrl)
    {
        _logger.LogInformation("[AssignmentNotification] Queue started for class {ClassId}", classId);

        List<(Guid UserId, string Name)> items;
        try
        {
            var rows = await _unitOfWork.Context.ClassEnrollments
                .AsNoTracking()
                .Where(e => e.ClassId == classId)
                .Select(e => new { e.StudentId, e.Student!.FullName })
                .ToListAsync();

            items = rows
                .Where(x => x.StudentId != Guid.Empty)
                .Select(x => (x.StudentId, string.IsNullOrWhiteSpace(x.FullName) ? "Student" : x.FullName!.Trim()))
                .ToList();

            _logger.LogInformation("[AssignmentNotification] Loaded {Count} recipients for class {ClassId}", items.Count, classId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AssignmentNotification] Failed to load recipients for class {ClassId}", classId);
            return;
        }

        if (items.Count == 0)
        {
            _logger.LogInformation("[AssignmentNotification] No student for class {ClassId}", classId);
            return;
        }

        var nameCopy = className;
        var titleCopy = assignmentTitle;
        var typeCopy = assignmentType;
        var urlCopy = targetUrl;
        var log = _logger;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var notificationTitle = $"New Assignment: {titleCopy}";
                var notificationMessage = $"Lecturer just assigned new {typeCopy} to class {nameCopy}";

                foreach (var (userId, studentName) in items)
                {
                    try
                    {
                        await notificationService.SendNotificationToUserAsync(
                            userId,
                            notificationTitle,
                            notificationMessage,
                            "assignment",
                            urlCopy);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "[AssignmentNotification] Error sending to user {UserId}", userId);
                    }
                }

                log.LogInformation(
                    "[AssignmentNotification] Background send finished for class {ClassName}, {Count} recipients",
                    nameCopy,
                    items.Count);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "[AssignmentNotification] Background task failed for class {ClassId}", classId);
            }
        });
    }

    private async Task<AcademicClass> EnsureLecturerOwnsClassAsync(Guid lecturerId, Guid classId)
    {
        return await _unitOfWork.Context.AcademicClasses
            .FirstOrDefaultAsync(c => c.Id == classId && c.LecturerId == lecturerId)
            ?? throw new KeyNotFoundException("Class not found under lecturer's authority.");
    }

    // ── Quiz Review Methods ───────────────────────────────────────────────────────

    /// <summary>Get list of all quiz attempts for students in a class + specific quiz.</summary>
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
            StudentName = a.Student.FullName ?? "Student",
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

    /// <summary>Get details for 1 quiz: questions + student answers.</summary>
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
            ?? throw new KeyNotFoundException("Student's quiz attempt not found.");

        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId)
            ?? throw new KeyNotFoundException("Quiz not found.");

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
            PassingScore = NormalizePassingScore(session?.PassingScore, quiz.IsAiGenerated),
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

    /// <summary>Lecturer edits score / answers for a quiz attempt.</summary>
    public async Task<QuizAttemptDetailDto> UpdateQuizAttemptAsync(
        Guid lecturerId, Guid classId, Guid quizId, Guid attemptId, UpdateQuizAttemptRequestDto request)
    {
        await EnsureQuizSessionExistsForClassAsync(lecturerId, classId, quizId);

        var attempt = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.Id == attemptId && a.QuizId == quizId)
            .Include(a => a.Student)
            .Include(a => a.StudentQuizAnswers).ThenInclude(sa => sa.Question)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Student's quiz attempt not found.");

        // Update score if provided
        if (request.Score.HasValue)
            attempt.Score = request.Score.Value;

        // Update answers if provided
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
            throw new KeyNotFoundException("This quiz has not been assigned to a class.");
    }

    private static bool QuizAnswerTextMatches(string? correctAnswer, string? studentAnswer)
    {
        return string.Equals(
            studentAnswer?.Trim(),
            correctAnswer?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    // ── Assignment CRUD Methods ─────────────────────────────────────────────────

    /// <summary>Get details for an assignment (case or quiz) by ID.</summary>
    public async Task<AssignmentDetailDto> GetAssignmentByIdAsync(Guid assignmentId)
    {
        // Try to find in ClassCases (case assignment)
        var classCase = await _unitOfWork.Context.ClassCases
            .AsNoTracking()
            .Include(cc => cc.Class)
            .Include(cc => cc.Case)
            .FirstOrDefaultAsync(cc => cc.CaseId == assignmentId);

        if (classCase != null)
        {
            var totalStudents = await _unitOfWork.Context.ClassEnrollments
                .CountAsync(e => e.ClassId == classCase.ClassId);

            var submittedCount = await _unitOfWork.Context.CaseViewLogs
                .CountAsync(v => v.CaseId == classCase.CaseId && v.IsCompleted == true);

            return new AssignmentDetailDto
            {
                Id = classCase.CaseId,
                ClassId = classCase.ClassId,
                ClassName = classCase.Class?.ClassName ?? "",
                ClassCode = null,
                Type = "case",
                Title = classCase.Case?.Title ?? "Untitled Case",
                Description = classCase.Case?.Description,
                DueDate = classCase.DueDate,
                OpenDate = classCase.AssignedAt,
                IsMandatory = classCase.IsMandatory,
                AssignedAt = classCase.AssignedAt,
                TotalStudents = totalStudents,
                SubmittedCount = submittedCount,
                GradedCount = 0, // Case is not graded
                AllowLate = false,
                CreatedAt = classCase.AssignedAt
            };
        }

        // Try to find in ClassQuizSessions (quiz assignment)
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Class)
            .Include(cqs => cqs.Quiz)
            .FirstOrDefaultAsync(cqs => cqs.Id == assignmentId);

        if (quizSession == null)
            throw new KeyNotFoundException("Assignment not found.");

        var quizTotalStudents = await _unitOfWork.Context.ClassEnrollments
            .CountAsync(e => e.ClassId == quizSession.ClassId);

        var quizSubmittedCount = await _unitOfWork.Context.QuizAttempts
            .CountAsync(a => a.QuizId == quizSession.QuizId && a.CompletedAt.HasValue);

        var quizGradedCount = await _unitOfWork.Context.QuizAttempts
            .CountAsync(a => a.QuizId == quizSession.QuizId && a.Score.HasValue);

        var quizAvgScore = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.QuizId == quizSession.QuizId && a.Score.HasValue)
            .AverageAsync(a => (double?)a.Score) ?? null;

        return new AssignmentDetailDto
        {
            Id = quizSession.Id,
            ClassId = quizSession.ClassId,
            ClassName = quizSession.Class?.ClassName ?? "",
            ClassCode = null,
            Type = "quiz",
            Title = quizSession.Quiz?.Title ?? "Untitled Quiz",
            Description = null,
            Instructions = null,
            DueDate = quizSession.CloseTime,
            OpenDate = quizSession.OpenTime,
            IsMandatory = false,
            AssignedAt = quizSession.CreatedAt,
            TotalStudents = quizTotalStudents,
            SubmittedCount = quizSubmittedCount,
            GradedCount = quizGradedCount,
            MaxScore = 100,
            PassingScore = quizSession.PassingScore,
            TimeLimitMinutes = quizSession.TimeLimitMinutes,
            AllowLate = quizSession.AllowLate,
            AllowRetake = quizSession.AllowRetake,
            ShowResultsAfterSubmission = quizSession.ShowResultsAfterSubmission,
            AvgScore = quizAvgScore,
            CreatedAt = quizSession.CreatedAt
        };
    }

    /// <summary>Update assignment information.</summary>
    public async Task<AssignmentDetailDto> UpdateAssignmentAsync(Guid assignmentId, UpdateAssignmentRequestDto request)
    {
        // Try to update ClassCase
        var classCase = await _unitOfWork.Context.ClassCases
            .Include(cc => cc.Class)
            .Include(cc => cc.Case)
            .FirstOrDefaultAsync(cc => cc.CaseId == assignmentId);

        if (classCase != null)
        {
            if (request.DueDate.HasValue)
                classCase.DueDate = ToUtc(request.DueDate);
            if (request.IsMandatory.HasValue)
                classCase.IsMandatory = request.IsMandatory.Value;

            await _unitOfWork.SaveAsync();

            if (request.SendEmailUpdate)
            {
                await QueueAssignmentUpdateEmailsAsync(
                    classCase.ClassId,
                    classCase.Class.ClassName,
                    classCase.Case.Title ?? "Clinical Case Assignment",
                    "Clinical Case",
                    classCase.DueDate);
            }

            return await GetAssignmentByIdAsync(assignmentId);
        }

        // Try to update ClassQuizSession
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .Include(cqs => cqs.Class)
            .Include(cqs => cqs.Quiz)
            .FirstOrDefaultAsync(cqs => cqs.Id == assignmentId);

        if (quizSession == null)
            throw new KeyNotFoundException("Assignment not found.");

        var warnings = new List<string>();
        DateTime? effectiveOpenDate = request.OpenDate;
        DateTime? effectiveDueDate = request.DueDate;

        // If lecturer wants to use Expert's timing
        if (request.UseExpertTime)
        {
            effectiveOpenDate = quizSession.Quiz?.OpenTime;
            effectiveDueDate = quizSession.Quiz?.CloseTime;
            warnings.Add("Using Expert's open/close time.");
        }
        else
        {
            // Clamp thời gian mở: nếu sớm hơn Expert thì lấy thời gian Expert
            if (request.OpenDate.HasValue && quizSession.Quiz != null && quizSession.Quiz.OpenTime.HasValue && request.OpenDate.Value < quizSession.Quiz.OpenTime.Value)
            {
                warnings.Add($"Thời gian mở quiz đã được điều chỉnh từ {request.OpenDate.Value:HH:mm} lên {quizSession.Quiz.OpenTime.Value:HH:mm} (thời gian mở của Expert).");
                effectiveOpenDate = quizSession.Quiz.OpenTime;
            }

            // Clamp thời gian đóng: nếu muộn hơn Expert thì lấy thời gian Expert
            if (request.DueDate.HasValue && quizSession.Quiz != null && quizSession.Quiz.CloseTime.HasValue && request.DueDate.Value > quizSession.Quiz.CloseTime.Value)
            {
                warnings.Add($"Thời gian đóng quiz đã được điều chỉnh từ {request.DueDate.Value:HH:mm} xuống {quizSession.Quiz.CloseTime.Value:HH:mm} (thời gian đóng của Expert).");
                effectiveDueDate = quizSession.Quiz.CloseTime;
            }
        }

        if (effectiveDueDate.HasValue)
            quizSession.CloseTime = ToUtc(effectiveDueDate);
        if (effectiveOpenDate.HasValue)
            quizSession.OpenTime = ToUtc(effectiveOpenDate);

        // ============================================
        // TIME LIMIT & PASSING SCORE CLAMP LOGIC (same as AssignQuizSessionAsync)
        // ============================================
        if (request.PassingScore.HasValue || request.TimeLimitMinutes.HasValue)
        {
            // Get expert values from Quiz
            var expertTimeLimit = quizSession.Quiz?.TimeLimit;
            var expertPassingScore = quizSession.Quiz?.PassingScore;

            // CLAMP TIME LIMIT
            if (request.TimeLimitMinutes.HasValue)
            {
                int effectiveTimeLimit;
                if (expertTimeLimit.HasValue)
                {
                    var minTimeLimit = Math.Max(5, (int)(expertTimeLimit.Value * 0.5));
                    var maxTimeLimit = Math.Min(180, (int)(expertTimeLimit.Value * 1.5));

                    if (request.TimeLimitMinutes.Value < minTimeLimit)
                    {
                        effectiveTimeLimit = minTimeLimit;
                        warnings.Add($"Time limit adjusted to {minTimeLimit} min (minimum allowed for this quiz).");
                    }
                    else if (request.TimeLimitMinutes.Value > maxTimeLimit)
                    {
                        effectiveTimeLimit = maxTimeLimit;
                        warnings.Add($"Time limit adjusted to {maxTimeLimit} min (maximum allowed for this quiz).");
                    }
                    else
                    {
                        effectiveTimeLimit = request.TimeLimitMinutes.Value;
                    }
                }
                else
                {
                    // Quiz không có TimeLimit từ Expert → dùng giá trị Lecturer
                    effectiveTimeLimit = request.TimeLimitMinutes.Value;
                }
                quizSession.TimeLimitMinutes = effectiveTimeLimit;
            }

            // CLAMP PASSING SCORE
            if (request.PassingScore.HasValue)
            {
                int effectivePassingScore;
                if (expertPassingScore.HasValue)
                {
                    var minPassingScore = Math.Max(0, expertPassingScore.Value - 10);
                    var maxPassingScore = Math.Min(100, expertPassingScore.Value + 10);

                    if (request.PassingScore.Value < minPassingScore)
                    {
                        effectivePassingScore = minPassingScore;
                        warnings.Add($"Passing score adjusted to {minPassingScore}% (minimum allowed for this quiz).");
                    }
                    else if (request.PassingScore.Value > maxPassingScore)
                    {
                        effectivePassingScore = maxPassingScore;
                        warnings.Add($"Passing score adjusted to {maxPassingScore}% (maximum allowed for this quiz).");
                    }
                    else
                    {
                        effectivePassingScore = request.PassingScore.Value;
                    }
                }
                else
                {
                    // Quiz không có PassingScore từ Expert → dùng giá trị Lecturer
                    effectivePassingScore = request.PassingScore.Value;
                }
                quizSession.PassingScore = effectivePassingScore;
            }
        }

        if (request.AllowRetake.HasValue)
            quizSession.AllowRetake = request.AllowRetake.Value;
        if (request.AllowLate.HasValue)
            quizSession.AllowLate = request.AllowLate.Value;
        if (request.ShowResultsAfterSubmission.HasValue)
            quizSession.ShowResultsAfterSubmission = request.ShowResultsAfterSubmission.Value;

        await _unitOfWork.SaveAsync();

        if (request.SendEmailUpdate)
        {
            await QueueAssignmentUpdateEmailsAsync(
                quizSession.ClassId,
                quizSession.Class.ClassName,
                quizSession.Quiz?.Title ?? "Quiz",
                "Quiz",
                quizSession.CloseTime);
        }

        var detail = new AssignmentDetailDto
        {
            Id = quizSession.Id,
            ClassId = quizSession.ClassId,
            ClassName = quizSession.Class?.ClassName ?? "",
            ClassCode = null,
            Type = "quiz",
            Title = quizSession.Quiz?.Title ?? "Untitled Quiz",
            Description = null,
            Instructions = null,
            DueDate = quizSession.CloseTime,
            OpenDate = quizSession.OpenTime,
            IsMandatory = false,
            AssignedAt = quizSession.CreatedAt,
            TotalStudents = await _unitOfWork.Context.ClassEnrollments.CountAsync(e => e.ClassId == quizSession.ClassId),
            SubmittedCount = await _unitOfWork.Context.QuizAttempts.CountAsync(a => a.QuizId == quizSession.QuizId && a.CompletedAt.HasValue),
            GradedCount = await _unitOfWork.Context.QuizAttempts.CountAsync(a => a.QuizId == quizSession.QuizId && a.Score.HasValue),
            MaxScore = 100,
            PassingScore = quizSession.PassingScore,
            TimeLimitMinutes = quizSession.TimeLimitMinutes,
            AllowLate = quizSession.AllowLate,
            AllowRetake = quizSession.AllowRetake,
            ShowResultsAfterSubmission = quizSession.ShowResultsAfterSubmission,
            AvgScore = await _unitOfWork.Context.QuizAttempts.Where(a => a.QuizId == quizSession.QuizId && a.Score.HasValue).AverageAsync(a => (double?)a.Score) ?? null,
            CreatedAt = quizSession.CreatedAt,
            Warning = warnings.Count > 0 ? string.Join(" ", warnings) : null
        };

        return detail;
    }

    /// <summary>Delete an assignment.</summary>
    public async Task DeleteAssignmentAsync(Guid assignmentId)
    {
        // Try to delete ClassCase
        var classCase = await _unitOfWork.Context.ClassCases
            .FirstOrDefaultAsync(cc => cc.CaseId == assignmentId);

        if (classCase != null)
        {
            _unitOfWork.Context.ClassCases.Remove(classCase);
            await _unitOfWork.SaveAsync();
            return;
        }

        // Try to delete ClassQuizSession
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .FirstOrDefaultAsync(cqs => cqs.Id == assignmentId);

        if (quizSession == null)
            throw new KeyNotFoundException("Assignment not found.");

        _unitOfWork.Context.ClassQuizSessions.Remove(quizSession);
        await _unitOfWork.SaveAsync();
    }

    /// <summary>Get list of submissions for an assignment.</summary>
    public async Task<IReadOnlyList<AssignmentSubmissionDto>> GetAssignmentSubmissionsAsync(Guid assignmentId)
    {
        // Try to get submissions from ClassCases (case)
        var classCase = await _unitOfWork.Context.ClassCases
            .AsNoTracking()
            .Include(cc => cc.Class)
            .FirstOrDefaultAsync(cc => cc.CaseId == assignmentId);

        if (classCase != null)
        {
            var enrollments = await _unitOfWork.Context.ClassEnrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Where(e => e.ClassId == classCase.ClassId)
                .ToListAsync();

            var viewLogs = await _unitOfWork.Context.CaseViewLogs
                .AsNoTracking()
                .Where(v => v.CaseId == assignmentId)
                .ToDictionaryAsync(v => v.StudentId);

            return enrollments.Select(e => new AssignmentSubmissionDto
            {
                StudentId = e.StudentId,
                StudentName = e.Student?.FullName ?? "Unknown",
                StudentCode = e.Student?.SchoolCohort,
                SubmittedAt = viewLogs.TryGetValue(e.StudentId, out var log) ? log.ViewedAt : null,
                Score = null,
                Status = viewLogs.ContainsKey(e.StudentId) ? "graded" : "not-submitted"
            }).ToList();
        }

        // Lấy submissions từ ClassQuizSessions (quiz)
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Quiz)
            .FirstOrDefaultAsync(cqs => cqs.Id == assignmentId)
            ?? throw new KeyNotFoundException("Assignment not found.");

        var quizEnrollments = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Where(e => e.ClassId == quizSession.ClassId)
            .ToListAsync();

        var attempts = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.QuizId == quizSession.QuizId)
            .ToDictionaryAsync(a => a.StudentId);

        return quizEnrollments.Select(e => new AssignmentSubmissionDto
        {
            StudentId = e.StudentId,
            StudentName = e.Student?.FullName ?? "Unknown",
            StudentCode = e.Student?.SchoolCohort,
            SubmittedAt = attempts.TryGetValue(e.StudentId, out var attempt) ? attempt.CompletedAt : null,
            Score = attempts.TryGetValue(e.StudentId, out var attempt2) ? attempt2.Score : null,
            Status = attempts.TryGetValue(e.StudentId, out var attempt3)
                ? (attempt3.Score.HasValue ? "graded" : "pending")
                : "not-submitted"
        }).ToList();
    }

    /// <summary>Cập nhật điểm cho nhiều submissions.</summary>
    public async Task<IReadOnlyList<AssignmentSubmissionDto>> UpdateAssignmentSubmissionsAsync(
        Guid assignmentId, UpdateSubmissionsRequestDto request)
    {
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Quiz)
            .FirstOrDefaultAsync(cqs => cqs.Id == assignmentId)
            ?? throw new KeyNotFoundException("Assignment not found.");

        var studentIds = request.Submissions.Select(s => s.StudentId).ToList();
        var attempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.QuizId == quizSession.QuizId && studentIds.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId);

        foreach (var update in request.Submissions)
        {
            if (attempts.TryGetValue(update.StudentId, out var attempt))
            {
                attempt.Score = update.Score;
            }
        }

        await _unitOfWork.SaveAsync();
        return await GetAssignmentSubmissionsAsync(assignmentId);
    }
}
