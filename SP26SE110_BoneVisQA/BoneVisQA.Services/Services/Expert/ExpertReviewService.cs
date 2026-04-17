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
using BoneVisQA.Services.Services;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Expert;

public class ExpertReviewService : IExpertReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IRagExpertAnswerIndexingSignal _ragExpertAnswerIndexingSignal;

    /// <summary>
    /// Expert queue sourced from Visual QA sessions escalated by lecturer.
    /// </summary>
    private static IQueryable<VisualQASession> QueryExpertScopedEscalatedQueue(IUnitOfWork uow, Guid expertId) =>
        uow.Context.VisualQaSessions
            .AsNoTracking()
            .Where(s => s.Status == "EscalatedToExpert")
            .Where(s =>
                uow.Context.ClassEnrollments.Any(e =>
                    e.StudentId == s.StudentId &&
                    e.Class != null &&
                    e.Class.ExpertId == expertId));

    public ExpertReviewService(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IRagExpertAnswerIndexingSignal ragExpertAnswerIndexingSignal)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _ragExpertAnswerIndexingSignal = ragExpertAnswerIndexingSignal;
    }

    public async Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetEscalatedAnswersAsync(Guid expertId)
    {
        var sessions = await QueryExpertScopedEscalatedQueue(_unitOfWork, expertId)
            .AsSplitQuery()
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
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
            .Where(e => studentIds.Contains(e.StudentId) && e.Class != null && e.Class.ExpertId == expertId)
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
            var (userMessage, latestAssistant) = ResolveRequestedReviewPair(s, orderedMessages);
        if (s.RequestedReviewMessageId.HasValue &&
            (latestAssistant == null || latestAssistant.Id != s.RequestedReviewMessageId.Value || userMessage == null))
        {
            return new ExpertEscalatedAnswerDto
            {
                AnswerId = s.Id,
                QuestionId = s.Id,
                StudentId = s.StudentId,
                StudentName = s.Student?.FullName ?? string.Empty,
                StudentEmail = s.Student?.Email ?? string.Empty,
                CaseId = s.CaseId,
                CaseTitle = s.Case?.Title ?? string.Empty,
                QuestionText = string.Empty,
                CurrentAnswerText = "Selected review pair is inconsistent. Please re-request review from student flow.",
                Status = s.Status,
                EscalatedById = s.LecturerId,
                EscalatedAt = s.UpdatedAt ?? s.CreatedAt,
                ClassId = enrollment?.ClassId,
                ClassName = enrollment?.Class?.ClassName ?? string.Empty,
                RequestedReviewMessageId = s.RequestedReviewMessageId,
                SelectedUserMessageId = null,
                SelectedAssistantMessageId = null
            };
        }

            return new ExpertEscalatedAnswerDto
            {
                AnswerId = s.Id,
                QuestionId = userMessage?.Id ?? Guid.Empty,
                StudentId = s.StudentId,
                StudentName = s.Student?.FullName ?? string.Empty,
                StudentEmail = s.Student?.Email ?? string.Empty,
                CaseId = s.CaseId,
                CaseTitle = s.Case?.Title ?? string.Empty,
                QuestionText = userMessage?.Content ?? string.Empty,
                CurrentAnswerText = latestAssistant?.Content,
                StructuredDiagnosis = latestAssistant?.SuggestedDiagnosis,
                DifferentialDiagnoses = latestAssistant?.DifferentialDiagnoses,
                KeyImagingFindings = latestAssistant?.KeyImagingFindings,
                ReflectiveQuestions = latestAssistant?.ReflectiveQuestions,
                Status = s.Status,
                EscalatedById = s.LecturerId,
                EscalatedAt = s.UpdatedAt ?? s.CreatedAt,
                AiConfidenceScore = latestAssistant?.AiConfidenceScore,
                ClassId = enrollment?.ClassId,
                ClassName = enrollment?.Class?.ClassName ?? string.Empty,
                ReviewNote = review?.ReviewNote,
                PromotedCaseId = s.PromotedCaseId,
                Citations = MapCitations(latestAssistant?.Citations ?? Enumerable.Empty<Citation>()),
                ImageUrl = ResolveSessionImageUrl(s),
                CustomCoordinates = userMessage?.Coordinates,
                RequestedReviewMessageId = s.RequestedReviewMessageId,
                SelectedUserMessageId = userMessage?.Id,
                SelectedAssistantMessageId = latestAssistant?.Id
            };
        }).ToList();
    }

    public Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetCaseAnswersAsync(Guid expertId)
        => GetEscalatedAnswersAsync(expertId);

    public async Task<ExpertEscalatedAnswerDto> RespondToSessionAsync(Guid expertId, Guid sessionId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Expert feedback content is required.");

        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Image)
            .Include(s => s.Messages)
                .ThenInclude(m => m.Citations)
                    .ThenInclude(c => c.Chunk)
                        .ThenInclude(ch => ch.Doc)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.ExpertId == expertId);

        if (enrollment == null)
            throw new InvalidOperationException("The expert does not have permission to respond to this Q&A session.");
        if (!CanTransitionFrom(session.Status, "Active"))
            throw new ConflictException($"Cannot respond to a session from status '{session.Status}'.");

        var now = DateTime.UtcNow;
        var expertMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "Expert",
            Content = content.Trim(),
            CreatedAt = now
        };

        await _unitOfWork.Context.QaMessages.AddAsync(expertMessage);
        session.Status = "Active";
        session.ExpertId = expertId;
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

        return new ExpertEscalatedAnswerDto
        {
            AnswerId = session.Id,
            QuestionId = userMessage?.Id ?? Guid.Empty,
            StudentId = session.StudentId,
            StudentName = session.Student?.FullName ?? string.Empty,
            StudentEmail = session.Student?.Email ?? string.Empty,
            CaseId = session.CaseId,
            CaseTitle = session.Case?.Title ?? string.Empty,
            QuestionText = userMessage?.Content ?? string.Empty,
            CurrentAnswerText = expertMessage.Content,
            StructuredDiagnosis = null,
            DifferentialDiagnoses = null,
            KeyImagingFindings = null,
            ReflectiveQuestions = null,
            Status = session.Status,
            EscalatedById = session.LecturerId,
            EscalatedAt = session.UpdatedAt,
            AiConfidenceScore = null,
            ClassId = enrollment?.ClassId,
            ClassName = enrollment?.Class?.ClassName ?? string.Empty,
            ReviewNote = null,
            PromotedCaseId = session.PromotedCaseId,
            Citations = new List<ExpertCitationDto>(),
            ImageUrl = ResolveSessionImageUrl(session),
            CustomCoordinates = userMessage?.Coordinates,
            RequestedReviewMessageId = session.RequestedReviewMessageId,
            SelectedUserMessageId = userMessage?.Id,
            SelectedAssistantMessageId = session.RequestedReviewMessageId
        };
    }

    public async Task ApproveSessionAsync(Guid expertId, Guid sessionId)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.ExpertId == expertId);

        if (enrollment == null)
            throw new InvalidOperationException("The expert does not have permission to approve this Q&A session.");
        if (!CanTransitionFrom(session.Status, "ExpertApproved"))
            throw new ConflictException($"Cannot approve a session from status '{session.Status}'.");

        session.Status = "ExpertApproved";
        session.ExpertId = expertId;
        session.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveAsync();
    }

    public async Task<Guid> PromoteToLibraryAsync(Guid expertId, Guid sessionId, PromoteToLibraryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new InvalidOperationException("Description is required.");
        if (string.IsNullOrWhiteSpace(request.SuggestedDiagnosis))
            throw new InvalidOperationException("SuggestedDiagnosis is required.");
        if (string.IsNullOrWhiteSpace(request.KeyFindings))
            throw new InvalidOperationException("KeyFindings is required.");
        if (string.IsNullOrWhiteSpace(request.ReflectiveQuestions))
            throw new InvalidOperationException("ReflectiveQuestions is required.");

        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.ExpertId == expertId);

        if (enrollment == null)
            throw new InvalidOperationException("The expert does not have permission to move this Q&A session to the library.");

        if (!string.Equals(session.Status, "ExpertApproved", StringComparison.Ordinal))
            throw new InvalidOperationException("This session can be moved to the library only after expert approval.");

        if (session.PromotedCaseId.HasValue)
            throw new InvalidOperationException("This case has already been added to the library.");

        if (string.IsNullOrWhiteSpace(session.CustomImageUrl) || session.ImageId.HasValue)
            throw new InvalidOperationException("Only self-uploaded images can be added to the library.");

        var now = DateTime.UtcNow;

        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync();
        try
        {
            var newCase = new MedicalCase
            {
                Id = Guid.NewGuid(),
                Title = "Clinical case from the community",
                Description = request.Description.Trim(),
                SuggestedDiagnosis = request.SuggestedDiagnosis.Trim(),
                KeyFindings = request.KeyFindings.Trim(),
                ReflectiveQuestions = request.ReflectiveQuestions.Trim(),
                Difficulty = "Medium",
                IsApproved = true,
                IsActive = true,
                CreatedByExpertId = expertId,
                CreatedAt = now,
                UpdatedAt = now,
                IndexingStatus = DocumentIndexingStatuses.Pending,
                Version = SemanticDocumentVersion.Initial
            };

            await _unitOfWork.Context.MedicalCases.AddAsync(newCase);

            var image = new MedicalImage
            {
                Id = Guid.NewGuid(),
                CaseId = newCase.Id,
                ImageUrl = session.CustomImageUrl.Trim(),
                Modality = "Other",
                CreatedAt = now
            };
            await _unitOfWork.Context.MedicalImages.AddAsync(image);

            var tag = await _unitOfWork.Context.Tags
                .FirstOrDefaultAsync(t => t.Name == "Student Q&A");
            if (tag == null)
            {
                tag = new Tag
                {
                    Id = Guid.NewGuid(),
                    Name = "Student Q&A",
                    Type = "Source",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _unitOfWork.Context.Tags.AddAsync(tag);
            }

            var caseTag = new CaseTag
            {
                CaseId = newCase.Id,
                TagId = tag.Id,
                CreatedAt = now
            };
            await _unitOfWork.Context.CaseTags.AddAsync(caseTag);

            session.PromotedCaseId = newCase.Id;
            await _unitOfWork.SaveAsync();
            await transaction.CommitAsync();

            return newCase.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ExpertEscalatedAnswerDto> ResolveEscalatedAnswerAsync(Guid expertId, Guid sessionId, ResolveEscalatedAnswerRequestDto request)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Image)
            .Include(s => s.Messages)
                .ThenInclude(m => m.Citations)
                    .ThenInclude(c => c.Chunk)
                        .ThenInclude(ch => ch.Doc)
            .Include(s => s.ExpertReviews)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("The Q&A session to process was not found.");

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.ExpertId == expertId);

        var existingReview = session.ExpertReviews.FirstOrDefault(r => r.ExpertId == expertId);

        if (string.Equals(session.Status, "ExpertApproved", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("This Q&A session has already been processed.");

        if (enrollment == null)
            throw new InvalidOperationException("The expert does not have permission to process this Q&A session (the student is not in a class you manage).");

        if (!string.Equals(session.Status, "EscalatedToExpert", StringComparison.Ordinal))
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
        var expertMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "Expert",
            Content = request.AnswerText,
            SuggestedDiagnosis = request.StructuredDiagnosis,
            DifferentialDiagnoses = SerializeJsonArray(request.DifferentialDiagnoses),
            KeyImagingFindings = request.KeyImagingFindings,
            ReflectiveQuestions = request.ReflectiveQuestions,
            CreatedAt = now
        };
        await _unitOfWork.Context.QaMessages.AddAsync(expertMessage);

        session.Status = "ExpertApproved";
        session.ExpertId = expertId;
        session.UpdatedAt = now;

        if (existingReview == null)
        {
            existingReview = new ExpertReview
            {
                Id = Guid.NewGuid(),
                ExpertId = expertId,
                AnswerId = null,
                SessionId = session.Id,
                ReviewNote = request.ReviewNote,
                Action = "Approve",
                CreatedAt = now
            };
            await _unitOfWork.ExpertReviewRepository.AddAsync(existingReview);
        }
        else
        {
            existingReview.AnswerId = null;
            existingReview.SessionId = session.Id;
            existingReview.ReviewNote = request.ReviewNote;
            existingReview.Action = "Approve";
            await _unitOfWork.ExpertReviewRepository.UpdateAsync(existingReview);
        }

        await _unitOfWork.SaveAsync();

        await _notificationService.SendNotificationToUserAsync(
            session.StudentId,
            "An expert has processed your question",
            "Your answer has been approved by an expert. You can review it in your question history.",
            "expert_review",
            $"/student/cases/history");

        await _ragExpertAnswerIndexingSignal.NotifyExpertApprovedForFutureIndexingAsync(session.Id);

        var orderedMessages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();
        var (userMessage, latestAssistant) = ResolveRequestedReviewPair(session, orderedMessages);

        return new ExpertEscalatedAnswerDto
        {
            AnswerId = session.Id,
            QuestionId = userMessage?.Id ?? Guid.Empty,
            StudentId = session.StudentId,
            StudentName = session.Student?.FullName ?? string.Empty,
            StudentEmail = session.Student?.Email ?? string.Empty,
            CaseId = session.CaseId,
            CaseTitle = session.Case?.Title ?? string.Empty,
            QuestionText = userMessage?.Content ?? string.Empty,
            CurrentAnswerText = latestAssistant?.Content,
            StructuredDiagnosis = latestAssistant?.SuggestedDiagnosis,
            DifferentialDiagnoses = latestAssistant?.DifferentialDiagnoses,
            KeyImagingFindings = latestAssistant?.KeyImagingFindings,
            ReflectiveQuestions = latestAssistant?.ReflectiveQuestions,
            Status = session.Status,
            EscalatedById = session.LecturerId,
            EscalatedAt = session.UpdatedAt,
            AiConfidenceScore = latestAssistant?.AiConfidenceScore,
            ClassId = enrollment?.ClassId,
            ClassName = enrollment?.Class?.ClassName ?? string.Empty,
            ReviewNote = existingReview.ReviewNote,
            PromotedCaseId = session.PromotedCaseId,
            Citations = MapCitations(latestAssistant?.Citations ?? Enumerable.Empty<Citation>()),
            ImageUrl = ResolveSessionImageUrl(session),
            CustomCoordinates = userMessage?.Coordinates,
            RequestedReviewMessageId = session.RequestedReviewMessageId,
            SelectedUserMessageId = userMessage?.Id,
            SelectedAssistantMessageId = latestAssistant?.Id
        };
    }

    private static QAMessage? ResolveRequestedReviewQuestion(VisualQASession session, IEnumerable<QAMessage> orderedMessages)
    {
        if (!session.RequestedReviewMessageId.HasValue)
            return orderedMessages.FirstOrDefault(m => m.Role == "User");

        var assistant = orderedMessages
            .Where(m => m.Role == "Assistant" && m.Id == session.RequestedReviewMessageId.Value)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();
        if (assistant == null)
            return orderedMessages.FirstOrDefault(m => m.Role == "User");

        return orderedMessages
            .Where(m => m.Role == "User" && m.CreatedAt <= assistant.CreatedAt)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();
    }

    private static (QAMessage? User, QAMessage? Assistant) ResolveRequestedReviewPair(VisualQASession session, IEnumerable<QAMessage> orderedMessages)
    {
        var messages = orderedMessages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();

        var user = ResolveRequestedReviewQuestion(session, messages);
        var assistant = session.RequestedReviewMessageId.HasValue
            ? messages.FirstOrDefault(m => m.Role == "Assistant" && m.Id == session.RequestedReviewMessageId.Value)
            : messages.LastOrDefault(m => m.Role == "Assistant");

        if (assistant == null)
        {
            assistant = messages
                .Where(m => m.Role == "Assistant")
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();
        }

        return (user, assistant);
    }

    public async Task FlagChunkAsync(Guid expertId, Guid chunkId, FlagChunkRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Chunk flag reason is required.");

        var chunk = await _unitOfWork.Context.DocumentChunks.FirstOrDefaultAsync(ch => ch.Id == chunkId)
            ?? throw new KeyNotFoundException("Document chunk not found.");

        var canReviewChunk = await _unitOfWork.Context.Citations
            .Where(c => c.ChunkId == chunkId)
            .AnyAsync(c =>
                c.Message != null &&
                c.Message.Session != null &&
                c.Message.Session.Status == "EscalatedToExpert" &&
                _unitOfWork.Context.ClassEnrollments.Any(e =>
                    e.StudentId == c.Message.Session.StudentId &&
                    e.Class!.ExpertId == expertId));

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

    private static List<ExpertCitationDto> MapCitations(IEnumerable<Citation> citations)
    {
        return citations
            .OrderBy(c => c.Chunk?.ChunkOrder ?? int.MaxValue)
            .Select(c => new ExpertCitationDto
            {
                ChunkId = c.ChunkId,
                SourceText = c.Chunk?.Content,
                ReferenceUrl = BuildCitationUrl(c.Chunk?.Doc?.FilePath),
                PageNumber = c.Chunk == null ? null : (c.Chunk.StartPage > 0 ? c.Chunk.StartPage : c.Chunk.ChunkOrder + 1),
                StartPage = c.Chunk?.StartPage,
                EndPage = c.Chunk?.EndPage
            })
            .ToList();
    }

    private static string? BuildCitationUrl(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        return filePath;
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
}
