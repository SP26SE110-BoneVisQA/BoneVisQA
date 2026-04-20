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
    private readonly ISupabaseStorageService _storageService;

    public StudentService(
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork,
        ILogger<StudentService> logger,
        IStudentLearningService studentLearningService,
        ISupabaseStorageService storageService,
        INotificationService notificationService,
        IEmailService emailService)
    {
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _studentLearningService = studentLearningService;
        _storageService = storageService;
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
            LesionType = filter.LesionType ?? filter.LessonType,
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
            LesionType = filter.LesionType ?? filter.LessonType,
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
            Status = "Active",
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

        var normalizedClientRequestId = NormalizeClientRequestId(request.ClientRequestId);
        if (!string.IsNullOrWhiteSpace(normalizedClientRequestId))
        {
            var alreadyPersisted = await _unitOfWork.Context.QaMessages
                .AsNoTracking()
                .AnyAsync(m =>
                    m.SessionId == sessionId &&
                    m.Role == "Assistant" &&
                    m.ClientRequestId == normalizedClientRequestId);
            if (alreadyPersisted)
                return;
        }

        var coordJson = TryParseCoordinatesJson(request.Coordinates);
        if (string.IsNullOrWhiteSpace(coordJson))
        {
            coordJson = await _unitOfWork.Context.QaMessages
                .AsNoTracking()
                .Where(m => m.SessionId == sessionId && m.Role == "User" && m.Coordinates != null)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(m => m.Coordinates)
                .FirstOrDefaultAsync();
        }

        var userMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = "User",
            Content = request.QuestionText,
            Coordinates = coordJson,
            ClientRequestId = normalizedClientRequestId,
            CreatedAt = DateTime.UtcNow
        };

        var assistantMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = "Assistant",
            Content = response.AnswerText ?? string.Empty,
            ClientRequestId = normalizedClientRequestId,
            CitationsJson = VisualQaCitationMetadataBuilder.SerializeMany(response.Citations),
            SuggestedDiagnosis = response.SuggestedDiagnosis,
            DifferentialDiagnoses = SerializeJsonArray(response.DifferentialDiagnoses),
            KeyImagingFindings = response.KeyImagingFindings,
            ReflectiveQuestions = response.ReflectiveQuestions,
            AiConfidenceScore = response.AiConfidenceScore,
            CreatedAt = DateTime.UtcNow
        };
        response.TurnId = assistantMessage.Id.ToString();
        response.UserQuestionText = userMessage.Content;

        try
        {
            await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync();
            await _unitOfWork.Context.QaMessages.AddRangeAsync(userMessage, assistantMessage);
            if (response.Citations != null && response.Citations.Count > 0)
            {
                var citationRows = response.Citations
                    .Where(c => c.ChunkId != Guid.Empty)
                    .Select(c => new Citation
                    {
                        Id = Guid.NewGuid(),
                        MessageId = assistantMessage.Id,
                        ChunkId = c.ChunkId,
                        SimilarityScore = response.AiConfidenceScore ?? 0d
                    })
                    .ToList();

                if (citationRows.Count > 0)
                    await _unitOfWork.Context.Citations.AddRangeAsync(citationRows);
            }
            session.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is PostgresException pg)
            {
                if (pg.SqlState == PostgresErrorCodes.UniqueViolation
                    && string.Equals(pg.ConstraintName, "ux_qa_messages_session_client_request_role", StringComparison.Ordinal))
                {
                    _logger.LogInformation(
                        "SaveVisualQAMessagesAsync: duplicate client request ignored for session {SessionId} and clientRequestId {ClientRequestId}.",
                        sessionId,
                        normalizedClientRequestId);
                    return;
                }

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
                "Unable to save Visual QA conversation to the database. Please try again later.",
                ex);
        }
    }

    public async Task<VisualQAResponseDto?> GetExistingVisualQaResponseAsync(
        Guid studentId,
        Guid sessionId,
        string clientRequestId,
        CancellationToken cancellationToken = default)
    {
        var normalizedClientRequestId = NormalizeClientRequestId(clientRequestId);
        if (string.IsNullOrWhiteSpace(normalizedClientRequestId))
            return null;

        var sessionExists = await _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.StudentId == studentId, cancellationToken);
        if (!sessionExists)
            return null;

        var assistantMessage = await _unitOfWork.Context.QaMessages
            .AsNoTracking()
            .Include(m => m.Citations)
                .ThenInclude(c => c.Chunk)
                    .ThenInclude(ch => ch.Doc)
            .Where(m => m.SessionId == sessionId && m.Role == "Assistant" && m.ClientRequestId == normalizedClientRequestId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (assistantMessage == null)
            return null;

        var sessionCaseId = await _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.StudentId == studentId)
            .Select(s => s.CaseId)
            .FirstAsync(cancellationToken);

        return new VisualQAResponseDto
        {
            SessionId = sessionId,
            CaseId = sessionCaseId,
            TurnId = assistantMessage.Id.ToString(),
            UserQuestionText = await _unitOfWork.Context.QaMessages
                .AsNoTracking()
                .Where(m =>
                    m.SessionId == sessionId &&
                    m.Role == "User" &&
                    m.ClientRequestId == normalizedClientRequestId)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(m => m.Content)
                .FirstOrDefaultAsync(cancellationToken),
            AnswerText = assistantMessage.Content,
            SuggestedDiagnosis = assistantMessage.SuggestedDiagnosis,
            DifferentialDiagnoses = DeserializeJsonArrayToList(assistantMessage.DifferentialDiagnoses).ToList(),
            KeyImagingFindings = assistantMessage.KeyImagingFindings,
            ReflectiveQuestions = assistantMessage.ReflectiveQuestions,
            AiConfidenceScore = assistantMessage.AiConfidenceScore,
            ErrorMessage = null,
            ResponseKind = DetermineResponseKind(assistantMessage),
            PolicyReason = DeterminePolicyReason(assistantMessage),
            ClientRequestId = normalizedClientRequestId,
            Citations = ResolveMessageCitations(assistantMessage).ToList()
        };
    }

    public async Task<VisualQARequestDto> HydrateVisualQaFollowUpContextAsync(
        Guid studentId,
        Guid sessionId,
        VisualQARequestDto request,
        CancellationToken cancellationToken = default)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .Include(s => s.Image)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.StudentId == studentId, cancellationToken)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
            request.ImageUrl = session.CustomImageUrl ?? session.Image?.ImageUrl;

        if (string.IsNullOrWhiteSpace(request.Coordinates))
        {
            var latestQuestionCoordinates = await _unitOfWork.Context.QaMessages
                .AsNoTracking()
                .Where(m => m.SessionId == sessionId && m.Role == "User" && m.Coordinates != null)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(m => m.Coordinates)
                .FirstOrDefaultAsync(cancellationToken);
            request.Coordinates = latestQuestionCoordinates;
        }

        if (!request.ImageId.HasValue)
            request.ImageId = session.ImageId;
        if (!request.CaseId.HasValue)
            request.CaseId = session.CaseId;

        return request;
    }

    public async Task<VisualQaCapabilitiesDto> GetVisualQaSessionCapabilitiesAsync(
        Guid studentId,
        Guid sessionId,
        int maxUserQuestions = 3,
        CancellationToken cancellationToken = default)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.StudentId == studentId, cancellationToken);
        if (session == null)
            throw new KeyNotFoundException("Q&A session not found.");

        var userTurnCount = await CountBillableUserTurnsAsync(sessionId, cancellationToken);

        var lastActivity = session.UpdatedAt ?? session.CreatedAt;
        var isExpired = DateTime.UtcNow - lastActivity >= TimeSpan.FromHours(24);
        var turnLimitExceeded = userTurnCount >= maxUserQuestions;
        var isReadOnly = isExpired || turnLimitExceeded;

        var reason = turnLimitExceeded
            ? "TURN_LIMIT_EXCEEDED"
            : isExpired
                ? "SESSION_EXPIRED"
                : null;

        var canRequestReview = !isReadOnly
            && !BlocksStudentReviewRequest(session.Status)
            && await HasVisualQaReviewPathAsync(studentId, session.CaseId, cancellationToken);

        return new VisualQaCapabilitiesDto
        {
            TurnsUsed = userTurnCount,
            TurnLimit = maxUserQuestions,
            IsReadOnly = isReadOnly,
            CanAskNext = !isReadOnly && !turnLimitExceeded,
            CanRequestReview = canRequestReview,
            Reason = reason
        };
    }

    private static bool BlocksStudentReviewRequest(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;
        return status.Trim() switch
        {
            "PendingExpertReview" or "EscalatedToExpert" or "LecturerApproved" or "ExpertApproved" or "Rejected" => true,
            _ => false
        };
    }

    private async Task<bool> HasVisualQaReviewPathAsync(Guid studentId, Guid? caseId, CancellationToken cancellationToken)
    {
        var enrolledWithLecturer = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .Join(
                _unitOfWork.Context.AcademicClasses.Where(c => c.LecturerId != null),
                e => e.ClassId,
                c => c.Id,
                (e, c) => c.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (enrolledWithLecturer.Count == 0)
            return false;

        if (!caseId.HasValue || caseId.Value == Guid.Empty)
            return true;

        var classIdsForCase = await _unitOfWork.Context.ClassCases
            .AsNoTracking()
            .Where(cc => cc.CaseId == caseId.Value)
            .Select(cc => cc.ClassId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (classIdsForCase.Count == 0)
            return true;

        return enrolledWithLecturer.Any(classIdsForCase.Contains);
    }

    public async Task ValidateSessionStateAsync(Guid studentId, Guid sessionId, int maxUserQuestions = 3)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.StudentId == studentId);
        if (session == null)
            throw new KeyNotFoundException("Q&A session not found.");

        var lastActivity = session.UpdatedAt ?? session.CreatedAt;
        var inactiveTime = DateTime.UtcNow - lastActivity;
        if (inactiveTime.TotalHours >= 24)
            throw new InvalidOperationException("SESSION_EXPIRED");

        var userTurnCount = await CountBillableUserTurnsAsync(sessionId);

        if (userTurnCount >= maxUserQuestions)
            throw new InvalidOperationException("TURN_LIMIT_EXCEEDED");
    }

    public async Task RequestVisualQaReviewAsync(Guid studentId, Guid sessionId, Guid? assistantMessageId = null)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.StudentId == studentId)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        var lastActivity = session.UpdatedAt ?? session.CreatedAt;
        var inactiveTime = DateTime.UtcNow - lastActivity;
        if (inactiveTime.TotalHours >= 24)
            throw new InvalidOperationException("SESSION_EXPIRED");

        var targetAssistantMessage = assistantMessageId.HasValue
            ? session.Messages.FirstOrDefault(m => m.Id == assistantMessageId.Value && m.Role == "Assistant")
            : session.Messages
                .Where(m => m.Role == "Assistant")
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();
        if (targetAssistantMessage == null)
            throw new KeyNotFoundException("Assistant turn not found for review request.");

        session.RequestedReviewMessageId = targetAssistantMessage.Id;
        session.Status = "PendingExpertReview";
        session.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveAsync();

        var enrollments = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Class)
            .Where(e => e.StudentId == studentId && e.Class != null && e.Class.LecturerId != null)
            .ToListAsync();

        var student = await _unitOfWork.Context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == studentId);

        var firstUser = session.Messages
            .Where(m => string.Equals(m.Role, "User", StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .FirstOrDefault();
        var snippet = (firstUser?.Content ?? string.Empty).Trim();
        if (snippet.Length > 200)
            snippet = snippet[..200].TrimEnd() + "…";

        foreach (var e in enrollments)
        {
            var lecturerId = e.Class!.LecturerId!.Value;
            var body =
                $"{student?.FullName ?? "A student"} requested a review on a Visual QA session." +
                (string.IsNullOrWhiteSpace(snippet) ? string.Empty : $"\nQuestion: {snippet}");

            await _notificationService.SendNotificationToUserAsync(
                lecturerId,
                "Student requested Visual QA review",
                body.Trim(),
                "visual_qa_review_request",
                $"/lecturer/triage?classId={e.ClassId}");
        }
    }

    public async Task<CaseCatalogFiltersDto> GetCaseCatalogFiltersAsync(CancellationToken cancellationToken = default)
    {
        var locationTypes = new[] { "Location", "BoneLocation" };
        var lesionTypes = new[] { "Lesion Type", "Lesion" };

        var locations = await _unitOfWork.Context.Tags
            .AsNoTracking()
            .Where(t => locationTypes.Contains(t.Type))
            .Select(t => t.Name.Trim())
            .Where(n => n.Length > 0)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var lesions = await _unitOfWork.Context.Tags
            .AsNoTracking()
            .Where(t => lesionTypes.Contains(t.Type))
            .Select(t => t.Name.Trim())
            .Where(n => n.Length > 0)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        return new CaseCatalogFiltersDto
        {
            Locations = locations,
            LesionTypes = lesions,
            Difficulties = new[] { "Easy", "Medium", "Hard" }
        };
    }

    public async Task RequestSupportAsync(Guid studentId, Guid answerId, CancellationToken cancellationToken = default)
    {
        var answer = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question!)
                .ThenInclude(q => q.Case)
            .Include(a => a.Question!)
                .ThenInclude(q => q.Annotation!)
                .ThenInclude(an => an!.Image)
            .FirstOrDefaultAsync(a => a.Id == answerId, cancellationToken)
            ?? throw new KeyNotFoundException("Case answer not found.");

        if (answer.Question.StudentId != studentId)
            throw new InvalidOperationException("You can only request support for your own answer.");

        answer.Status = CaseAnswerStatuses.RequiresLecturerReview;
        answer.ReviewedAt = null;
        answer.ReviewedById = null;
        answer.EscalatedAt = null;
        answer.EscalatedById = null;
        await _unitOfWork.SaveAsync();

        var enrollments = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Class)
            .Where(e => e.StudentId == studentId && e.Class != null && e.Class.LecturerId != null)
            .ToListAsync(cancellationToken);

        var q = answer.Question;
        var snippet = q.QuestionText.Trim();
        if (snippet.Length > 200)
            snippet = snippet[..200].TrimEnd() + "…";

        foreach (var e in enrollments)
        {
            var lecturerId = e.Class!.LecturerId!.Value;
            var title = "Student requested lecturer review";
            var body =
                $"Case: {q.Case?.Title ?? "N/A"}\n" +
                $"Question: {snippet}\n" +
                (string.IsNullOrWhiteSpace(answer.StructuredDiagnosis)
                    ? string.Empty
                    : $"Suggested diagnosis (AI): {answer.StructuredDiagnosis}\n") +
                (string.IsNullOrWhiteSpace(answer.KeyImagingFindings)
                    ? string.Empty
                    : $"Key imaging findings: {answer.KeyImagingFindings}\n") +
                (q.Annotation != null
                    ? $"Annotation: {q.Annotation.Label}\n"
                    : string.Empty);

            await _notificationService.SendNotificationToUserAsync(
                lecturerId,
                title,
                body.Trim(),
                "case_qa_triage",
                $"/lecturer/triage?classId={e.ClassId}");
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

    public async Task<PagedResultDto<VisualQaSessionHistoryItemDto>> GetVisualQaHistoryAsync(
        Guid studentId,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
        => await GetVisualQaHistoryInternalAsync(studentId, limit, offset, cancellationToken, hasCaseSession: null);

    public async Task<PagedResultDto<VisualQaSessionHistoryItemDto>> GetVisualQaPersonalHistoryAsync(
        Guid studentId,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
        => await GetVisualQaHistoryInternalAsync(studentId, limit, offset, cancellationToken, hasCaseSession: false);

    public async Task<PagedResultDto<VisualQaSessionHistoryItemDto>> GetVisualQaCaseHistoryAsync(
        Guid studentId,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
        => await GetVisualQaHistoryInternalAsync(studentId, limit, offset, cancellationToken, hasCaseSession: true);

    private async Task<PagedResultDto<VisualQaSessionHistoryItemDto>> GetVisualQaHistoryInternalAsync(
        Guid studentId,
        int limit,
        int offset,
        CancellationToken cancellationToken,
        bool? hasCaseSession)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(offset, 0);
        const int snippetMax = 240;
        var baseQuery = _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .Where(s => s.StudentId == studentId);

        if (hasCaseSession.HasValue)
            baseQuery = hasCaseSession.Value
                ? baseQuery.Where(s => s.CaseId != null)
                : baseQuery.Where(s => s.CaseId == null);

        var orderedQuery = baseQuery.OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        var sessions = await baseQuery
            .Include(s => s.Image)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var list = new List<VisualQaSessionHistoryItemDto>(sessions.Count);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var firstQuestionsBySession = await _unitOfWork.Context.QaMessages
            .AsNoTracking()
            .Where(m => sessionIds.Contains(m.SessionId) && m.Role == "User")
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .GroupBy(m => m.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                Question = g.Select(x => x.Content).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.SessionId, x => x.Question, cancellationToken);
        var lastResponderBySession = await _unitOfWork.Context.QaMessages
            .AsNoTracking()
            .Where(m => sessionIds.Contains(m.SessionId))
            .GroupBy(m => m.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                Role = g.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(x => x.Role).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.SessionId, x => x.Role, cancellationToken);

        var rejectedSessionIds = sessions.Where(x => string.Equals(x.Status, "Rejected", StringComparison.Ordinal)).Select(x => x.Id).ToList();
        Dictionary<Guid, string> rejectionReasonBySession = new();
        if (rejectedSessionIds.Count > 0)
        {
            var lecturerRows = await _unitOfWork.Context.QaMessages
                .AsNoTracking()
                .Where(m =>
                    rejectedSessionIds.Contains(m.SessionId) &&
                    (m.Role == "Lecturer" || m.Role == "Expert"))
                .Select(m => new { m.SessionId, m.Content, m.CreatedAt, m.Id })
                .ToListAsync(cancellationToken);
            rejectionReasonBySession = lecturerRows
                .GroupBy(x => x.SessionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).First().Content.Trim());
        }

        foreach (var s in sessions)
        {
            var raw = (firstQuestionsBySession.TryGetValue(s.Id, out var firstQuestion) ? firstQuestion : null)?.Trim() ?? string.Empty;
            string? snippet = null;
            if (raw.Length > 0)
            {
                snippet = raw.Length <= snippetMax
                    ? raw
                    : raw[..snippetMax].TrimEnd() + "…";
            }

            list.Add(new VisualQaSessionHistoryItemDto
            {
                SessionId = s.Id,
                CaseId = s.CaseId,
                Status = s.Status,
                UpdatedAt = s.UpdatedAt ?? s.CreatedAt,
                QuestionSnippet = snippet,
                ImageUrl = await ResolveStudentVisibleVisualQaImageUrlAsync(
                    ResolveVisualQaSessionRawImageUrl(s),
                    cancellationToken),
                ReviewState = MapReviewState(s.Status),
                LastResponderRole = MapResponderRole(lastResponderBySession.TryGetValue(s.Id, out var lastRole) ? lastRole : null),
                RejectionReason = string.Equals(s.Status, "Rejected", StringComparison.Ordinal) && rejectionReasonBySession.TryGetValue(s.Id, out var rr)
                    ? rr
                    : null
            });
        }

        return new PagedResultDto<VisualQaSessionHistoryItemDto>
        {
            TotalCount = totalCount,
            Items = list
        };
    }

    public async Task<VisualQaThreadDto?> GetVisualQaThreadAsync(
        Guid studentId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .Include(s => s.Image)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.StudentId == studentId, cancellationToken);
        if (session == null)
            return null;

        var messages = await _unitOfWork.Context.QaMessages
            .AsNoTracking()
            .Include(m => m.Citations)
                .ThenInclude(c => c.Chunk)
                    .ThenInclude(ch => ch.Doc)
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        var turns = VisualQaSessionTurnsMapper.BuildTurns(sessionId, messages, session.Status, session.RequestedReviewMessageId)
            .ToList();
        var reviewState = MapReviewState(session.Status);

        var capabilities = await GetVisualQaSessionCapabilitiesAsync(studentId, sessionId, cancellationToken: cancellationToken);
        var blockingNotice = BuildBlockingNotice(capabilities.Reason);

        var sessionImageRaw = ResolveVisualQaSessionRawImageUrl(session);
        var sessionImageUrl = await ResolveStudentVisibleVisualQaImageUrlAsync(sessionImageRaw, cancellationToken);

        var latestUserWithRoi = messages
            .Where(m => string.Equals(m.Role, "User", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(m.Coordinates))
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();

        return new VisualQaThreadDto
        {
            SessionId = sessionId,
            SessionImageUrl = sessionImageUrl,
            ImageUrl = sessionImageUrl,
            StudyImageUrl = sessionImageUrl,
            RoiBoundingBox = latestUserWithRoi?.Coordinates,
            CaseId = session.CaseId,
            ImageId = session.ImageId,
            Turns = turns,
            Capabilities = capabilities,
            ReviewState = reviewState,
            LastResponderRole = ResolveLastResponderRole(messages, capabilities.Reason),
            BlockingNotice = blockingNotice
        };
    }

    private async Task<string?> ResolveStudentVisibleVisualQaImageUrlAsync(string? rawImagePathOrUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawImagePathOrUrl))
            return null;

        var value = rawImagePathOrUrl.Trim();

        // Public objects can be returned directly.
        if (value.Contains("/storage/v1/object/public/", StringComparison.OrdinalIgnoreCase))
            return value;

        // For private object URLs/paths, return short-lived signed URL.
        if (value.Contains("/storage/v1/object/", StringComparison.OrdinalIgnoreCase) ||
            !Uri.IsWellFormedUriString(value, UriKind.Absolute))
        {
            var signed = await _storageService.CreateSignedUrlAsync(value, duration: 3600, cancellationToken);
            if (!string.IsNullOrWhiteSpace(signed))
                return signed;
        }

        return value;
    }

    /// <summary>Raw storage URL/path for the session study (before signing for student clients).</summary>
    private static string? ResolveVisualQaSessionRawImageUrl(VisualQASession? session)
    {
        if (session == null)
            return null;
        if (!string.IsNullOrWhiteSpace(session.CustomImageUrl))
            return session.CustomImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(session.Image?.ImageUrl))
            return session.Image.ImageUrl.Trim();
        return session.Case?.MedicalImages?
            .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
            .ThenBy(m => m.Id)
            .Select(m => m.ImageUrl)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
    }

    private static IReadOnlyList<CitationItemDto> ResolveMessageCitations(QAMessage? message)
    {
        if (message == null)
            return Array.Empty<CitationItemDto>();

        var fromJson = VisualQaCitationMetadataBuilder.DeserializeMany(message.CitationsJson);
        if (fromJson.Count > 0)
            return fromJson
                .Take(5)
                .ToList();

        return MapTurnCitations(message.Citations);
    }

    private static IReadOnlyList<CitationItemDto> MapTurnCitations(ICollection<Citation>? citations)
    {
        if (citations == null || citations.Count == 0)
            return Array.Empty<CitationItemDto>();

        return citations
            .OrderBy(c => c.Chunk?.ChunkOrder ?? int.MaxValue)
            .ThenBy(c => c.Id)
            .Select(c => VisualQaCitationMetadataBuilder.FromDocumentChunk(c.Chunk))
            .Take(5)
            .ToList();
    }

    private static IReadOnlyList<string> SplitMultilineField(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('-', '*').Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static IReadOnlyList<string> DeserializeJsonArrayToList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json);
            if (parsed == null || parsed.Count == 0)
                return Array.Empty<string>();

            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
        }
        catch
        {
            return SplitMultilineField(json);
        }
    }

    private static string? ResolveLastResponderRole(IEnumerable<QAMessage> messages, string? blockingReason = null)
    {
        var last = messages
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();
        return last == null && !string.IsNullOrWhiteSpace(blockingReason)
            ? "system"
            : MapResponderRole(last?.Role);
    }

    private static string? MapReviewState(string? status)
    {
        return status switch
        {
            "PendingExpertReview" => "pending",
            "EscalatedToExpert" => "escalated",
            "LecturerApproved" => "reviewed",
            "ExpertApproved" => "resolved",
            "Rejected" => "rejected",
            _ => "none"
        };
    }

    private const string GeminiNoContextAnswer =
        "The current medical data does not contain enough information to answer this question.";
    private const string GeminiFallbackNoReliableInfoAnswer =
        "Sorry, based on our musculoskeletal medical knowledge base, I could not find sufficiently reliable information to answer this advanced question.";

    private static string DetermineResponseKind(QAMessage? assistantMessage)
    {
        if (assistantMessage == null)
            return "clarification";

        if (string.Equals(assistantMessage.Role, "Lecturer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assistantMessage.Role, "Expert", StringComparison.OrdinalIgnoreCase))
            return "review_update";

        var diagnosis = (assistantMessage.SuggestedDiagnosis ?? assistantMessage.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(diagnosis))
            return "clarification";

        if (diagnosis.Contains("not related to the musculoskeletal medical domain", StringComparison.OrdinalIgnoreCase) ||
            diagnosis.Contains("not valid medical data", StringComparison.OrdinalIgnoreCase) ||
            diagnosis.Contains("not a valid human bone x-ray image", StringComparison.OrdinalIgnoreCase) ||
            diagnosis.Contains("please upload a proper medical x-ray image", StringComparison.OrdinalIgnoreCase))
            return "refusal";

        if (string.Equals(diagnosis, GeminiNoContextAnswer, StringComparison.Ordinal) ||
            string.Equals(diagnosis, GeminiFallbackNoReliableInfoAnswer, StringComparison.Ordinal))
            return "clarification";

        var findings = SplitMultilineField(assistantMessage.KeyImagingFindings);
        var differentialDiagnoses = DeserializeJsonArrayToList(assistantMessage.DifferentialDiagnoses);
        var reflectiveQuestions = SplitMultilineField(assistantMessage.ReflectiveQuestions);

        return findings.Count == 0 && differentialDiagnoses.Count == 0 && reflectiveQuestions.Count == 0
            ? "clarification"
            : "analysis";
    }

    private static string? DeterminePolicyReason(QAMessage? assistantMessage)
    {
        if (assistantMessage == null)
            return "clarification";

        var responseKind = DetermineResponseKind(assistantMessage);
        if (string.Equals(responseKind, "refusal", StringComparison.Ordinal))
            return "off_topic";

        if (string.Equals(responseKind, "review_update", StringComparison.Ordinal))
            return "review_update";

        return "medical_intent";
    }

    private static string? MapResponderRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        return role.Trim() switch
        {
            "Assistant" => "assistant",
            "Lecturer" => "lecturer",
            "Expert" => "expert",
            _ => "system"
        };
    }

    private static string? BuildBlockingNotice(string? reason)
    {
        return reason switch
        {
            "TURN_LIMIT_EXCEEDED" => "You have used all question turns for this Visual QA session.",
            "SESSION_EXPIRED" => "This Visual QA session expired after 24 hours of inactivity.",
            "SESSION_READ_ONLY" => "This session is locked. You cannot send new questions.",
            _ => null
        };
    }

    private async Task<int> CountBillableUserTurnsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var messages = await _unitOfWork.Context.QaMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .Where(m => m.Role == "User" || m.Role == "Assistant")
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        var count = 0;
        QAMessage? pendingUser = null;
        foreach (var message in messages)
        {
            if (string.Equals(message.Role, "User", StringComparison.OrdinalIgnoreCase))
            {
                pendingUser = message;
                continue;
            }

            if (pendingUser == null)
                continue;

            var responseKind = DetermineResponseKind(message);
            if (string.Equals(responseKind, "analysis", StringComparison.Ordinal))
                count++;

            pendingUser = null;
        }

        return count;
    }

    private static string? NormalizeClientRequestId(string? clientRequestId)
    {
        if (string.IsNullOrWhiteSpace(clientRequestId))
            return null;

        var normalized = clientRequestId.Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
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

        var results = new List<StudentAnnouncementDto>();
        foreach (var a in announcements)
        {
            var dto = new StudentAnnouncementDto
            {
                Id = a.Id,
                ClassId = a.ClassId,
                ClassName = a.Class?.ClassName,
                Title = a.Title,
                Content = a.Content,
                CreatedAt = a.CreatedAt
            };

            // Get related assignment info if present
            if (a.AssignmentId.HasValue)
            {
                dto.RelatedAssignment = await GetRelatedAssignmentInfoForStudentAsync(a.AssignmentId);
            }

            results.Add(dto);
        }

        return results;
    }

    /// <summary>
    /// Get assignment info for student announcement response (case or quiz).
    /// </summary>
    private async Task<Models.Student.AnnouncementAssignmentInfoDto?> GetRelatedAssignmentInfoForStudentAsync(Guid? assignmentId)
    {
        if (!assignmentId.HasValue)
            return null;

        // Check if it's a ClassCase
        var classCase = await _unitOfWork.Context.ClassCases
            .Include(cc => cc.Case)
            .FirstOrDefaultAsync(cc => cc.CaseId == assignmentId.Value);
        if (classCase != null)
        {
            return new Models.Student.AnnouncementAssignmentInfoDto
            {
                AssignmentId = classCase.CaseId,
                AssignmentTitle = classCase.Case?.Title ?? "Case Assignment",
                AssignmentType = "case"
            };
        }

        // Check if it's a ClassQuizSession
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .Include(qs => qs.Quiz)
            .FirstOrDefaultAsync(qs => qs.Id == assignmentId.Value);
        if (quizSession != null)
        {
            return new Models.Student.AnnouncementAssignmentInfoDto
            {
                AssignmentId = quizSession.Id,
                AssignmentTitle = quizSession.Quiz?.Title ?? "Quiz Assignment",
                AssignmentType = "quiz"
            };
        }

        return null;
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

        // Lấy CreatedAt từ bảng quizzes
        var quizzesWithCreatedAt = await _unitOfWork.Context.Quizzes
            .Where(q => quizIds.Contains(q.Id))
            .Select(q => new { q.Id, q.CreatedAt })
            .ToDictionaryAsync(x => x.Id, x => x.CreatedAt);

        return sessions.Select(s =>
        {
            var attempt = attempts.FirstOrDefault(a => a.QuizId == s.QuizId);
            quizzesWithCreatedAt.TryGetValue(s.QuizId, out var createdAt);
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
                AttemptId = attempt?.Id,
                CreatedAt = createdAt
            };
        })
        .OrderByDescending(q => q.CreatedAt.HasValue)  // Items with CreatedAt come first
        .ThenByDescending(q => q.CreatedAt)            // Within those, sort by date descending
        .ToList();
    }


    public async Task<QuizSessionDto> StartQuizAsync(Guid studentId, Guid quizId)
    {
        // Auto-submit expired quizzes before starting a new quiz
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
                "You are not assigned this quiz through an enrolled class, or the quiz is outside its availability window.");
        }

        // Fetch session to check allow_retake BEFORE processing retake
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

        // Check availability window and return clear error
        var effectiveOpenTime = classSession?.OpenTime ?? quiz.OpenTime;
        var effectiveCloseTime = classSession?.CloseTime ?? quiz.CloseTime;

        if (effectiveOpenTime.HasValue && effectiveOpenTime.Value > utcNow)
        {
            throw new InvalidOperationException(
                $"Quiz is not open yet. Open time: {effectiveOpenTime.Value:dd/MM/yyyy HH:mm} (Vietnam time).");
        }

        if (effectiveCloseTime.HasValue && effectiveCloseTime.Value <= utcNow)
        {
            throw new InvalidOperationException(
                "Quiz is closed. You cannot start or continue this attempt.");
        }

        var existingAttempt = await _studentRepository.GetQuizAttemptAsync(studentId, quizId);
        QuizAttempt attempt;

        if (existingAttempt != null)
        {
            if (existingAttempt.CompletedAt.HasValue)
            {
                // Check retake eligibility: global flag OR lecturer-specific reset
                var globalRetake = classSession?.AllowRetake ?? false;
                var lecturerRetake = classSession?.RetakeResetAt > existingAttempt.CompletedAt;
                if (!globalRetake && !lecturerRetake)
                {
                    throw new InvalidOperationException(
                        "You have already submitted this quiz. Your lecturer will enable retake when needed.");
                }

                // Retake allowed: clear previous answers and reset
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
            ?? throw new KeyNotFoundException("Quiz attempt not found.");

        var question = await _unitOfWork.QuizQuestionRepository
            .GetByIdAsync(submit.QuestionId)
            ?? throw new KeyNotFoundException("Question not found.");

        var quiz = await _unitOfWork.QuizRepository
            .GetByIdAsync(attempt.QuizId)
            ?? throw new KeyNotFoundException("Quiz not found.");

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
            throw new InvalidOperationException("Quiz attempt time has expired.");

        var existing = await _unitOfWork.StudentQuizAnswerRepository
            .FirstOrDefaultAsync(a => a.AttemptId == submit.AttemptId
                                   && a.QuestionId == submit.QuestionId);
        if (existing != null)
            throw new InvalidOperationException("This question has already been answered.");

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

    /// Return the list of classes the student has enrolled in.
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

        // Get the number of announcements, quizzes, and cases for each class
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
        // not only quizzes with attempts (students must still see assigned quizzes they have not started).
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

        // Announcements with related assignment info
        var announcementsRaw = await _unitOfWork.Context.Announcements
            .AsNoTracking()
            .Where(a => a.ClassId == classId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var announcements = new List<ClassAnnouncementDto>();
        foreach (var a in announcementsRaw)
        {
            var dto = new ClassAnnouncementDto
            {
                Id = a.Id,
                Title = a.Title,
                Content = a.Content,
                CreatedAt = a.CreatedAt,
            };

            if (a.AssignmentId.HasValue)
            {
                dto.RelatedAssignment = await GetRelatedAssignmentInfoForStudentAsync(a.AssignmentId);
            }

            announcements.Add(dto);
        }

        // Assigned cases for this class (from ClassCases table)
        var assignedCasesRaw = await _unitOfWork.Context.ClassCases
            .AsNoTracking()
            .Where(cc => cc.ClassId == classId)
            .OrderByDescending(cc => cc.AssignedAt)
            .ToListAsync();

        var assignedCaseIds = assignedCasesRaw.Select(cc => cc.CaseId).ToList();
        var casesLookup = await _unitOfWork.Context.MedicalCases
            .AsNoTracking()
            .Where(c => assignedCaseIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Title);

        var assignedCases = assignedCasesRaw
            .Select(cc => new StudentCaseAssignmentDto
            {
                CaseId = cc.CaseId,
                Title = casesLookup.GetValueOrDefault(cc.CaseId, "Unknown Case"),
                DueDate = cc.DueDate,
                IsMandatory = cc.IsMandatory
            })
            .ToList();

        return new StudentClassDetailDto
        {
            ClassId = cls.Id,
            ClassName = cls.ClassName,
            Semester = cls.Semester,
            LecturerId = cls.LecturerId,
            LecturerName = cls.Lecturer?.FullName,
            ExpertId = cls.ExpertId,
            ExpertName = cls.Expert?.FullName,
            ExpertEmail = cls.Expert?.Email,
            ExpertAvatarUrl = cls.Expert?.AvatarUrl,
            EnrolledAt = enrollment.EnrolledAt,
            AssignedCases = assignedCases,
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
            ?? throw new KeyNotFoundException("Student not found.");

        var quiz = await _unitOfWork.Context.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quizId)
            ?? throw new KeyNotFoundException("Quiz not found.");

        var classSession = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Class)
            .Where(cqs => cqs.QuizId == quizId)
            .Where(cqs => cqs.Class != null)
            .ToListAsync();

        if (classSession.Count == 0)
            throw new InvalidOperationException("This quiz is not assigned through a class.");

        // Group sessions by LecturerId — each lecturer gets ONE notification only (avoids duplicates)
        var lecturerGroups = classSession
            .Where(s => s.Class!.LecturerId != null)
            .GroupBy(s => s.Class!.LecturerId!.Value);

        foreach (var group in lecturerGroups)
        {
            var lecturerId = group.Key;
            var classNames = group.Select(s => s.Class!.ClassName).Distinct().ToList();
            var lecturer = await _unitOfWork.Context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == lecturerId);

            if (lecturer == null) continue;

            // Skip if this lecturer already received a retake_request notification for this quiz from this student
            var existingNotif = await _unitOfWork.Context.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.UserId == lecturerId
                    && n.Type == "retake_request"
                    && n.Message != null
                    && n.Message.Contains(studentId.ToString())
                    && n.Message.Contains(quizId.ToString()));
            if (existingNotif) continue;

            var classListText = classNames.Count == 1
                ? $"\"{classNames[0]}\""
                : $"classes [{string.Join(", ", classNames.Select(c => $"\"{c}\""))}]";

            var notifTitle = $"Retake Request: {quiz.Title}";
            var notifMsg = $"Student \"{student.FullName}\" ({studentId}) requested a retake for quiz \"{quiz.Title}\" ({quizId}) in {classListText}.";

            // Notification (SignalR real-time)
            await _notificationService.SendNotificationToUserAsync(
                lecturerId,
                notifTitle,
                notifMsg,
                "retake_request",
                $"/lecturer/quizzes/{quizId}/results"
            );

            // Email (background, non-blocking on failure)
            try
            {
                foreach (var className in classNames)
                {
                    await _emailService.SendRetakeRequestEmailAsync(
                        lecturer.Email,
                        student.FullName,
                        quiz.Title,
                        className,
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
