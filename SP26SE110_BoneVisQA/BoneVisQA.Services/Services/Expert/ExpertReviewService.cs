using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.VisualQA;
using BoneVisQA.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.Expert;

public class ExpertReviewService : IExpertReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IRagExpertAnswerIndexingSignal _ragExpertAnswerIndexingSignal;
    private readonly IPythonAiConnectorService _pythonAiConnector;
    private readonly ILogger<ExpertReviewService> _logger;

    public static IQueryable<VisualQASession> QueryExpertScopedReviewQueue(
        IUnitOfWork uow,
        Guid expertId,
        Guid? specialtyId = null,
        string? statusFilter = null)
    {
        var statuses = ExpertReviewStatusFilter.ResolveVisualQaStatuses(statusFilter);
        var pendingQueue = ExpertReviewStatusFilter.IsPendingQueueFilter(statuses);

        var expertSpecialtyIds = uow.Context.Users
            .Where(u => u.Id == expertId && u.PrimaryBoneSpecialtyId.HasValue)
            .Select(u => u.PrimaryBoneSpecialtyId!.Value);

        var query = uow.Context.VisualQaSessions
            .AsNoTracking()
            .Where(s => statuses.Contains(s.Status));

        if (pendingQueue)
        {
            query = query.Where(s =>
                s.ExpertId == expertId ||
                (s.TargetBoneSpecialtyId.HasValue && expertSpecialtyIds.Contains(s.TargetBoneSpecialtyId.Value)) ||
                (!s.ExpertId.HasValue && !s.TargetBoneSpecialtyId.HasValue &&
                    uow.Context.ClassEnrollments.Any(e =>
                        e.StudentId == s.StudentId &&
                        e.Class != null &&
                        e.Class.ExpertId == expertId)));
        }
        else
        {
            query = query.Where(s =>
                s.ExpertId == expertId ||
                s.ExpertReviews.Any(r => r.ExpertId == expertId && r.Action != null));
        }

        if (specialtyId.HasValue)
            query = query.Where(s => s.TargetBoneSpecialtyId == specialtyId.Value);

        return query;
    }

    public static IQueryable<VisualQASession> QueryExpertScopedEscalatedQueue(IUnitOfWork uow, Guid expertId, Guid? specialtyId = null) =>
        QueryExpertScopedReviewQueue(uow, expertId, specialtyId);

    private async Task<bool> ExpertMayActOnVisualSessionAsync(
        Guid expertId,
        Guid studentId,
        Guid? sessionExpertId,
        Guid? sessionTargetBoneSpecialtyId,
        CancellationToken cancellationToken = default)
    {
        if (sessionExpertId == expertId)
            return true;

        if (sessionTargetBoneSpecialtyId.HasValue
            && await ExpertMatchesSpecialtyAsync(expertId, sessionTargetBoneSpecialtyId.Value, cancellationToken))
        {
            return true;
        }

        return await _unitOfWork.Context.ClassEnrollments
            .AnyAsync(e =>
                e.StudentId == studentId &&
                e.Class != null &&
                e.Class.ExpertId == expertId,
                cancellationToken);
    }

    public ExpertReviewService(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IRagExpertAnswerIndexingSignal ragExpertAnswerIndexingSignal,
        IPythonAiConnectorService pythonAiConnector,
        ILogger<ExpertReviewService> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _ragExpertAnswerIndexingSignal = ragExpertAnswerIndexingSignal;
        _pythonAiConnector = pythonAiConnector;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetEscalatedAnswersAsync(
        Guid expertId,
        Guid? specialtyId = null,
        string? status = null)
    {
        var sessions = await QueryExpertScopedReviewQueue(_unitOfWork, expertId, specialtyId, status)
            .AsSplitQuery()
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Case!)
                .ThenInclude(c => c.CaseMedia)
            .Include(s => s.Image)
            .Include(s => s.Messages)
                .ThenInclude(m => m.Citations)
                    .ThenInclude(c => c.Chunk)
                        .ThenInclude(ch => ch.Doc)
            .Include(s => s.ExpertReviews)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToListAsync();

        var studentIds = sessions.Select(s => s.StudentId).Distinct().ToList();
        var enrollmentRows = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Class)
            .Where(e => studentIds.Contains(e.StudentId) && e.Class != null)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        var enrollmentByStudent = enrollmentRows
            .GroupBy(e => e.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        return sessions.Select(s =>
        {
            enrollmentByStudent.TryGetValue(s.StudentId, out var enrollment);
            var review = s.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId);
            var orderedMessages = s.Messages
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .ToList();
            var turns = VisualQaSessionTurnsMapper.BuildTurns(s.Id, orderedMessages, s.Status, s.RequestedReviewMessageId);
            var (userMessage, latestAssistant) = ResolveRequestedReviewPair(s, orderedMessages);
            var dicomMetadata = CaseMediaDicomMetadataHelper.ResolveFirstMetadata(s.Case);

            return MapEscalatedAnswerDto(
                s,
                userMessage,
                latestAssistant,
                review,
                enrollment,
                turns,
                dicomMetadata);
        }).ToList();
    }

    public Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetCaseAnswersAsync(Guid expertId, Guid? specialtyId = null, string? status = null)
        => GetEscalatedAnswersAsync(expertId, specialtyId, status);

    public async Task<ExpertEscalatedAnswerDto> GetEscalatedSessionDetailAsync(Guid expertId, Guid sessionId)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Case!)
                .ThenInclude(c => c.CaseMedia)
            .Include(s => s.Image)
            .Include(s => s.Messages)
                .ThenInclude(m => m.Citations)
                    .ThenInclude(c => c.Chunk)
                        .ThenInclude(ch => ch.Doc)
            .Include(s => s.ExpertReviews)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        if (!await ExpertMayActOnVisualSessionAsync(expertId, session.StudentId, session.ExpertId, session.TargetBoneSpecialtyId))
            throw new InvalidOperationException("The expert does not have permission to view this Q&A session.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Class)
            .Where(e => e.StudentId == session.StudentId && e.Class != null)
            .OrderByDescending(e => e.EnrolledAt)
            .FirstOrDefaultAsync();

        var review = session.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId);
        var orderedMessages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();
        var turns = VisualQaSessionTurnsMapper.BuildTurns(session.Id, orderedMessages, session.Status, session.RequestedReviewMessageId);
        var (userMessage, latestAssistant) = ResolveRequestedReviewPair(session, orderedMessages);
        var dicomMetadata = CaseMediaDicomMetadataHelper.ResolveFirstMetadata(session.Case);

        return MapEscalatedAnswerDto(
            session,
            userMessage,
            latestAssistant,
            review,
            enrollment,
            turns,
            dicomMetadata);
    }

    public async Task<ExpertVisualSessionDraftResponseDto> UpsertSessionReviewDraftAsync(Guid expertId, Guid sessionId, ExpertVisualSessionDraftRequestDto request)
    {
        if (request == null)
            throw new InvalidOperationException("Request body is required.");

        if (string.IsNullOrWhiteSpace(request.ReviewNote)
            && (request.CorrectedRoiBoundingBox == null || request.CorrectedRoiBoundingBox.Length == 0))
        {
            throw new InvalidOperationException("At least one of reviewNote or correctedRoiBoundingBox is required.");
        }

        var session = await _unitOfWork.Context.VisualQaSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        if (!string.Equals(session.Status, CaseAnswerStatuses.EscalatedToExpert, StringComparison.Ordinal))
            throw new ConflictException("Draft can only be saved while the session is escalated to an expert.");

        if (!await ExpertMayActOnVisualSessionAsync(expertId, session.StudentId, session.ExpertId, session.TargetBoneSpecialtyId))
            throw new InvalidOperationException("The expert does not have permission to edit this session draft.");

        var review = await _unitOfWork.Context.ExpertReviews
            .FirstOrDefaultAsync(r => r.SessionId == sessionId && r.ExpertId == expertId);

        if (review != null && IsFinalizedExpertReviewAction(review.Action))
            throw new ConflictException("This session review has already been submitted.");

        var roiJson = SerializeCorrectedRoi(request.CorrectedRoiBoundingBox);
        var now = DateTime.UtcNow;

        if (review == null)
        {
            review = new ExpertReview
            {
                Id = Guid.NewGuid(),
                ExpertId = expertId,
                AnswerId = null,
                SessionId = sessionId,
                ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote) ? null : request.ReviewNote.Trim(),
                Action = null,
                CorrectedRoi = roiJson,
                CreatedAt = now
            };
            await _unitOfWork.Context.ExpertReviews.AddAsync(review);
        }
        else
        {
            review.ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote) ? null : request.ReviewNote.Trim();
            review.CorrectedRoi = roiJson;
            _unitOfWork.Context.ExpertReviews.Update(review);
        }

        await _unitOfWork.SaveAsync();

        return new ExpertVisualSessionDraftResponseDto
        {
            SessionId = sessionId,
            ReviewRowId = review.Id,
            ReviewNote = review.ReviewNote,
            ExpertCorrectedRoiBoundingBox = DeserializeCorrectedRoi(review.CorrectedRoi)
        };
    }

    public async Task DeleteSessionReviewDraftAsync(Guid expertId, Guid sessionId)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        if (!await ExpertMayActOnVisualSessionAsync(expertId, session.StudentId, session.ExpertId, session.TargetBoneSpecialtyId))
            throw new InvalidOperationException("The expert does not have permission to delete this session draft.");

        var review = await _unitOfWork.Context.ExpertReviews
            .FirstOrDefaultAsync(r => r.SessionId == sessionId && r.ExpertId == expertId);

        if (review == null)
            return;

        if (IsFinalizedExpertReviewAction(review.Action))
            throw new ConflictException("Cannot delete a submitted expert review.");

        _unitOfWork.Context.ExpertReviews.Remove(review);
        await _unitOfWork.SaveAsync();
    }

    private async Task RemoveStaleDraftExpertReviewsAsync(Guid sessionId, Guid expertId)
    {
        var drafts = await _unitOfWork.Context.ExpertReviews
            .Where(r => r.SessionId == sessionId && r.ExpertId == expertId && r.Action == null)
            .ToListAsync();
        foreach (var d in drafts)
            _unitOfWork.Context.ExpertReviews.Remove(d);
        if (drafts.Count > 0)
            await _unitOfWork.SaveAsync();
    }

    private static bool IsFinalizedExpertReviewAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return false;
        return string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase)
               || string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase)
               || string.Equals(action, "Edit", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ExpertEscalatedAnswerDto> RespondToSessionAsync(Guid expertId, Guid sessionId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Expert feedback content is required.");

        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Case!)
                .ThenInclude(c => c.CaseMedia)
            .Include(s => s.Image)
            .Include(s => s.Messages)
                .ThenInclude(m => m.Citations)
                    .ThenInclude(c => c.Chunk)
                        .ThenInclude(ch => ch.Doc)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        if (!await ExpertMayActOnVisualSessionAsync(expertId, session.StudentId, session.ExpertId, session.TargetBoneSpecialtyId))
            throw new InvalidOperationException("The expert does not have permission to respond to this Q&A session.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class != null);

        if (!CanTransitionFrom(session.Status, "Active"))
            throw new ConflictException($"Cannot respond to a session from status '{session.Status}'.");

        var now = DateTime.UtcNow;
        var expertMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "Expert",
            Content = content.Trim(),
            CreatedAt = now,
            TargetAssistantMessageId = session.RequestedReviewMessageId
        };

        await _unitOfWork.Context.QaMessages.AddAsync(expertMessage);
        session.Status = "Active";
        session.ExpertId = expertId;
        session.ReviewFeedback = content.Trim();
        session.UpdatedAt = now;
        await _unitOfWork.SaveAsync();

        var orderedMessages = session.Messages
            .Append(expertMessage)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();
        var userMessage = ResolveRequestedReviewQuestion(session, orderedMessages);
        if (userMessage == null)
            throw new InvalidOperationException("The selected student question could not be resolved for this review session.");
        if (session.RequestedReviewMessageId.HasValue &&
            !orderedMessages.Any(m => m.Role == "Assistant" && m.Id == session.RequestedReviewMessageId.Value))
        {
            throw new ConflictException("Cannot respond because the selected review assistant turn is inconsistent.");
        }

        var turnsAfter = VisualQaSessionTurnsMapper.BuildTurns(session.Id, orderedMessages, session.Status, session.RequestedReviewMessageId);
        var dicomMetadata = CaseMediaDicomMetadataHelper.ResolveFirstMetadata(session.Case);
        return new ExpertEscalatedAnswerDto
        {
            AnswerId = session.Id,
            SessionId = session.Id,
            QuestionId = userMessage?.Id ?? Guid.Empty,
            StudentId = session.StudentId,
            StudentName = session.Student?.FullName ?? string.Empty,
            StudentEmail = session.Student?.Email ?? string.Empty,
            CaseId = session.CaseId,
            CaseTitle = session.Case?.Title ?? string.Empty,
            QuestionText = userMessage?.Content ?? string.Empty,
            AnswerText = MapAssistantAnswerText(expertMessage),
            CurrentAnswerText = MapAssistantAnswerText(expertMessage),
            StructuredDiagnosis = null,
            DifferentialDiagnoses = null,
            KeyImagingFindings = null,
            ReflectiveQuestions = null,
            Status = session.Status,
            SessionStatus = session.Status,
            ReviewFeedback = session.ReviewFeedback,
            EscalatedById = session.LecturerId,
            EscalatedAt = session.UpdatedAt,
            AiConfidenceScore = null,
            ClassId = enrollment?.ClassId,
            ClassName = enrollment?.Class?.ClassName ?? string.Empty,
            ReviewNote = session.ReviewFeedback,
            PromotedCaseId = session.PromotedCaseId,
            Citations = MergeCitationsFromAssistantMessages(orderedMessages),
            ImageUrl = ResolveSessionImageUrl(session),
            CustomCoordinates = VisualQaRoiResolutionHelper.ResolvePreferredUserRoiJson(
                userMessage,
                session.RequestedReviewMessageId,
                turnsAfter),
            ExpertCorrectedRoiBoundingBox = DeserializeCorrectedRoi(
                session.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId)?.CorrectedRoi),
            RequestedReviewMessageId = session.RequestedReviewMessageId,
            SelectedUserMessageId = userMessage?.Id,
            SelectedAssistantMessageId = session.RequestedReviewMessageId,
            Turns = turnsAfter,
            DicomMetadata = dicomMetadata
        };
    }

    public async Task ApproveSessionAsync(Guid expertId, Guid sessionId)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        if (!await ExpertMayActOnVisualSessionAsync(expertId, session.StudentId, session.ExpertId, session.TargetBoneSpecialtyId))
            throw new InvalidOperationException("The expert does not have permission to approve this Q&A session.");

        if (session.PromotedCaseId.HasValue)
            return;

        if (!CanTransitionFrom(session.Status, "ExpertApproved"))
            throw new ConflictException($"Cannot approve a session from status '{session.Status}'.");

        session.Status = CaseAnswerStatuses.ExpertApproved;
        session.ExpertId = expertId;
        session.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveAsync();

        await TryIngestGoldenChunkAfterSimpleApproveAsync(sessionId);

        await _notificationService.SendNotificationToUserAsync(
            session.StudentId,
            "An expert approved your Visual QA session",
            "Your session has been reviewed by an expert. Open Visual QA to read the feedback.",
            "expert_review",
            $"/student/qa/image?sessionId={session.Id}");
    }

    public async Task<PromoteToLibraryResponseDto> ApproveAndPromoteToLibraryAsync(
        Guid expertId,
        Guid sessionId,
        ApproveAndPromoteToLibraryRequestDto request)
    {
        if (request == null)
            throw new InvalidOperationException("Request body is required.");

        var existingResponse = await TryBuildExistingPromoteResponseAsync(sessionId);
        if (existingResponse != null)
            return existingResponse;

        var now = DateTime.UtcNow;

        await using var transaction =
            await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var session = await LoadSessionForPromotionAsync(sessionId);

            if (!await ExpertMayActOnVisualSessionAsync(expertId, session.StudentId, session.ExpertId, session.TargetBoneSpecialtyId))
                throw new InvalidOperationException("The expert does not have permission to move this Q&A session to the library.");

            if (session.PromotedCaseId.HasValue)
            {
                await transaction.CommitAsync();
                return await BuildPromoteResponseAsync(session.PromotedCaseId.Value);
            }

            if (!string.Equals(session.Status, CaseAnswerStatuses.EscalatedToExpert, StringComparison.Ordinal)
                && !string.Equals(session.Status, CaseAnswerStatuses.ExpertApproved, StringComparison.Ordinal))
            {
                throw new ConflictException(
                    $"Cannot approve and promote a session from status '{session.Status}'.");
            }

            var expertReview = await UpsertExpertReviewForPromotionAsync(
                session,
                expertId,
                request.ReviewNote,
                request.CorrectedRoiBoundingBox,
                now,
                finalizeAction: true);

            session.Status = CaseAnswerStatuses.ExpertApproved;
            session.ExpertId = expertId;
            if (!string.IsNullOrWhiteSpace(request.ReviewNote))
                session.ReviewFeedback = VisualQaEducatorFeedbackHelper.SanitizeHumanFeedback(request.ReviewNote.Trim());
            session.UpdatedAt = now;

            var caseId = await CreatePromotedLibraryCaseAsync(
                session,
                expertId,
                request,
                expertReview,
                now);

            await _unitOfWork.SaveAsync();
            await transaction.CommitAsync();

            await SendPromoteSuccessNotificationsAsync(session, caseId);

            try
            {
                await TryIngestGoldenChunkAfterPromoteAsync(session, request);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Golden chunk ingest failed after approve-and-promote for session {SessionId}.", sessionId);
            }

            return await BuildPromoteResponseAsync(caseId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PromoteToLibraryResponseDto> PromoteToLibraryAsync(Guid expertId, Guid sessionId, PromoteToLibraryRequestDto request)
    {
        var existingResponse = await TryBuildExistingPromoteResponseAsync(sessionId);
        if (existingResponse != null)
            return existingResponse;

        var now = DateTime.UtcNow;

        await using var transaction =
            await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var session = await LoadSessionForPromotionAsync(sessionId);

            if (!await ExpertMayActOnVisualSessionAsync(expertId, session.StudentId, session.ExpertId, session.TargetBoneSpecialtyId))
                throw new InvalidOperationException("The expert does not have permission to move this Q&A session to the library.");

            if (session.PromotedCaseId.HasValue)
            {
                await transaction.CommitAsync();
                return await BuildPromoteResponseAsync(session.PromotedCaseId.Value);
            }

            if (!string.Equals(session.Status, CaseAnswerStatuses.ExpertApproved, StringComparison.Ordinal))
                throw new InvalidOperationException("This session can be moved to the library only after expert approval.");

            if (string.IsNullOrWhiteSpace(session.CustomImageUrl) || session.ImageId.HasValue)
                throw new InvalidOperationException("Only self-uploaded images can be added to the library.");

            var expertReview = session.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId);
            var caseId = await CreatePromotedLibraryCaseAsync(
                session,
                expertId,
                request,
                expertReview,
                now);

            await _unitOfWork.SaveAsync();
            await transaction.CommitAsync();

            try
            {
                await TryIngestGoldenChunkAfterPromoteAsync(session, request);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Golden chunk ingest failed after promote-to-library for session {SessionId}.", sessionId);
            }

            return await BuildPromoteResponseAsync(caseId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<VisualQASession> LoadSessionForPromotionAsync(Guid sessionId)
    {
        return await _unitOfWork.Context.VisualQaSessions
                   .Include(s => s.Case!).ThenInclude(c => c!.Category)
                   .Include(s => s.Case!).ThenInclude(c => c!.MedicalImages)
                   .Include(s => s.Case!).ThenInclude(c => c!.CaseMedia)
                   .Include(s => s.Case!).ThenInclude(c => c!.CaseMetadata)
                   .Include(s => s.Image)
                   .Include(s => s.Messages)
                   .Include(s => s.ExpertReviews)
                   .FirstOrDefaultAsync(s => s.Id == sessionId)
               ?? throw new KeyNotFoundException("Q&A session not found.");
    }

    private async Task<PromoteToLibraryResponseDto?> TryBuildExistingPromoteResponseAsync(Guid sessionId)
    {
        var promotedCaseId = await _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.PromotedCaseId.HasValue)
            .Select(s => s.PromotedCaseId)
            .FirstOrDefaultAsync();

        return promotedCaseId.HasValue
            ? await BuildPromoteResponseAsync(promotedCaseId.Value)
            : null;
    }

    private async Task<PromoteToLibraryResponseDto> BuildPromoteResponseAsync(Guid caseId)
    {
        var row = await _unitOfWork.Context.MedicalCases
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.CaseTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == caseId)
            ?? throw new KeyNotFoundException("Promoted medical case not found.");

        return new PromoteToLibraryResponseDto
        {
            PromotedCaseId = row.Id,
            CaseId = row.Id,
            Title = row.Title,
            Status = row.IsApproved == true ? "approved" : "pending",
            CaseOrigin = ExpertCaseOriginValues.FromStudentRequest,
            CategoryId = row.CategoryId,
            CategoryName = row.Category?.Name,
            Difficulty = row.Difficulty,
            TagNames = row.CaseTags
                .Where(ct => ct.Tag != null)
                .Select(ct => ct.Tag!.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private async Task<ExpertReview> UpsertExpertReviewForPromotionAsync(
        VisualQASession session,
        Guid expertId,
        string? reviewNote,
        double[]? correctedRoiBoundingBox,
        DateTime nowUtc,
        bool finalizeAction)
    {
        var review = session.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId);
        var roiJson = SerializeCorrectedRoi(correctedRoiBoundingBox);
        var note = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();

        if (review == null)
        {
            review = new ExpertReview
            {
                Id = Guid.NewGuid(),
                ExpertId = expertId,
                AnswerId = null,
                SessionId = session.Id,
                ReviewNote = note,
                Action = finalizeAction ? "Approve" : null,
                CorrectedRoi = roiJson,
                CreatedAt = nowUtc
            };
            await _unitOfWork.Context.ExpertReviews.AddAsync(review);
            session.ExpertReviews.Add(review);
        }
        else
        {
            if (finalizeAction && IsFinalizedExpertReviewAction(review.Action))
                throw new ConflictException("This Q&A session has already been processed.");

            if (!string.IsNullOrWhiteSpace(note))
                review.ReviewNote = note;
            if (correctedRoiBoundingBox != null)
                review.CorrectedRoi = roiJson;
            if (finalizeAction)
                review.Action = "Approve";
            _unitOfWork.Context.ExpertReviews.Update(review);
        }

        return review;
    }

    private async Task<Guid> CreatePromotedLibraryCaseAsync(
        VisualQASession session,
        Guid expertId,
        PromoteToLibraryRequestDto request,
        ExpertReview? expertReview,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(session.CustomImageUrl) || session.ImageId.HasValue)
            throw new InvalidOperationException("Only self-uploaded images can be added to the library.");

        var title = string.IsNullOrWhiteSpace(request.Title?.Trim())
            ? "Clinical case from the community"
            : request.Title.Trim();
        var normalizedDifficulty = MedicalCaseDifficultyNormalizer.Normalize(request.Difficulty);

        var orderedMessages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();
        var turns = VisualQaSessionTurnsMapper.BuildTurns(session.Id, orderedMessages, session.Status, session.RequestedReviewMessageId);
        var (targetUser, targetAssistant) = ResolveRequestedReviewPair(session, orderedMessages);
        var dicomMetadata = CaseMediaDicomMetadataHelper.ResolveFirstMetadata(session.Case);
        var sourceCaseMetadata = session.Case?.CaseMetadata;
        request = PromoteToLibraryRequestHydrator.Merge(
            request,
            session,
            orderedMessages,
            targetUser,
            targetAssistant,
            turns,
            dicomMetadata,
            sourceCaseMetadata);

        var validationErrors = PromoteToLibraryValidation.ValidateRequiredFields(request);
        if (validationErrors != null)
            throw new PromoteValidationException(validationErrors);

        LibraryPromotionQualityGate.ValidateOrThrow(request, session, orderedMessages, expertReview);

        var resolvedCategoryId = await ResolvePromoteCategoryIdAsync(request);

        var newCase = new MedicalCase
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = request.Description.Trim(),
            SuggestedDiagnosis = request.SuggestedDiagnosis.Trim(),
            KeyFindings = request.KeyFindings.Trim(),
            ReflectiveQuestions = request.ReflectiveQuestions.Trim(),
            Difficulty = normalizedDifficulty,
            CategoryId = resolvedCategoryId,
            IsApproved = true,
            IsActive = true,
            CreatedByExpertId = expertId,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            IndexingStatus = DocumentIndexingStatuses.Pending,
            Version = SemanticDocumentVersion.Initial,
            ReviewVersion = "1.0.0",
            ValidatedByUserId = expertId,
            ValidatedAt = nowUtc
        };

        await _unitOfWork.Context.MedicalCases.AddAsync(newCase);

        var image = new MedicalImage
        {
            Id = Guid.NewGuid(),
            CaseId = newCase.Id,
            ImageUrl = session.CustomImageUrl.Trim(),
            Modality = MedicalImageModalityNormalizer.Normalize(request.Modality),
            CreatedAt = nowUtc
        };
        await _unitOfWork.Context.MedicalImages.AddAsync(image);

        var roiItems = (request.TurnAnnotations ?? request.ImageAnnotations ?? Enumerable.Empty<PromoteCaseAnnotationDto>())
            .Where(a => a != null)
            .ToList();
        var annotationsCreated = 0;
        foreach (var ann in roiItems)
        {
            var coords = SerializePromoteCoordinates(ann!.Coordinates);
            var label = ResolvePromoteAnnotationLabel(ann.Label);
            if (string.IsNullOrWhiteSpace(coords) && string.IsNullOrWhiteSpace(ann.Label?.Trim()))
                continue;

            await _unitOfWork.Context.CaseAnnotations.AddAsync(new CaseAnnotation
            {
                Id = Guid.NewGuid(),
                ImageId = image.Id,
                Label = label,
                Coordinates = coords,
                CreatedAt = nowUtc
            });
            annotationsCreated++;
        }

        if (annotationsCreated == 0)
        {
            var autoCoords = ResolvePromoteRoiCoordinates(session, orderedMessages, expertReview, turns, targetUser);
            if (!string.IsNullOrWhiteSpace(autoCoords))
            {
                await _unitOfWork.Context.CaseAnnotations.AddAsync(new CaseAnnotation
                {
                    Id = Guid.NewGuid(),
                    ImageId = image.Id,
                    Label = "finding",
                    Coordinates = autoCoords,
                    CreatedAt = nowUtc
                });
            }
        }

        await CopySourceCaseMediaAsync(session.Case, newCase.Id, request.Modality, nowUtc);

        var sourceTagId = await GetOrCreateTagIdByNameAndTypeAsync("Student Q&A", "Source", nowUtc);
        await AddCaseTagIfMissingAsync(newCase.Id, sourceTagId, nowUtc);

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Student Q&A" };
        foreach (var tagId in request.TagIds ?? Enumerable.Empty<Guid>())
        {
            var tag = await _unitOfWork.Context.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tagId);
            if (tag == null)
                continue;

            await AddCaseTagIfMissingAsync(newCase.Id, tag.Id, nowUtc);
            seenNames.Add(tag.Name);
        }

        foreach (var raw in request.TagNames ?? Enumerable.Empty<string>())
        {
            var name = raw?.Trim();
            if (string.IsNullOrWhiteSpace(name) || !seenNames.Add(name))
                continue;

            var extraTagId = await GetOrCreateTagIdByNameAndTypeAsync(name, "Custom", nowUtc);
            await AddCaseTagIfMissingAsync(newCase.Id, extraTagId, nowUtc);
        }

        if (!string.IsNullOrWhiteSpace(request.AnatomySite))
        {
            var locationTagId = await GetOrCreateTagIdByNameAndTypeAsync(request.AnatomySite.Trim(), "Location", nowUtc);
            await AddCaseTagIfMissingAsync(newCase.Id, locationTagId, nowUtc);
        }

        if (!string.IsNullOrWhiteSpace(request.PathologyGroup))
        {
            var lesionTagId = await GetOrCreateTagIdByNameAndTypeAsync(request.PathologyGroup.Trim(), "Lesion Type", nowUtc);
            await AddCaseTagIfMissingAsync(newCase.Id, lesionTagId, nowUtc);
        }

        session.PromotedCaseId = newCase.Id;
        session.Status = CaseAnswerStatuses.ExpertApproved;

        await InsertCaseMetadataForPromotedLibraryCaseAsync(
            newCase.Id,
            request,
            session,
            targetUser,
            targetAssistant,
            nowUtc,
            normalizedDifficulty);

        return newCase.Id;
    }

    private async Task SendPromoteSuccessNotificationsAsync(VisualQASession session, Guid caseId)
    {
        await _notificationService.SendNotificationToUserAsync(
            session.StudentId,
            "Your Visual QA case was published to the library",
            "An expert approved your session and published it as a teaching case.",
            "expert_review",
            $"/student/qa/image?sessionId={session.Id}");

        await _ragExpertAnswerIndexingSignal.NotifyExpertApprovedForFutureIndexingAsync(session.Id);
    }

    private async Task InsertCaseMetadataForPromotedLibraryCaseAsync(
        Guid caseId,
        PromoteToLibraryRequestDto request,
        VisualQASession session,
        QAMessage? targetUser,
        QAMessage? targetAssistant,
        DateTime nowUtc,
        string normalizedDifficulty)
    {
        var anatomyRegion = session.Case?.Category?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(anatomyRegion))
            anatomyRegion = "Other";

        var references = BuildReferencesAndCitationsFromAssistant(targetAssistant);
        var clinical = new Dictionary<string, object?>
        {
            ["source"] = "bonevisqa-promote",
            ["clinical_evidence"] = request.ClinicalEvidence.Trim(),
            ["student_question"] = targetUser?.Content?.Trim(),
            ["differential_diagnoses"] = request.DifferentialDiagnoses
                .Select(s => s?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ["references_and_citations"] = references,
            ["promoted_from_session_id"] = session.Id,
            ["validated_at_utc"] = nowUtc
        };
        var json = JsonSerializer.Serialize(clinical);

        await _unitOfWork.Context.CaseMetadata.AddAsync(new CaseMetadata
        {
            CaseId = caseId,
            Modality = request.Modality.Trim(),
            Anatomy = anatomyRegion,
            AnatomySite = request.AnatomySite.Trim(),
            PathologyGroup = request.PathologyGroup.Trim(),
            Laterality = request.Laterality.Trim(),
            ViewPosition = request.ViewPosition.Trim(),
            Difficulty = normalizedDifficulty.Trim(),
            SourceType = request.SourceType.Trim(),
            QualityScore = (double)request.QualityScore,
            SuggestedDiagnosis = request.SuggestedDiagnosis.Trim(),
            ClinicalContext = json,
            CreatedAt = nowUtc
        });
    }

    private async Task<Guid?> ResolvePromoteCategoryIdAsync(PromoteToLibraryRequestDto request)
    {
        if (request.CategoryId is { } cid && cid != Guid.Empty)
        {
            return await _unitOfWork.Context.Categories.AnyAsync(c => c.Id == cid)
                ? cid
                : null;
        }

        if (string.IsNullOrWhiteSpace(request.CategoryName))
            return null;

        var trimmed = request.CategoryName.Trim();
        var cat = await _unitOfWork.Context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => EF.Functions.ILike(c.Name, trimmed));
        return cat?.Id;
    }

    private static string? SerializePromoteCoordinates(JsonElement? element)
    {
        if (!element.HasValue)
            return null;

        var el = element.Value;
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        var raw = el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : el.GetRawText();

        return BoundingBoxParser.NormalizeCoordinatesJson(raw) ?? raw;
    }

    private static string ResolvePromoteAnnotationLabel(string? label) =>
        string.IsNullOrWhiteSpace(label) ? "finding" : label.Trim();

    private static string? ResolvePromoteRoiCoordinates(
        VisualQASession session,
        IReadOnlyList<QAMessage> orderedMessages,
        ExpertReview? expertReview,
        IReadOnlyList<VisualQaTurnDto> turns,
        QAMessage? targetUser)
    {
        if (!string.IsNullOrWhiteSpace(expertReview?.CorrectedRoi))
        {
            try
            {
                var arr = JsonSerializer.Deserialize<double[]>(expertReview.CorrectedRoi);
                if (arr is { Length: >= 4 })
                {
                    var fake = $"{{\"x\":{arr[0]},\"y\":{arr[1]},\"width\":{arr[2]},\"height\":{arr[3]}}}";
                    var normalized = BoundingBoxParser.NormalizeCoordinatesJson(fake);
                    if (normalized != null)
                        return normalized;
                }
            }
            catch
            {
                // Fall through to user ROI.
            }
        }

        var roiJson = VisualQaRoiResolutionHelper.ResolvePreferredUserRoiJson(
            targetUser,
            session.RequestedReviewMessageId,
            turns);
        return BoundingBoxParser.NormalizeCoordinatesJson(roiJson) ?? roiJson;
    }

    private async Task CopySourceCaseMediaAsync(MedicalCase? sourceCase, Guid targetCaseId, string modality, DateTime nowUtc)
    {
        if (sourceCase?.CaseMedia == null || sourceCase.CaseMedia.Count == 0)
            return;

        foreach (var sourceMedia in sourceCase.CaseMedia.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id))
        {
            await _unitOfWork.Context.CaseMedia.AddAsync(new CaseMedia
            {
                Id = Guid.NewGuid(),
                CaseId = targetCaseId,
                MediaUrl = sourceMedia.MediaUrl,
                StoragePath = sourceMedia.StoragePath,
                MediaType = sourceMedia.MediaType,
                Modality = string.IsNullOrWhiteSpace(sourceMedia.Modality)
                    ? MedicalImageModalityNormalizer.Normalize(modality)
                    : sourceMedia.Modality,
                Anatomy = sourceMedia.Anatomy,
                DicomMetadata = sourceMedia.DicomMetadata,
                CreatedAt = nowUtc,
            });
        }
    }

    private async Task<Guid> GetOrCreateTagIdByNameAndTypeAsync(string name, string type, DateTime now)
    {
        var existing = await _unitOfWork.Context.Tags
            .FirstOrDefaultAsync(t => t.Name == name && t.Type == type);
        if (existing != null)
            return existing.Id;

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _unitOfWork.Context.Tags.AddAsync(tag);
        return tag.Id;
    }

    private async Task AddCaseTagIfMissingAsync(Guid caseId, Guid tagId, DateTime now)
    {
        var exists = await _unitOfWork.Context.CaseTags.AnyAsync(ct => ct.CaseId == caseId && ct.TagId == tagId);
        if (exists)
            return;

        await _unitOfWork.Context.CaseTags.AddAsync(new CaseTag
        {
            CaseId = caseId,
            TagId = tagId,
            CreatedAt = now
        });
    }

    public async Task<ExpertEscalatedAnswerDto> ResolveEscalatedAnswerAsync(Guid expertId, Guid sessionId, ResolveEscalatedAnswerRequestDto request)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Case!)
                .ThenInclude(c => c.CaseMedia)
            .Include(s => s.Image)
            .Include(s => s.Messages)
                .ThenInclude(m => m.Citations)
                    .ThenInclude(c => c.Chunk)
                        .ThenInclude(ch => ch.Doc)
            .Include(s => s.ExpertReviews)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("The Q&A session to process was not found.");

        if (!await ExpertMayActOnVisualSessionAsync(expertId, session.StudentId, session.ExpertId, session.TargetBoneSpecialtyId))
            throw new InvalidOperationException("The expert does not have permission to process this Q&A session.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class != null);

        var existingReview = session.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId);

        ValidateResolveDecision(request.Decision);

        if (string.Equals(session.Status, CaseAnswerStatuses.ExpertApproved, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(session.Status, CaseAnswerStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("This Q&A session has already been processed.");

        if (!string.Equals(session.Status, CaseAnswerStatuses.EscalatedToExpert, StringComparison.Ordinal))
            throw new ConflictException("Only sessions escalated by lecturers can be processed here.");

        var now = DateTime.UtcNow;
        var selectedQuestion = ResolveRequestedReviewQuestion(session, session.Messages);
        if (selectedQuestion == null)
            throw new InvalidOperationException("The selected student question could not be resolved for this review session.");
        if (session.RequestedReviewMessageId.HasValue &&
            !session.Messages.Any(m => m.Role == "Assistant" && m.Id == session.RequestedReviewMessageId.Value))
        {
            throw new ConflictException("Cannot resolve because the selected review assistant turn is inconsistent.");
        }

        var isReject = IsRejectDecision(request.Decision);
        if (isReject)
        {
            var rejectionNote = request.ReviewNote?.Trim();
            if (string.IsNullOrWhiteSpace(rejectionNote))
                throw new InvalidOperationException("ReviewNote is required when rejecting an escalated session.");

            if (VisualQaEducatorFeedbackHelper.IsLikelyAiStructuredBlock(rejectionNote))
                throw new InvalidOperationException("Rejection reason must be a human note, not copied AI structured output.");
        }

        QAMessage? expertMessage = null;
        var reviewFeedbackText = isReject
            ? request.ReviewNote!.Trim()
            : (string.IsNullOrWhiteSpace(request.ReviewNote?.Trim())
                ? VisualQaEducatorFeedbackHelper.SanitizeHumanFeedback(request.AnswerText)
                : request.ReviewNote.Trim());

        expertMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "Expert",
            Content = isReject
                ? reviewFeedbackText ?? string.Empty
                : request.AnswerText ?? string.Empty,
            SuggestedDiagnosis = isReject ? null : request.StructuredDiagnosis,
            DifferentialDiagnoses = isReject ? null : SerializeJsonArray(request.DifferentialDiagnoses),
            KeyImagingFindings = isReject ? null : request.KeyImagingFindings,
            ReflectiveQuestions = isReject ? null : request.ReflectiveQuestions,
            CreatedAt = now,
            TargetAssistantMessageId = session.RequestedReviewMessageId
        };

        await using var resolutionTransaction = await _unitOfWork.Context.Database.BeginTransactionAsync();
        try
        {
            if (expertMessage != null)
                await _unitOfWork.Context.QaMessages.AddAsync(expertMessage);

            session.Status = isReject ? CaseAnswerStatuses.Rejected : CaseAnswerStatuses.ExpertApproved;
            session.ExpertId = expertId;
            session.ReviewFeedback = isReject
                ? reviewFeedbackText
                : VisualQaEducatorFeedbackHelper.SanitizeHumanFeedback(reviewFeedbackText);
            session.UpdatedAt = now;

            var correctedRoiJson = SerializeCorrectedRoi(request.CorrectedRoiBoundingBox);
            var reviewNoteStored = reviewFeedbackText;

            if (existingReview == null)
            {
                existingReview = new ExpertReview
                {
                    Id = Guid.NewGuid(),
                    ExpertId = expertId,
                    AnswerId = null,
                    SessionId = session.Id,
                    ReviewNote = reviewNoteStored,
                    Action = isReject ? "Reject" : "Approve",
                    CorrectedRoi = isReject ? null : correctedRoiJson,
                    CreatedAt = now
                };
                await _unitOfWork.ExpertReviewRepository.AddAsync(existingReview);
            }
            else
            {
                existingReview.AnswerId = null;
                existingReview.SessionId = session.Id;
                existingReview.ReviewNote = reviewNoteStored;
                existingReview.Action = isReject ? "Reject" : "Approve";
                existingReview.CorrectedRoi = isReject ? null : correctedRoiJson;
                await _unitOfWork.ExpertReviewRepository.UpdateAsync(existingReview);
            }

            await _unitOfWork.SaveAsync();
            await resolutionTransaction.CommitAsync();
        }
        catch
        {
            await resolutionTransaction.RollbackAsync();
            throw;
        }

        await RemoveStaleDraftExpertReviewsAsync(session.Id, expertId);

        if (!isReject && expertMessage != null)
        {
            var goldenCore = BuildExpertApprovedGoldenCore(expertMessage);
            await TryIngestGoldenChunkWithCoreAsync(session.Id, goldenCore);
        }

        if (isReject)
        {
            await _notificationService.SendNotificationToUserAsync(
                session.StudentId,
                "Your Visual QA escalation was declined",
                reviewFeedbackText,
                "expert_review",
                $"/student/qa/image?sessionId={session.Id}");
        }
        else
        {
            await _notificationService.SendNotificationToUserAsync(
                session.StudentId,
                "An expert replied to your Visual QA session",
                "Your session has been updated by an expert. Open Visual QA to read the full response.",
                "expert_review",
                $"/student/qa/image?sessionId={session.Id}");

            await _ragExpertAnswerIndexingSignal.NotifyExpertApprovedForFutureIndexingAsync(session.Id);
        }

        var orderedMessages = expertMessage == null
            ? session.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList()
            : session.Messages
                .Append(expertMessage)
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .ToList();
        var (userMessage, latestAssistant) = ResolveRequestedReviewPair(session, orderedMessages);
        var turnsResolved = VisualQaSessionTurnsMapper.BuildTurns(session.Id, orderedMessages, session.Status, session.RequestedReviewMessageId);
        var dicomMetadata = CaseMediaDicomMetadataHelper.ResolveFirstMetadata(session.Case);

        return new ExpertEscalatedAnswerDto
        {
            AnswerId = session.Id,
            SessionId = session.Id,
            QuestionId = userMessage?.Id ?? Guid.Empty,
            StudentId = session.StudentId,
            StudentName = session.Student?.FullName ?? string.Empty,
            StudentEmail = session.Student?.Email ?? string.Empty,
            CaseId = session.CaseId,
            CaseTitle = session.Case?.Title ?? string.Empty,
            QuestionText = userMessage?.Content ?? string.Empty,
            AnswerText = MapAssistantAnswerText(latestAssistant),
            CurrentAnswerText = MapAssistantAnswerText(latestAssistant),
            StructuredDiagnosis = isReject ? null : latestAssistant?.SuggestedDiagnosis,
            DifferentialDiagnoses = isReject ? null : latestAssistant?.DifferentialDiagnoses,
            KeyImagingFindings = isReject ? null : latestAssistant?.KeyImagingFindings ?? null,
            ReflectiveQuestions = isReject ? null : latestAssistant?.ReflectiveQuestions ?? null,
            Status = session.Status,
            SessionStatus = session.Status,
            ReviewFeedback = session.ReviewFeedback,
            EscalatedById = session.LecturerId,
            EscalatedAt = session.UpdatedAt,
            AiConfidenceScore = isReject ? null : latestAssistant?.AiConfidenceScore,
            ClassId = enrollment?.ClassId,
            ClassName = enrollment?.Class?.ClassName ?? string.Empty,
            ReviewNote = existingReview.ReviewNote,
            PromotedCaseId = session.PromotedCaseId,
            Citations = isReject
                ? new List<ExpertCitationDto>()
                : MergeCitationsFromAssistantMessages(orderedMessages),
            ImageUrl = ResolveSessionImageUrl(session),
            CustomCoordinates = VisualQaRoiResolutionHelper.ResolvePreferredUserRoiJson(
                userMessage,
                session.RequestedReviewMessageId,
                turnsResolved),
            ExpertCorrectedRoiBoundingBox = DeserializeCorrectedRoi(existingReview.CorrectedRoi),
            RequestedReviewMessageId = session.RequestedReviewMessageId,
            SelectedUserMessageId = userMessage?.Id,
            SelectedAssistantMessageId = latestAssistant?.Id,
            Turns = turnsResolved,
            DicomMetadata = dicomMetadata
        };
    }

    private static void ValidateResolveDecision(string? decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
            return;
        var d = decision.Trim();
        if (string.Equals(d, "approve", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d, "reject", StringComparison.OrdinalIgnoreCase))
            return;
        throw new InvalidOperationException("Decision must be \"approve\" or \"reject\".");
    }

    private static bool IsRejectDecision(string? decision) =>
        string.Equals(decision?.Trim(), "reject", StringComparison.OrdinalIgnoreCase);

    private static string ResolveExpertRejectDisplayMessage(ResolveEscalatedAnswerRequestDto request)
    {
        var note = request.ReviewNote?.Trim();
        if (!string.IsNullOrWhiteSpace(note))
            return note;
        var ans = request.AnswerText?.Trim();
        if (!string.IsNullOrWhiteSpace(ans))
            return ans;
        return "This review escalation was declined by the expert reviewer.";
    }

    private static QAMessage? ResolveRequestedReviewQuestion(VisualQASession session, IEnumerable<QAMessage> orderedMessages)
    {
        return ResolveRequestedReviewPair(session, orderedMessages).User;
    }

    private static (QAMessage? User, QAMessage? Assistant) ResolveRequestedReviewPair(VisualQASession session, IEnumerable<QAMessage> orderedMessages)
    {
        var messages = orderedMessages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();

        QAMessage? assistant = null;
        if (session.RequestedReviewMessageId.HasValue)
        {
            assistant = messages.FirstOrDefault(m =>
                string.Equals(m.Role, "Assistant", StringComparison.OrdinalIgnoreCase) &&
                m.Id == session.RequestedReviewMessageId.Value);
        }

        if (assistant == null)
        {
            assistant = messages
                .Where(m => string.Equals(m.Role, "Assistant", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();
        }

        if (assistant == null)
        {
            var lastUserOnly = messages
                .Where(m => string.Equals(m.Role, "User", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();
            return (lastUserOnly, null);
        }

        var user = messages
            .Where(m => string.Equals(m.Role, "User", StringComparison.OrdinalIgnoreCase) &&
                        m.CreatedAt <= assistant.CreatedAt)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();

        return (user, assistant);
    }

    private static ExpertEscalatedAnswerDto MapEscalatedAnswerDto(
        VisualQASession session,
        QAMessage? userMessage,
        QAMessage? latestAssistant,
        ExpertReview? review,
        ClassEnrollment? enrollment,
        IReadOnlyList<VisualQaTurnDto> turns,
        JsonElement? dicomMetadata)
    {
        var orderedMessages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();
        var report = ExpertEscalatedReportBuilder.BuildFromAssistant(latestAssistant);

        return new ExpertEscalatedAnswerDto
        {
            AnswerId = session.Id,
            SessionId = session.Id,
            QuestionId = userMessage?.Id ?? Guid.Empty,
            StudentId = session.StudentId,
            StudentName = session.Student?.FullName ?? string.Empty,
            StudentEmail = session.Student?.Email ?? string.Empty,
            CaseId = session.CaseId,
            CaseTitle = session.Case?.Title ?? string.Empty,
            CaseDescription = session.Case?.Description,
            CaseSuggestedDiagnosis = session.Case?.SuggestedDiagnosis,
            CaseKeyFindings = session.Case?.KeyFindings,
            QuestionText = userMessage?.Content ?? string.Empty,
            AnswerText = MapAssistantAnswerText(latestAssistant),
            CurrentAnswerText = MapAssistantAnswerText(latestAssistant),
            StructuredDiagnosis = latestAssistant?.SuggestedDiagnosis ?? report.Diagnosis,
            DifferentialDiagnoses = latestAssistant?.DifferentialDiagnoses
                                    ?? (report.DifferentialDiagnoses.Count > 0
                                        ? JsonSerializer.Serialize(report.DifferentialDiagnoses)
                                        : null),
            DifferentialDiagnosesList = report.DifferentialDiagnoses.Count > 0
                ? report.DifferentialDiagnoses
                : ParseDifferentialDiagnosesList(latestAssistant?.DifferentialDiagnoses),
            KeyImagingFindings = latestAssistant?.KeyImagingFindings ?? report.KeyImagingFindings,
            ReflectiveQuestions = latestAssistant?.ReflectiveQuestions
                                  ?? (report.ReflectiveQuestions.Count > 0
                                      ? string.Join("\n", report.ReflectiveQuestions)
                                      : null),
            ReferencesAndCitations = BuildReferencesAndCitationsFromAssistant(latestAssistant),
            Status = session.Status,
            SessionStatus = session.Status,
            ReviewFeedback = session.ReviewFeedback,
            EscalatedById = session.LecturerId,
            EscalatedAt = session.UpdatedAt ?? session.CreatedAt,
            AiConfidenceScore = latestAssistant?.AiConfidenceScore ?? report.AiConfidenceScore,
            ClassId = enrollment?.ClassId,
            ClassName = enrollment?.Class?.ClassName ?? string.Empty,
            ReviewNote = review?.ReviewNote,
            PromotedCaseId = session.PromotedCaseId,
            Citations = MergeCitationsFromAssistantMessages(orderedMessages),
            ImageUrl = ResolveSessionImageUrl(session),
            CustomCoordinates = VisualQaRoiResolutionHelper.ResolvePreferredUserRoiJson(
                userMessage,
                session.RequestedReviewMessageId,
                turns),
            ExpertCorrectedRoiBoundingBox = DeserializeCorrectedRoi(review?.CorrectedRoi),
            RequestedReviewMessageId = session.RequestedReviewMessageId,
            SelectedUserMessageId = userMessage?.Id,
            SelectedAssistantMessageId = latestAssistant?.Id,
            Turns = turns,
            Report = report,
            DicomMetadata = dicomMetadata
        };
    }

    private static string? MapAssistantAnswerText(QAMessage? assistant) =>
        VisualQaAssistantAnswerFormatter.FormatDisplayText(assistant);

    private static IReadOnlyList<string> ParseDifferentialDiagnosesList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(raw);
            if (parsed is { Count: > 0 })
            {
                return parsed
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return raw
            .Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> BuildReferencesAndCitationsFromAssistant(QAMessage? assistant)
    {
        if (assistant == null)
            return Array.Empty<string>();

        var fromJson = VisualQaCitationMetadataBuilder.DeserializeMany(assistant.CitationsJson);
        if (fromJson.Count > 0)
        {
            return fromJson
                .Select(c =>
                    !string.IsNullOrWhiteSpace(c.DisplayLabel) ? c.DisplayLabel :
                    !string.IsNullOrWhiteSpace(c.Snippet) ? c.Snippet :
                    c.SourceText)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();
        }

        return assistant.Citations
            .Select(c => VisualQaCitationMetadataBuilder.FromDocumentChunk(c.Chunk))
            .Select(c =>
                !string.IsNullOrWhiteSpace(c.DisplayLabel) ? c.DisplayLabel :
                !string.IsNullOrWhiteSpace(c.Snippet) ? c.Snippet :
                c.SourceText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    public async Task FlagChunkAsync(Guid expertId, Guid chunkId, FlagChunkRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Chunk flag reason is required.");

        var chunk = await _unitOfWork.Context.DocumentChunks.FirstOrDefaultAsync(ch => ch.Id == chunkId)
            ?? throw new KeyNotFoundException("Document chunk not found.");

        var allowedSpecialtyIds = _unitOfWork.Context.Users
            .Where(u => u.Id == expertId && u.PrimaryBoneSpecialtyId.HasValue)
            .Select(u => u.PrimaryBoneSpecialtyId!.Value);

        var canReviewChunk = await _unitOfWork.Context.Citations
            .Where(c => c.ChunkId == chunkId)
            .AnyAsync(c =>
                c.Message != null &&
                c.Message.Session != null &&
                c.Message.Session.Status == CaseAnswerStatuses.EscalatedToExpert &&
                (
                    c.Message.Session.ExpertId == expertId ||
                    (c.Message.Session.TargetBoneSpecialtyId.HasValue
                        && allowedSpecialtyIds.Contains(c.Message.Session.TargetBoneSpecialtyId.Value)) ||
                    _unitOfWork.Context.ClassEnrollments.Any(e =>
                        e.StudentId == c.Message.Session!.StudentId &&
                        e.Class != null &&
                        e.Class.ExpertId == expertId)));

        if (!canReviewChunk)
            throw new InvalidOperationException("The expert does not have permission to flag this chunk.");

        if (!chunk.IsFlagged)
        {
            chunk.IsFlagged = true;
            chunk.FlagReason = request.Reason.Trim();
            chunk.FlaggedByExpertId = expertId;
            chunk.FlaggedAt = DateTime.UtcNow;
            await _unitOfWork.DocumentChunkRepository.UpdateAsync(chunk);
            await _unitOfWork.SaveAsync();
        }
    }

    private Task<bool> ExpertMatchesSpecialtyAsync(Guid expertId, Guid specialtyId, CancellationToken cancellationToken = default) =>
        _unitOfWork.Context.Users
            .Where(u => u.Id == expertId)
            .AnyAsync(u => u.PrimaryBoneSpecialtyId == specialtyId, cancellationToken);

    private static string? SerializeCorrectedRoi(double[]? value)
    {
        if (value == null || value.Length == 0)
            return null;
        return JsonSerializer.Serialize(value);
    }

    private static double[]? DeserializeCorrectedRoi(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<double[]>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? SerializeJsonArray(JsonElement? value)
    {
        if (value == null)
            return null;

        var el = value.Value;
        return el.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => string.IsNullOrWhiteSpace(el.GetString()) ? null : el.GetString(),
            JsonValueKind.Array =>
                JsonSerializer.Serialize(
                    el.EnumerateArray()
                        .Select(x => x.ValueKind == JsonValueKind.String ? (x.GetString() ?? string.Empty) : x.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .ToList()) is { Length: > 2 } joined
                    ? joined
                    : null,
            _ => el.ToString()
        };
    }

    private static string? ResolveSessionImageUrl(VisualQASession? session)
    {
        if (session == null)
            return null;
        if (!string.IsNullOrWhiteSpace(session.CustomImageUrl))
            return session.CustomImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(session.Image?.ImageUrl))
            return session.Image.ImageUrl.Trim();

        var images = session.Case?.MedicalImages;
        if (images == null || images.Count == 0)
            return null;

        var url = images
            .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
            .ThenBy(m => m.Id)
            .Select(m => m.ImageUrl)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
    }

    /// <summary>Unions citations from every assistant turn, deduped by chunk and optional medical case id.</summary>
    private static List<ExpertCitationDto> MergeCitationsFromAssistantMessages(IEnumerable<QAMessage> orderedMessages)
    {
        var messages = orderedMessages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();
        var seen = new HashSet<(Guid ChunkId, Guid? MedicalCaseId)>();
        var result = new List<ExpertCitationDto>();
        foreach (var m in messages)
        {
            if (!string.Equals(m.Role, "Assistant", StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var dto in MapCitations(m.Citations ?? Enumerable.Empty<Citation>()))
            {
                var key = (dto.ChunkId, dto.MedicalCaseId);
                if (seen.Add(key))
                    result.Add(dto);
            }
        }

        return result;
    }

    private static List<ExpertCitationDto> MapCitations(IEnumerable<Citation> citations)
    {
        return citations
            .OrderBy(c => c.Chunk?.ChunkOrder ?? int.MaxValue)
            .Select(c =>
            {
                var meta = VisualQaCitationMetadataBuilder.FromDocumentChunk(c.Chunk);
                var sourceText = meta.SourceText ?? c.Chunk?.Content;
                var snippet = meta.Snippet ?? VisualQaCitationMetadataBuilder.BuildSnippet(sourceText);

                if (string.IsNullOrWhiteSpace(sourceText))
                    sourceText = snippet;

                if (string.IsNullOrWhiteSpace(sourceText) ||
                    string.Equals(snippet, "Reference excerpt", StringComparison.Ordinal))
                {
                    var label = meta.DisplayLabel?.Trim();
                    var page = meta.PageLabel?.Trim();
                    var fallback = string.IsNullOrWhiteSpace(label)
                        ? null
                        : string.IsNullOrWhiteSpace(page)
                            ? label
                            : $"{label} ({page})";
                    if (!string.IsNullOrWhiteSpace(fallback))
                    {
                        sourceText = fallback;
                        snippet = VisualQaCitationMetadataBuilder.BuildSnippet(fallback);
                    }
                }

                return new ExpertCitationDto
                {
                    ChunkId = c.ChunkId,
                    DocumentId = c.Chunk?.DocId,
                    MedicalCaseId = meta.MedicalCaseId,
                    SourceText = sourceText,
                    ReferenceUrl = meta.ReferenceUrl,
                    Href = meta.Href ?? meta.ReferenceUrl,
                    PageNumber = meta.PageNumber,
                    StartPage = meta.StartPage,
                    EndPage = meta.EndPage,
                    PageLabel = meta.PageLabel,
                    DisplayLabel = meta.DisplayLabel,
                    Snippet = snippet,
                    Preview = snippet,
                    Kind = string.IsNullOrWhiteSpace(meta.Kind) ? "doc" : meta.Kind
                };
            })
            .ToList();
    }

    private static bool CanTransitionFrom(string currentStatus, string targetStatus)
    {
        if (string.Equals(targetStatus, "ExpertApproved", StringComparison.Ordinal))
            return string.Equals(currentStatus, "EscalatedToExpert", StringComparison.Ordinal);

        if (string.Equals(targetStatus, "Active", StringComparison.Ordinal))
            return string.Equals(currentStatus, "EscalatedToExpert", StringComparison.Ordinal)
                   || string.Equals(currentStatus, "ExpertApproved", StringComparison.Ordinal);

        return true;
    }

    private static string BuildExpertApprovedGoldenCore(QAMessage expertMessage)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(expertMessage.SuggestedDiagnosis))
            parts.Add($"Structured diagnosis: {expertMessage.SuggestedDiagnosis.Trim()}");
        if (!string.IsNullOrWhiteSpace(expertMessage.KeyImagingFindings))
            parts.Add($"Key imaging findings: {expertMessage.KeyImagingFindings.Trim()}");
        if (!string.IsNullOrWhiteSpace(expertMessage.Content))
            parts.Add(expertMessage.Content.Trim());
        return parts.Count > 0 ? string.Join("\n\n", parts) : string.Empty;
    }

    private static string BuildGoldenChunkDiagnosisText(string modality, string anatomy, string pathologyGroup, string coreAnswer) =>
        $"Modality: {modality}\nAnatomy focus: {anatomy}\nPathology group / case context: {pathologyGroup}\n\nExpert-approved content:\n{coreAnswer}";

    private async Task TryIngestGoldenChunkAfterSimpleApproveAsync(Guid sessionId)
    {
        try
        {
            var session = await _unitOfWork.Context.VisualQaSessions
                .AsNoTracking()
                .Include(s => s.Case!).ThenInclude(c => c!.Category)
                .Include(s => s.Case!).ThenInclude(c => c!.MedicalImages)
                .Include(s => s.Image)
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null)
                return;

            var ordered = session.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
            var pair = ResolveRequestedReviewPair(session, ordered);
            var coreParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(pair.Assistant?.SuggestedDiagnosis))
                coreParts.Add(pair.Assistant.SuggestedDiagnosis.Trim());
            if (!string.IsNullOrWhiteSpace(pair.Assistant?.Content))
                coreParts.Add(pair.Assistant.Content.Trim());
            if (coreParts.Count == 0)
                return;

            await IngestInternalAsync(session, string.Join("\n\n", coreParts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Golden chunk ingest failed after simple approve for session {SessionId}.", sessionId);
        }
    }

    private async Task TryIngestGoldenChunkWithCoreAsync(Guid sessionId, string coreDiagnosisText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(coreDiagnosisText))
                return;

            var session = await _unitOfWork.Context.VisualQaSessions
                .AsNoTracking()
                .Include(s => s.Case!).ThenInclude(c => c!.Category)
                .Include(s => s.Case!).ThenInclude(c => c!.MedicalImages)
                .Include(s => s.Image)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null)
                return;

            await IngestInternalAsync(session, coreDiagnosisText.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Golden chunk ingest failed for session {SessionId}.", sessionId);
        }
    }

    private async Task TryIngestGoldenChunkAfterPromoteAsync(VisualQASession session, PromoteToLibraryRequestDto request)
    {
        var core = new List<string>
        {
            $"Suggested diagnosis: {request.SuggestedDiagnosis.Trim()}",
            $"Key findings: {request.KeyFindings.Trim()}",
            request.Description.Trim(),
            $"Reflective questions: {request.ReflectiveQuestions.Trim()}"
        };
        await IngestInternalAsync(session, string.Join("\n\n", core));
    }

    private async Task IngestInternalAsync(VisualQASession session, string coreDiagnosisText)
    {
        var imagePathOrUrl = ResolveSessionImageUrl(session);
        if (string.IsNullOrWhiteSpace(imagePathOrUrl))
        {
            _logger.LogWarning("Golden chunk ingest skipped: no image URL for session {SessionId}.", session.Id);
            return;
        }

        var modality = session.Image?.Modality;
        if (string.IsNullOrWhiteSpace(modality) && session.Case?.MedicalImages is { Count: > 0 } images)
        {
            modality = images
                .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
                .ThenBy(m => m.Id)
                .Select(m => m.Modality)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
        }

        modality = string.IsNullOrWhiteSpace(modality) ? "Unknown" : modality.Trim();

        var anatomy = session.Case?.Category?.Name;
        if (string.IsNullOrWhiteSpace(anatomy))
            anatomy = "Unknown";

        var pathologyGroup = session.Case?.SuggestedDiagnosis;
        if (string.IsNullOrWhiteSpace(pathologyGroup))
            pathologyGroup = session.Case?.Category?.Description;
        if (string.IsNullOrWhiteSpace(pathologyGroup))
            pathologyGroup = "Unspecified";

        var bundle = BuildGoldenChunkDiagnosisText(modality, anatomy.Trim(), pathologyGroup.Trim(), coreDiagnosisText);
        var ingest = await _pythonAiConnector.TriggerIngestAsync(
            imagePathOrUrl.Trim(),
            bundle,
            ingestPurpose: "library");
        if (!ingest.Success)
            _logger.LogWarning("Python AI ingest failed for golden chunk session {SessionId}: {Error}", session.Id, ingest.ErrorMessage);
    }
}
