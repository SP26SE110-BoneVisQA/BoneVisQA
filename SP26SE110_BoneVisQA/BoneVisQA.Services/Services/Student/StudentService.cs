using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Models.Notification;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Models.VisualQA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BoneVisQA.Services.Services.Student;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StudentService> _logger;
    private readonly IStudentLearningService _studentLearningService;

    public StudentService(
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork,
        ILogger<StudentService> logger,
        IStudentLearningService studentLearningService,
        INotificationService notificationService,
        IEmailService emailService)
    {
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _studentLearningService = studentLearningService;
        _notificationService = notificationService;
        _emailService = emailService;
    }

    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;

    /// <summary>
    /// Passing score luôn ở thang 100 (0-100). Identity function.
    /// </summary>
    private static int? NormalizePassingScore(int? passingScore, bool isAiGenerated)
    {
        return passingScore;
    }

    public async Task<IReadOnlyList<CaseListItemDto>> GetCasesAsync(Guid studentId)
    {
        var classIds = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        if (classIds.Count == 0)
            return new List<CaseListItemDto>();

        var cases = await _unitOfWork.Context.ClassCases
            .AsNoTracking()
            .Where(cc => classIds.Contains(cc.ClassId))
            .Select(cc => cc.Case)
            .Where(c => c.IsApproved == true && c.IsActive == true)
            .Include(c => c.Category)
            .Include(c => c.CaseTags)
                .ThenInclude(ct => ct.Tag)
            .Include(c => c.MedicalImages)
            .Distinct()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return MapCaseList(cases);
    }

    public async Task<IReadOnlyList<CaseListItemDto>> GetCaseCatalogAsync(CaseFilterRequestDto? filter = null)
    {
        if (filter == null)
        {
            var allCases = await _studentRepository.GetAllCasesAsync();
            return MapCaseList(allCases);
        }

        var repoFilter = new CaseFilter
        {
            CategoryId = filter.CategoryId,
            Difficulty = filter.Difficulty,
            Location = filter.Location,
            LessonType = filter.LessonType
        };
        var filteredCases = await _studentRepository.GetFilteredCasesAsync(repoFilter);
        return MapCaseList(filteredCases);
    }

    private static IReadOnlyList<CaseListItemDto> MapCaseList(IEnumerable<MedicalCase> cases)
    {
        return cases

            .Select(c => new CaseListItemDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                Difficulty = c.Difficulty,
                CategoryName = c.Category?.Name,
                IsApproved = c.IsApproved ?? false,
                ThumbnailImageUrl = c.MedicalImages.FirstOrDefault()?.ImageUrl,
                Tags = c.CaseTags?.Select(ct => ct.Tag.Name).ToList()
            })
            .ToList();
    }

    public async Task<IReadOnlyList<CaseListItemDto>> GetFilteredCasesAsync(Guid studentId, CaseFilterRequestDto filter)
    {
        var repoFilter = new CaseFilter
        {
            CategoryId = filter.CategoryId,
            Difficulty = filter.Difficulty,
            Location = filter.Location,
            LessonType = filter.LessonType
        };
        var cases = await _studentRepository.GetFilteredCasesAsync(repoFilter);

        return cases
            .Select(c => new CaseListItemDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                Difficulty = c.Difficulty,
                CategoryName = c.Category?.Name,
                IsApproved = c.IsApproved ?? false,
                ThumbnailImageUrl = c.MedicalImages.FirstOrDefault()?.ImageUrl,
                Tags = c.CaseTags?.Select(ct => ct.Tag.Name).ToList()
            })
            .ToList();
    }

    public async Task<CaseDetailDto?> GetCaseDetailAsync(Guid caseId, Guid studentId)
    {
        var entity = await _studentRepository.GetCaseWithImagesAsync(caseId);
        if (entity == null)
        {
            return null;
        }

        var viewLog = new CaseViewLog
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CaseId = caseId,
            ViewedAt = DateTime.UtcNow,
            IsCompleted = false
        };

        await _studentRepository.AddCaseViewLogAsync(viewLog);

        return new CaseDetailDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Difficulty = entity.Difficulty,
            CategoryName = entity.Category?.Name,
            ExpertSummary = entity.SuggestedDiagnosis,
            KeyFindings = entity.KeyFindings,
            PrimaryImageUrl = entity.MedicalImages
                .OrderBy(i => i.CreatedAt)
                .Select(i => i.ImageUrl)
                .FirstOrDefault(),
            IsApproved = entity.IsApproved ?? false,
            Images = entity.MedicalImages
                .OrderBy(i => i.CreatedAt)
                .Select(i => new MedicalImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    Modality = i.Modality
                })
                .ToList()
        };
    }

    public async Task<IReadOnlyList<StudentCaseHistoryItemDto>> GetCaseHistoryAsync(Guid studentId)
    {
        var questions = await _unitOfWork.Context.StudentQuestions
            .AsNoTracking()
            .Include(q => q.Case)
            .Include(q => q.CaseAnswers)
            .Where(q => q.StudentId == studentId && q.CaseId.HasValue)
            .ToListAsync();

        var views = await _unitOfWork.Context.CaseViewLogs
            .AsNoTracking()
            .Include(v => v.Case)
            .Where(v => v.StudentId == studentId)
            .ToListAsync();

        var history = new Dictionary<Guid, StudentCaseHistoryItemDto>();

        foreach (var view in views.Where(v => v.Case != null))
        {
            history[view.CaseId] = new StudentCaseHistoryItemDto
            {
                CaseId = view.CaseId,
                CaseTitle = view.Case!.Title,
                CategoryName = view.Case.Category?.Name,
                Difficulty = view.Case.Difficulty,
                LastInteractedAt = view.ViewedAt ?? DateTime.MinValue,
                InteractionType = "Viewed case"
            };
        }

        foreach (var question in questions.Where(q => q.Case != null))
        {
            var latestAnswer = question.CaseAnswers
                .OrderByDescending(a => a.ReviewedAt ?? a.GeneratedAt)
                .FirstOrDefault();

            var item = new StudentCaseHistoryItemDto
            {
                CaseId = question.CaseId!.Value,
                CaseTitle = question.Case!.Title,
                CategoryName = question.Case.Category?.Name,
                Difficulty = question.Case.Difficulty,
                LastInteractedAt = question.CreatedAt ?? DateTime.MinValue,
                InteractionType = "Asked question",
                LatestQuestionText = question.QuestionText,
                LatestAnswerStatus = latestAnswer?.Status,
                ReviewedAt = latestAnswer?.ReviewedAt
            };

            if (!history.TryGetValue(item.CaseId, out var existing) || item.LastInteractedAt >= existing.LastInteractedAt)
            {
                history[item.CaseId] = item;
            }
        }

        return history.Values
            .OrderByDescending(x => x.LastInteractedAt)
            .ToList();
    }

    public async Task<AnnotationDto> CreateAnnotationAsync(Guid studentId, CreateAnnotationRequestDto request)
    {
        var coordinatesJson = BoundingBoxParser.TryParseFromJson(request.Coordinates) is { } box
            ? BoundingBoxParser.Serialize(box)
            : TryParseCoordinatesJson(request.Coordinates);

        var entity = new CaseAnnotation
        {
            Id = Guid.NewGuid(),
            ImageId = request.ImageId,
            Label = request.Label,
            Coordinates = coordinatesJson,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _studentRepository.CreateAnnotationAsync(entity);

        return new AnnotationDto
        {
            Id = created.Id,
            ImageId = created.ImageId,
            Label = created.Label,
            Coordinates = created.Coordinates,
            CreatedAt = created.CreatedAt
        };
    }

    public async Task<StudentQuestionDto> AskQuestionAsync(Guid studentId, AskQuestionRequestDto request)
    {
        var question = new StudentQuestion
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CaseId = request.CaseId == Guid.Empty ? null : request.CaseId,
            AnnotationId = request.AnnotationId,
            QuestionText = request.QuestionText,
            Language = "vi",
            CreatedAt = DateTime.UtcNow
        };

        var created = await _studentRepository.CreateStudentQuestionAsync(question);

        return new StudentQuestionDto
        {
            Id = created.Id,
            StudentId = created.StudentId,
            CaseId = created.CaseId ?? Guid.Empty,
            AnnotationId = created.AnnotationId,
            QuestionText = created.QuestionText,
            CreatedAt = created.CreatedAt
        };
    }


    public async Task<Guid> CreateOrGetVisualQaSessionAsync(Guid studentId, VisualQARequestDto request)
    {
        var isPersonalUpload = !request.CaseId.HasValue;

        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            var existing = await _unitOfWork.Context.VisualQaSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SessionId.Value && s.StudentId == studentId);
            if (existing != null)
                return existing.Id;
        }

        string? coordsToSave = BoundingBoxParser.TryParseFromJson(request.Coordinates) is { } b
            ? BoundingBoxParser.Serialize(b)
            : TryParseCoordinatesJson(request.Coordinates);
        string? imageUrlToSave = request.ImageUrl;

        if (request.AnnotationId.HasValue && !isPersonalUpload)
        {
            var annotationId = request.AnnotationId.Value;

            // Kiểm tra sự tồn tại và lấy tọa độ chính xác (và URL hình ảnh) từ DB.
            // Cần include để có thể lấy URL hình ảnh cho xử lý vision.
            var annotations = await _unitOfWork.CaseAnnotationRepository
                .FindIncludeAsync(a => a.Id == annotationId, a => a.Image);

            var annotation = annotations.FirstOrDefault();
            if (annotation == null)
            {
                throw new ArgumentException($"AnnotationId '{annotationId}' does not exist.");
            }

            coordsToSave = annotation.Coordinates;
            request.Coordinates = BoundingBoxParser.TryParseFromJson(coordsToSave) is { } dbBox
                ? BoundingBoxParser.Serialize(dbBox)
                : coordsToSave;

            if (string.IsNullOrWhiteSpace(request.ImageUrl) && annotation.Image != null)
            {
                request.ImageUrl = annotation.Image.ImageUrl;
            }

            imageUrlToSave = request.ImageUrl;
        }

        var session = new VisualQASession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CaseId = request.CaseId,
            ImageId = request.ImageId,
            CustomImageUrl = imageUrlToSave,
            Status = "PendingLecturerReview",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Context.VisualQaSessions.AddAsync(session);
        await _unitOfWork.SaveAsync();

        return session.Id;
    }

    public async Task SaveVisualQAMessagesAsync(Guid sessionId, VisualQARequestDto request, VisualQAResponseDto response)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new InvalidOperationException("Visual QA session not found.");

        var userMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = "User",
            Content = request.QuestionText,
            Coordinates = TryParseCoordinatesJson(request.Coordinates),
            CreatedAt = DateTime.UtcNow
        };

        var assistantMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = "Assistant",
            Content = response.AnswerText ?? string.Empty,
            SuggestedDiagnosis = response.SuggestedDiagnosis,
            DifferentialDiagnoses = SerializeJsonArray(response.DifferentialDiagnoses),
            KeyImagingFindings = response.KeyImagingFindings,
            ReflectiveQuestions = response.ReflectiveQuestions,
            AiConfidenceScore = response.AiConfidenceScore,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync();
            await _unitOfWork.Context.QaMessages.AddRangeAsync(userMessage, assistantMessage);
            session.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is PostgresException pg)
            {
                _logger.LogError(
                    ex,
                    "SaveVisualQAMessagesAsync: database update failed (SQL state {SqlState}) for session {SessionId}.",
                    pg.SqlState,
                    sessionId);
            }
            else
            {
                _logger.LogError(ex, "SaveVisualQAMessagesAsync: database update failed for session {SessionId}.", sessionId);
            }

            throw new InvalidOperationException(
                "Không thể lưu hội thoại Visual QA vào cơ sở dữ liệu. Vui lòng thử lại sau.",
                ex);
        }
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "vi";
        var t = language.Trim();
        return t.Length >= 2 ? t[..2].ToLowerInvariant() : "vi";
    }

    private static string? TryParseCoordinatesJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var _ = JsonDocument.Parse(value);
            return value;
        }
        catch
        {
            return null;

        }
    }

    private static string? SerializeJsonArray(List<string>? values)
    {
        if (values == null || values.Count == 0)
            return null;

        var normalized = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .ToList();

        return normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized);
    }

    public async Task<IReadOnlyList<StudentQuestionHistoryItemDto>> GetQuestionHistoryAsync(Guid studentId)
    {
        return await _unitOfWork.Context.StudentQuestions
            .AsNoTracking()
            .Where(q => q.StudentId == studentId)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new StudentQuestionHistoryItemDto
            {
                Id = q.Id,
                CaseId = q.CaseId ?? Guid.Empty,
                QuestionText = q.QuestionText,
                CreatedAt = q.CreatedAt,
                AnswerText = q.CaseAnswers
                    .OrderByDescending(a => a.ReviewedAt ?? a.GeneratedAt)
                    .Select(a => a.AnswerText)
                    .FirstOrDefault(),
                StructuredDiagnosis = q.CaseAnswers
                    .OrderByDescending(a => a.ReviewedAt ?? a.GeneratedAt)
                    .Select(a => a.StructuredDiagnosis)
                    .FirstOrDefault(),
                DifferentialDiagnoses = q.CaseAnswers
                    .OrderByDescending(a => a.ReviewedAt ?? a.GeneratedAt)
                    .Select(a => a.DifferentialDiagnoses)
                    .FirstOrDefault(),
                KeyImagingFindings = q.CaseAnswers
                    .OrderByDescending(a => a.ReviewedAt ?? a.GeneratedAt)
                    .Select(a => a.KeyImagingFindings)
                    .FirstOrDefault(),
                ReflectiveQuestions = q.CaseAnswers
                    .OrderByDescending(a => a.ReviewedAt ?? a.GeneratedAt)
                    .Select(a => a.ReflectiveQuestions)
                    .FirstOrDefault(),
                AnswerStatus = q.CaseAnswers
                    .OrderByDescending(a => a.ReviewedAt ?? a.GeneratedAt)
                    .Select(a => a.Status)
                    .FirstOrDefault(),
                ReviewedAt = q.CaseAnswers
                    .OrderByDescending(a => a.ReviewedAt ?? a.GeneratedAt)
                    .Select(a => a.ReviewedAt)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<StudentAnnouncementDto>> GetAnnouncementsAsync(Guid studentId)
    {
        var announcements = await _studentRepository.GetAnnouncementsForStudentAsync(studentId);

        return announcements
            .Select(a => new StudentAnnouncementDto
            {
                Id = a.Id,
                ClassId = a.ClassId,
                ClassName = a.Class?.ClassName,
                Title = a.Title,
                Content = a.Content,
                CreatedAt = a.CreatedAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<QuizListItemDto>> GetAvailableQuizzesAsync(Guid studentId)
    {
        var utcNow = DateTime.UtcNow;
        var sessions = await _studentRepository.GetQuizzesWithSessionForStudentAsync(studentId, utcNow);

        var quizIds = sessions.Select(s => s.QuizId).Distinct().ToList();
        var attempts = await _unitOfWork.QuizAttemptRepository
            .FindByCondition(a => a.StudentId == studentId && quizIds.Contains(a.QuizId))
            .ToListAsync();

        var questionCounts = await _unitOfWork.Context.QuizQuestions
            .Where(q => quizIds.Contains(q.QuizId))
            .GroupBy(q => q.QuizId)
            .Select(g => new { QuizId = g.Key, Count = g.Count() })
            .ToListAsync();
        var countByQuiz = questionCounts.ToDictionary(x => x.QuizId, x => x.Count);

        return sessions.Select(s =>
        {
            var attempt = attempts.FirstOrDefault(a => a.QuizId == s.QuizId);
            return new QuizListItemDto
            {
                QuizId = s.QuizId,
                Title = s.Title,
                ClassId = s.ClassId,
                ClassName = s.ClassName,
                OpenTime = s.OpenTime,
                CloseTime = s.CloseTime,
                TimeLimit = s.TimeLimitMinutes,
                PassingScore = s.PassingScore,
                TotalQuestions = countByQuiz.GetValueOrDefault(s.QuizId),
                IsCompleted = attempt?.CompletedAt != null,
                Score = attempt?.Score,
                AttemptId = attempt?.Id
            };
        }).ToList();
    }


    public async Task<QuizSessionDto> StartQuizAsync(Guid studentId, Guid quizId)
    {
        // Tự động nộp các quiz đã hết hạn trước khi bắt đầu quiz mới
        await _studentLearningService.AutoCloseExpiredAttemptsAsync();

        var quiz = await _studentRepository.GetQuizWithQuestionsAsync(quizId);
        if (quiz == null)
        {
            throw new InvalidOperationException("Quiz does not exist.");
        }

        var utcNow = DateTime.UtcNow;
        if (!await _studentRepository.IsStudentEligibleForAssignedQuizAsync(studentId, quizId, utcNow))
        {
            throw new InvalidOperationException(
                "Bạn chưa được gán quiz này qua lớp đã đăng ký, hoặc quiz không nằm trong thời gian mở.");
        }

        // Lấy session để kiểm tra allow_retake TRƯỚC khi xử lý retake
        var classIds = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        var classSession = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Quiz)
            .FirstOrDefaultAsync(cqs =>
                cqs.QuizId == quizId &&
                classIds.Contains(cqs.ClassId));

        // Kiểm tra thời gian mở — trả lỗi rõ ràng
        var effectiveOpenTime = classSession?.OpenTime ?? quiz.OpenTime;
        var effectiveCloseTime = classSession?.CloseTime ?? quiz.CloseTime;

        if (effectiveOpenTime.HasValue && effectiveOpenTime.Value > utcNow)
        {
            throw new InvalidOperationException(
                $"Quiz chưa mở. Thời gian mở: {effectiveOpenTime.Value:dd/MM/yyyy HH:mm} (giờ Việt Nam).");
        }

        if (effectiveCloseTime.HasValue && effectiveCloseTime.Value <= utcNow)
        {
            throw new InvalidOperationException(
                "Quiz đã đóng. Không thể bắt đầu hoặc tiếp tục làm bài.");
        }

        var existingAttempt = await _studentRepository.GetQuizAttemptAsync(studentId, quizId);
        QuizAttempt attempt;

        if (existingAttempt != null)
        {
            if (existingAttempt.CompletedAt.HasValue)
            {
                // Kiểm tra có được retake không: global flag HOẶC lecturer đã reset riêng
                var globalRetake = classSession?.AllowRetake ?? false;
                var lecturerRetake = classSession?.RetakeResetAt > existingAttempt.CompletedAt;
                if (!globalRetake && !lecturerRetake)
                {
                    throw new InvalidOperationException(
                        "You have already submitted this quiz. Your lecturer will enable retake when needed.");
                }

                // Được retake: xóa đáp án cũ và reset
                var oldAnswers = await _unitOfWork.Context.StudentQuizAnswers
                    .Where(a => a.AttemptId == existingAttempt.Id)
                    .ToListAsync();
                _unitOfWork.Context.StudentQuizAnswers.RemoveRange(oldAnswers);
                existingAttempt.CompletedAt = null;
                existingAttempt.Score = null;
                existingAttempt.StartedAt = DateTime.UtcNow;
                await _unitOfWork.QuizAttemptRepository.UpdateAsync(existingAttempt);
                await _unitOfWork.SaveAsync();
            }

            attempt = existingAttempt;
        }
        else
        {
            attempt = new QuizAttempt
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                QuizId = quizId,
                StartedAt = DateTime.UtcNow
            };

            attempt = await _studentRepository.CreateQuizAttemptAsync(attempt);
        }

        var shuffleQuestions = classSession?.ShuffleQuestions ?? false;

        var questionList = quiz.QuizQuestions.AsEnumerable();

        if (shuffleQuestions)
            questionList = questionList.OrderBy(_ => Random.Shared.Next());

        var questionDtos = questionList
            .Select(q =>
            {
                var caseImageUrl = q.Case?.MedicalImages
                    .OrderBy(mi => mi.CreatedAt ?? DateTime.MaxValue)
                    .Select(mi => mi.ImageUrl)
                    .FirstOrDefault();
                return new StudentQuizQuestionDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText,
                    Type = q.Type?.ToString(),
                    CaseId = q.CaseId,
                    CaseTitle = q.Case?.Title,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    ImageUrl = !string.IsNullOrWhiteSpace(q.ImageUrl) ? q.ImageUrl : caseImageUrl,
                };
            })
            .ToList();

        return new QuizSessionDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            Title = quiz.Title,
            Topic = quiz.Topic,
            TimeLimit = classSession?.TimeLimitMinutes ?? quiz.TimeLimit,
            CloseTime = classSession?.CloseTime ?? quiz.CloseTime,
            Questions = questionDtos
        };
    }
    //Có 2 hàm student submit question và submit quiz, hàm submit question để lưu từng câu hỏi 1, hàm submit quiz để tính điểm và kết thúc quiz

    //===================== phan nam =====================   

    private string? GetOptionText(QuizQuestion question, string? optionKey)
    {
        return optionKey?.ToUpper() switch
        {
            "A" => question.OptionA,
            "B" => question.OptionB,
            "C" => question.OptionC,
            "D" => question.OptionD,
            _ => null
        };
    }

    public async Task<StudentSubmitQuestionResponseDto> SubmitQuizAsync(Guid studentId, StudentSubmitQuestionDto submit)
    {
        var attempt = await _unitOfWork.QuizAttemptRepository
            .FirstOrDefaultAsync(a => a.Id == submit.AttemptId && a.StudentId == studentId)
            ?? throw new KeyNotFoundException("Không tìm thấy lần làm quiz.");

        var question = await _unitOfWork.QuizQuestionRepository
            .GetByIdAsync(submit.QuestionId)
            ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");

        var quiz = await _unitOfWork.QuizRepository
            .GetByIdAsync(attempt.QuizId)
            ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

        var utcNow = DateTime.UtcNow;

        var classIds = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        var session = await _unitOfWork.Context.ClassQuizSessions
            .Include(cqs => cqs.Quiz)
            .FirstOrDefaultAsync(cqs =>
                cqs.QuizId == attempt.QuizId &&
                classIds.Contains(cqs.ClassId) &&
                ((cqs.OpenTime ?? cqs.Quiz!.OpenTime) == null || (cqs.OpenTime ?? cqs.Quiz!.OpenTime) <= utcNow) &&
                ((cqs.CloseTime ?? cqs.Quiz!.CloseTime) == null || (cqs.CloseTime ?? cqs.Quiz!.CloseTime) >= utcNow));

        if (session == null)
            throw new InvalidOperationException("Quiz đã hết thời gian làm bài.");

        var existing = await _unitOfWork.StudentQuizAnswerRepository
            .FirstOrDefaultAsync(a => a.AttemptId == submit.AttemptId
                                   && a.QuestionId == submit.QuestionId);
        if (existing != null)
            throw new InvalidOperationException("Câu hỏi này đã được trả lời.");

        bool isCorrect = string.Equals(
            submit.StudentAnswer?.Trim(),
            question.CorrectAnswer?.Trim(),
            StringComparison.OrdinalIgnoreCase
        );

        var studentQuizAnswer = new StudentQuizAnswer
        {
            Id = Guid.NewGuid(),
            AttemptId = submit.AttemptId,
            QuestionId = submit.QuestionId,
            StudentAnswer = submit.StudentAnswer,
            IsCorrect = isCorrect
        };

        await _unitOfWork.StudentQuizAnswerRepository.AddAsync(studentQuizAnswer);
        await _unitOfWork.SaveAsync();

        return new StudentSubmitQuestionResponseDto
        {
            QuizTitle = quiz.Title,
            QuestionText = question.QuestionText,
            OptionA = question.OptionA,
            OptionB = question.OptionB,
            OptionC = question.OptionC,
            OptionD = question.OptionD,
            StudentAnswer = submit.StudentAnswer?.ToUpper(),
            StudentAnswerText = GetOptionText(question, submit.StudentAnswer),
            CorrectAnswer = question.CorrectAnswer,
            CorrectAnswerText = GetOptionText(question, question.CorrectAnswer),
            IsCorrect = isCorrect
        };
    }

    //===================== phan tran =====================   
    //public async Task<QuizResultDto> SubmitQuizAsync(Guid studentId, SubmitQuizRequestDto request)
    //{
    //    await _unitOfWork.BeginTransactionAsync();
    //    try
    //    {
    //        var attempt = await _studentRepository.GetQuizAttemptByIdAsync(request.AttemptId, studentId);
    //        if (attempt == null)
    //        {
    //            await _unitOfWork.RollbackTransactionAsync();
    //            throw new InvalidOperationException(
    //                "Lần làm quiz không tồn tại hoặc không thuộc về sinh viên này. Kiểm tra attemptId và studentId (phải trùng với student_id của lần làm bài trong bảng quiz_attempts).");
    //        }

    //        var quiz = await _studentRepository.GetQuizWithQuestionsAsync(attempt.QuizId);
    //        if (quiz == null)
    //        {
    //            await _unitOfWork.RollbackTransactionAsync();
    //            throw new InvalidOperationException("Quiz does not exist.");
    //        }

    //        var questionDict = quiz.QuizQuestions.ToDictionary(q => q.Id, q => q);

    //        var answers = new List<StudentQuizAnswer>();
    //        var correctCount = 0;
    //        var unmatchedQuestionIds = new List<Guid>();

    //        foreach (var a in request.Answers)
    //        {
    //            if (!questionDict.TryGetValue(a.QuestionId, out var question))
    //            {
    //                unmatchedQuestionIds.Add(a.QuestionId);
    //                continue;
    //            }

    //            var isCorrect = false;
    //            if (question.CorrectAnswer != null && a.StudentAnswer != null)
    //            {
    //                isCorrect = string.Equals(
    //                    question.CorrectAnswer.Trim(),
    //                    a.StudentAnswer.Trim(),
    //                    StringComparison.OrdinalIgnoreCase);
    //            }

    //            if (isCorrect)
    //            {
    //                correctCount++;
    //            }

    //            answers.Add(new StudentQuizAnswer
    //            {
    //                Id = Guid.NewGuid(),
    //                AttemptId = attempt.Id,
    //                QuestionId = question.Id,
    //                StudentAnswer = a.StudentAnswer,
    //                IsCorrect = isCorrect
    //            });
    //        }

    //        if (unmatchedQuestionIds.Count > 0)
    //        {
    //            var validIds = string.Join(", ", questionDict.Keys.OrderBy(x => x));
    //            throw new InvalidOperationException(
    //                "Một hoặc nhiều questionId không thuộc quiz này: " +
    //                string.Join(", ", unmatchedQuestionIds.Distinct()) +
    //                ". Phải dùng QuestionId từ POST /api/Students/quizzes/{quizId}/start (mỗi phần tử questions[].questionId — đó là cột id trong bảng quiz_questions). " +
    //                "Không dùng quizId (bảng quizzes) làm questionId. " +
    //                (string.IsNullOrEmpty(validIds)
    //                    ? "Quiz hiện không có câu hỏi nào trong quiz_questions."
    //                    : $"Các questionId hợp lệ: {validIds}."));
    //        }

    //        var totalQuestions = quiz.QuizQuestions.Count;
    //        double? score = null;
    //        if (totalQuestions > 0)
    //        {
    //            score = correctCount * 100.0 / totalQuestions;
    //        }

    //        attempt.Score = score;
    //        attempt.CompletedAt = DateTime.UtcNow;

    //        await _studentRepository.AddStudentQuizAnswersAsync(answers);
    //        await _studentRepository.UpdateQuizAttemptAsync(attempt);

    //        await _unitOfWork.CommitTransactionAsync();

    //        var passed = score.HasValue && quiz.PassingScore.HasValue && score.Value >= quiz.PassingScore.Value;

    //        return new QuizResultDto
    //        {
    //            AttemptId = attempt.Id,
    //            QuizId = attempt.QuizId,
    //            Score = score,
    //            PassingScore = quiz.PassingScore,
    //            Passed = passed
    //        };
    //    }
    //    catch
    //    {
    //        await _unitOfWork.RollbackTransactionAsync();
    //        throw;
    //    }
    //}

    public async Task<StudentProgressDto> GetProgressAsync(Guid studentId)
    {
        var (totalCasesViewed, totalQuestionsAsked, quizzesCompleted, totalQuizAnswersSubmitted, avgQuizScore) =
            await _studentRepository.GetStudentAggregateStatsAsync(studentId);

        return new StudentProgressDto
        {
            TotalCasesViewed = totalCasesViewed,
            TotalQuestionsAsked = totalQuestionsAsked,
            QuizzesCompleted = quizzesCompleted,
            TotalQuizAnswersSubmitted = totalQuizAnswersSubmitted,
            AvgQuizScore = avgQuizScore
        };
    }

    /// Trả về danh sách lớp học mà sinh viên đã đăng ký.
    public async Task<IReadOnlyList<StudentClassDto>> GetEnrolledClassesAsync(Guid studentId)
    {
        var enrollments = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Class)
                .ThenInclude(c => c!.Lecturer)
            .Include(e => e.Class)
                .ThenInclude(c => c!.Expert)
            .Where(e => e.StudentId == studentId)
            .ToListAsync();

        var classIds = enrollments
            .Where(e => e.Class != null)
            .Select(e => e.Class!.Id)
            .ToList();

        // Lấy số lượng announcements, quizzes, cases cho mỗi lớp
        var announcementCounts = await _unitOfWork.Context.Announcements
            .Where(a => classIds.Contains(a.ClassId))
            .GroupBy(a => a.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var quizCounts = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => classIds.Contains(cqs.ClassId))
            .GroupBy(cqs => cqs.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var caseCounts = await _unitOfWork.Context.ClassCases
            .Where(cc => classIds.Contains(cc.ClassId))
            .GroupBy(cc => cc.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var result = new List<StudentClassDto>();
        foreach (var enrollment in enrollments)
        {
            if (enrollment.Class == null) continue;
            var c = enrollment.Class;
            result.Add(new StudentClassDto
            {
                ClassId = c.Id,
                ClassName = c.ClassName,
                Semester = c.Semester,
                LecturerId = c.LecturerId,
                LecturerName = c.Lecturer?.FullName,
                ExpertId = c.ExpertId,
                ExpertName = c.Expert?.FullName,
                TotalAnnouncements = announcementCounts.GetValueOrDefault(c.Id, 0),
                TotalQuizzes = quizCounts.GetValueOrDefault(c.Id, 0),
                TotalCases = caseCounts.GetValueOrDefault(c.Id, 0),
                EnrolledAt = enrollment.EnrolledAt,
            });
        }

        return result;
    }

    public async Task<StudentClassDetailDto> GetClassDetailAsync(Guid studentId, Guid classId)
    {
        // Verify enrollment
        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Class)
                .ThenInclude(c => c!.Lecturer)
            .Include(e => e.Class)
                .ThenInclude(c => c!.Expert)
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.ClassId == classId)
            ?? throw new KeyNotFoundException("You are not enrolled in this class.");

        var cls = enrollment.Class!;

        // Quiz IDs assigned to this class (via ClassQuizSession)
        var classQuizIds = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => cqs.ClassId == classId)
            .Select(cqs => cqs.QuizId)
            .ToListAsync();

        // Student attempts — chỉ dùng để gắn trạng thái hoàn thành / điểm; danh sách quiz phải lấy theo lớp (ClassQuizSessions),
        // không chỉ những quiz đã có attempt (sinh viên chưa làm vẫn phải thấy bài được gán).
        var quizAttempts = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && classQuizIds.Contains(a.QuizId))
            .Select(a => new { a.QuizId, a.CompletedAt, a.Score })
            .ToListAsync();

        var quizzesRaw = await _unitOfWork.Context.Quizzes
            .AsNoTracking()
            .Where(q => classQuizIds.Contains(q.Id))
            .Select(q => new { q.Id, q.Title, q.Topic, q.OpenTime, q.CloseTime, q.TimeLimit, q.PassingScore })
            .OrderByDescending(q => q.OpenTime ?? DateTime.MinValue)
            .ThenBy(q => q.Title)
            .ToListAsync();

        var questionCounts = await _unitOfWork.Context.QuizQuestions
            .Where(qq => classQuizIds.Contains(qq.QuizId))
            .GroupBy(qq => qq.QuizId)
            .Select(g => new { QuizId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.QuizId, x => x.Count);

        var quizzes = quizzesRaw
            .Select(q =>
            {
                var latest = quizAttempts
                    .Where(a => a.QuizId == q.Id)
                    .OrderByDescending(a => a.CompletedAt ?? DateTime.MinValue)
                    .FirstOrDefault();
                return new ClassQuizSummaryDto
                {
                    QuizId = q.Id,
                    Title = q.Title,
                    Topic = q.Topic,
                    OpenTime = q.OpenTime,
                    CloseTime = q.CloseTime,
                    TotalQuestions = questionCounts.GetValueOrDefault(q.Id, 0),
                    TimeLimit = q.TimeLimit,
                    PassingScore = q.PassingScore,
                    IsCompleted = latest?.CompletedAt.HasValue ?? false,
                    Score = latest?.Score,
                };
            }).ToList();

        // Students in this class
        var students = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Where(e => e.ClassId == classId)
            .Select(e => new ClassStudentSummaryDto
            {
                StudentId = e.StudentId,
                StudentName = e.Student != null ? e.Student.FullName : "Unknown",
                StudentCode = e.Student != null ? e.Student.SchoolCohort : null,
            })
            .ToListAsync();

        // Announcements
        var announcements = await _unitOfWork.Context.Announcements
            .AsNoTracking()
            .Where(a => a.ClassId == classId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ClassAnnouncementDto
            {
                Id = a.Id,
                Title = a.Title,
                Content = a.Content,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync();

        return new StudentClassDetailDto
        {
            ClassId = cls.Id,
            ClassName = cls.ClassName,
            Semester = cls.Semester,
            LecturerId = cls.LecturerId,
            LecturerName = cls.Lecturer?.FullName,
            ExpertId = cls.ExpertId,
            ExpertName = cls.Expert?.FullName,
            EnrolledAt = enrollment.EnrolledAt,
            Quizzes = quizzes,
            Students = students,
            Announcements = announcements,
        };
    }

    public async Task LeaveEnrolledClassAsync(Guid studentId, Guid classId)
    {
        var enrollment = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.StudentId == studentId && e.ClassId == classId)
            .FirstOrDefaultAsync();

        if (enrollment == null)
        {
            throw new KeyNotFoundException("You are not enrolled in this class.");
        }

        await _unitOfWork.ClassEnrollmentRepository.DeleteAsync(enrollment.Id);
        await _unitOfWork.SaveAsync();
        _logger.LogInformation("[LeaveEnrolledClassAsync] Student {StudentId} left class {ClassId}.", studentId, classId);
    }

    public async Task RequestRetakeAsync(Guid studentId, Guid quizId)
    {
        var student = await _unitOfWork.Context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == studentId)
            ?? throw new KeyNotFoundException("Không tìm thấy sinh viên.");

        var quiz = await _unitOfWork.Context.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quizId)
            ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

        var classSession = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Class)
            .Where(cqs => cqs.QuizId == quizId)
            .Where(cqs => cqs.Class != null)
            .ToListAsync();

        if (classSession.Count == 0)
            throw new InvalidOperationException("Quiz này không được gán qua lớp học.");

        // Gửi notification + email cho lecturer của mỗi lớp
        foreach (var session in classSession)
        {
            var academicClass = session.Class!;
            if (academicClass.LecturerId == null) continue;

            // Notification (SignalR real-time)
            var notifTitle = $"Retake Request: {quiz.Title}";
            var notifMsg = $"Student \"{student.FullName}\" requested a retake for quiz \"{quiz.Title}\" in class \"{academicClass.ClassName}\".";
            await _notificationService.SendNotificationToUserAsync(
                academicClass.LecturerId.Value,
                notifTitle,
                notifMsg,
                "retake_request",
                $"/lecturer/quizzes/{quizId}/results"
            );

            // Email (nền — không chặn nếu thất bại)
            try
            {
                var lecturer = await _unitOfWork.Context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == academicClass.LecturerId);
                if (lecturer != null)
                {
                    await _emailService.SendRetakeRequestEmailAsync(
                        lecturer.Email,
                        student.FullName,
                        quiz.Title,
                        academicClass.ClassName,
                        lecturer.FullName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RequestRetakeAsync] Failed to send retake email for quiz {QuizId}", quizId);
            }
        }

        _logger.LogInformation("[RequestRetakeAsync] Student {StudentId} requested retake for quiz {QuizId}", studentId, quizId);
    }
}
