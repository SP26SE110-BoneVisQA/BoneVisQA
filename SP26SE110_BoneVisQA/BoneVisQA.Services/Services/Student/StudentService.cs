using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
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
        IStudentLearningService studentLearningService)
    {
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _studentLearningService = studentLearningService;
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
            ReflectiveQuestions = entity.ReflectiveQuestions,
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
        var coordinatesJson = request.CustomPolygon is { Count: >= 3 }
            ? PolygonAnnotationParser.SerializePolygon(request.CustomPolygon)
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


    public async Task<StudentQuestionDto> CreateVisualQAQuestionAsync(Guid studentId, VisualQARequestDto request)
    {
        var isPersonalUpload = !request.CaseId.HasValue;

        // Personal uploads must not reference existing case/annotation rows.
        // This prevents FK violations for dummy or invalid GUIDs coming from the client.
        var caseIdToSave = isPersonalUpload ? null : request.CaseId;
        var annotationIdToSave = isPersonalUpload ? null : request.AnnotationId;

        // Promote polygon sent as raw JSON string in Coordinates (multipart / legacy clients).
        if (request.CustomPolygon == null && !string.IsNullOrWhiteSpace(request.Coordinates))
        {
            var parsed = PolygonAnnotationParser.TryParsePolygonFromJson(request.Coordinates);
            if (parsed is { Count: >= 3 })
                request.CustomPolygon = parsed;
        }

        string? coordsToSave = request.CustomPolygon is { Count: >= 3 }
            ? PolygonAnnotationParser.SerializePolygon(request.CustomPolygon)
            : TryParseCoordinatesJson(request.Coordinates);
        string? imageUrlToSave = request.ImageUrl;

        if (request.AnnotationId.HasValue && !isPersonalUpload)
        {
            var annotationId = request.AnnotationId.Value;

            // Validate existence and fetch authoritative coordinates (and image URL) from DB.
            // Includes are needed so we can also derive the image URL for vision processing.
            var annotations = await _unitOfWork.CaseAnnotationRepository
                .FindIncludeAsync(a => a.Id == annotationId, a => a.Image);

            var annotation = annotations.FirstOrDefault();
            if (annotation == null)
            {
                throw new ArgumentException($"AnnotationId '{annotationId}' does not exist.");
            }

            coordsToSave = annotation.Coordinates;
            var fromDb = PolygonAnnotationParser.TryParsePolygonFromJson(coordsToSave);
            if (fromDb is { Count: >= 3 })
            {
                request.CustomPolygon = fromDb;
                request.Coordinates = null;
            }
            else
            {
                request.CustomPolygon = null;
                request.Coordinates = coordsToSave;
            }

            if (string.IsNullOrWhiteSpace(request.ImageUrl) && annotation.Image != null)
            {
                request.ImageUrl = annotation.Image.ImageUrl;
            }

            imageUrlToSave = request.ImageUrl;
        }

        var language = NormalizeLanguage(request.Language);

        var question = new StudentQuestion
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CaseId = caseIdToSave,
            AnnotationId = annotationIdToSave,
            QuestionText = request.QuestionText,
            Language = language,
            CustomImageUrl = imageUrlToSave,
            CustomCoordinates = coordsToSave,
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

    public async Task SaveVisualQAAnswerAsync(Guid questionId, VisualQAResponseDto response)
    {
        // PostgreSQL case_answers_status_check — see CaseAnswerStatuses / db scripts.
        var status = ClassifyVisualQaAnswerStatus(response);

        var answer = new CaseAnswer
        {
            Id = Guid.NewGuid(),
            QuestionId = questionId,
            AnswerText = response.AnswerText,
            StructuredDiagnosis = response.SuggestedDiagnosis,
            DifferentialDiagnoses = response.DifferentialDiagnoses,
            KeyImagingFindings = response.KeyImagingFindings,
            ReflectiveQuestions = response.ReflectiveQuestions,
            AiConfidenceScore = response.AiConfidenceScore,
            Status = status,
            GeneratedAt = DateTime.UtcNow
        };

        await _studentRepository.CreateCaseAnswerAsync(answer);

        if (response.Citations != null && response.Citations.Count > 0)
        {
            var citations = response.Citations.Select(c => new Citation
            {
                Id = Guid.NewGuid(),
                AnswerId = answer.Id,
                ChunkId = c.ChunkId,
                SimilarityScore = 0d

            });

            await _studentRepository.AddCitationsAsync(citations);
        }

        try
        {
            await _unitOfWork.SaveAsync();
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is PostgresException pg)
            {
                _logger.LogError(
                    ex,
                    "SaveVisualQAAnswerAsync: database update failed (SQL state {SqlState}) for question {QuestionId}.",
                    pg.SqlState,
                    questionId);
            }
            else
            {
                _logger.LogError(ex, "SaveVisualQAAnswerAsync: database update failed for question {QuestionId}.", questionId);
            }

            throw new InvalidOperationException(
                "Không thể lưu câu trả lời AI vào cơ sở dữ liệu. Vui lòng thử lại sau.",
                ex);
        }
    }

    /// <summary>
    /// Maps AI outcomes to <c>case_answers.status</c> values allowed by PostgreSQL (<c>case_answers_status_check</c>).
    /// </summary>
    private static string ClassifyVisualQaAnswerStatus(VisualQAResponseDto response)
    {
        if (IsVisualQaRejectedResponse(response))
            return CaseAnswerStatuses.Rejected;

        if (response.AiConfidenceScore.HasValue
            && response.AiConfidenceScore.Value >= LecturerTriageThresholds.MinConfidenceToBypassTriage)
            return CaseAnswerStatuses.Approved;

        return CaseAnswerStatuses.RequiresLecturerReview;
    }

    /// <summary>
    /// Detects pipeline rejections / fallbacks (must stay in sync with GeminiService + VisualQaAiService copy).
    /// </summary>
    private static bool IsVisualQaRejectedResponse(VisualQAResponseDto response)
    {
        var t = response.AnswerText?.Trim() ?? string.Empty;
        if (t.Length == 0)
            return true;

        // Exact / prefix matches for standardized messages.
        const string noContext =
            "Dữ liệu y khoa hiện có không chứa thông tin để trả lời câu hỏi này.";
        if (string.Equals(t, noContext, StringComparison.Ordinal))
            return true;

        if (t.StartsWith("Xin lỗi, dựa trên cơ sở dữ liệu y khoa cơ xương khớp", StringComparison.Ordinal))
            return true;

        if (t.StartsWith("Hình ảnh bạn gửi không phải là phim X-quang", StringComparison.OrdinalIgnoreCase))
            return true;

        if (t.Contains("Không thể truy cập hình ảnh y khoa từ bộ lưu trữ", StringComparison.Ordinal))
            return true;

        var noDiagnosis = string.IsNullOrWhiteSpace(response.SuggestedDiagnosis)
                          && string.IsNullOrWhiteSpace(response.DifferentialDiagnoses);
        var noCitations = response.Citations == null || response.Citations.Count == 0;

        // Typical rejection path: no RAG citations and no structured diagnosis, plus refusal-like wording or short reply.
        if (noDiagnosis && noCitations)
        {
            if (t.Length <= 480)
                return true;

            if (ContainsRefusalHeuristic(t))
                return true;
        }

        return false;
    }

    private static bool ContainsRefusalHeuristic(string t)
    {
        return t.Contains("không chứa thông tin để trả lời", StringComparison.OrdinalIgnoreCase)
               || t.Contains("không tìm thấy thông tin đủ tin cậy", StringComparison.OrdinalIgnoreCase)
               || t.Contains("không phải là phim X-quang", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Tôi chỉ hỗ trợ phân tích các vấn đề về hệ vận động", StringComparison.OrdinalIgnoreCase)
               || t.Contains("ngoài phạm vi", StringComparison.OrdinalIgnoreCase)
               || t.Contains("không thuộc chuyên ngành", StringComparison.OrdinalIgnoreCase);
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
                Score = attempt?.Score
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
            throw new InvalidOperationException("Quiz không tồn tại.");
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
            .FirstOrDefaultAsync(cqs =>
                cqs.QuizId == quizId &&
                classIds.Contains(cqs.ClassId) &&
                (cqs.OpenTime == null || cqs.OpenTime <= utcNow) &&
                (cqs.CloseTime == null || cqs.CloseTime >= utcNow));

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
                        "Bạn đã nộp bài quiz này. Giảng viên sẽ cho phép làm lại khi cần.");
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
                    Type = q.Type,
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
            TimeLimit = quiz.TimeLimit,
            Questions = questionDtos
        };
    }
    //co 2 ham student submit question va submit quiz, ham submit question de luu tung cau hoi 1, ham submit quiz de tinh diem va ket thuc quiz

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
            .FirstOrDefaultAsync(cqs =>
                cqs.QuizId == attempt.QuizId &&
                classIds.Contains(cqs.ClassId) &&
                (cqs.OpenTime == null || cqs.OpenTime <= utcNow) &&
                (cqs.CloseTime == null || cqs.CloseTime >= utcNow));

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
    //            throw new InvalidOperationException("Quiz không tồn tại.");
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
}
