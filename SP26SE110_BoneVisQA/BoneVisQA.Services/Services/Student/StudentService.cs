using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Repositories.UnitOfWork;
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
        var coordinatesJson = TryParseCoordinatesJson(request.Coordinates);

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

        string? coordsToSave = request.Coordinates;
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
            request.Coordinates = coordsToSave; // keep pipeline in sync with the saved/authoritative coordinates

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
        // PostgreSQL case_answers_status_check: only 'Pending', 'Approved', 'Edited', 'Rejected'.
        var status = ClassifyVisualQaAnswerStatus(response);

        var answer = new CaseAnswer
        {
            Id = Guid.NewGuid(),
            QuestionId = questionId,
            AnswerText = response.AnswerText,
            StructuredDiagnosis = response.SuggestedDiagnosis,
            DifferentialDiagnoses = response.DifferentialDiagnoses,
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
        return IsVisualQaRejectedResponse(response) ? "Rejected" : "Pending";
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
        var items = await _unitOfWork.Context.StudentQuestions
            .AsNoTracking()
            .Include(q => q.CaseAnswers)
            .Where(q => q.StudentId == studentId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return items
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
                AnswerStatus = q.CaseAnswers
                    .OrderByDescending(a => a.ReviewedAt ?? a.GeneratedAt)
                    .Select(a => a.Status)
                    .FirstOrDefault(),
                ReviewedAt = q.CaseAnswers
                    .OrderByDescending(a => a.ReviewedAt ?? a.GeneratedAt)
                    .Select(a => a.ReviewedAt)
                    .FirstOrDefault()
            })
            .ToList();
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
                PassingScore = (int?)s.PassingScore,
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

        var existingAttempt = await _studentRepository.GetQuizAttemptAsync(studentId, quizId);
        QuizAttempt attempt;

        if (existingAttempt != null)
        {
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

        var questions = quiz.QuizQuestions
            .Select(q => new StudentQuizQuestionDto
            {
                QuestionId = q.Id,
                QuestionText = q.QuestionText,
                Type = q.Type,
                CaseId = q.CaseId ?? Guid.Empty,
                ImageUrl = q.ImageUrl
            })
            .ToList();

        return new QuizSessionDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            Title = quiz.Title,
            Questions = questions
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
}
