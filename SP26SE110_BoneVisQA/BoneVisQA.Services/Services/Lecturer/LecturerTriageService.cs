using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerTriageService : ILecturerTriageService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public LecturerTriageService(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<EscalatedAnswerDto> EscalateAnswerAsync(Guid lecturerId, Guid sessionId, EscalateAnswerRequestDto? request)
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
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("The Q&A session to escalate was not found.");

        var enrollments = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .Where(e =>
                e.StudentId == session.StudentId &&
                e.Class != null &&
                e.Class.LecturerId == lecturerId)
            .ToListAsync();

        if (enrollments.Count == 0)
            throw new InvalidOperationException("The lecturer does not have permission to escalate this answer.");

        var withExpert = enrollments
            .Where(e => e.Class != null && e.Class.ExpertId.HasValue)
            .ToList();
        if (withExpert.Count == 0)
            throw new InvalidOperationException("This class has not been assigned an expert for escalation yet.");

        ClassEnrollment classEnrollment;
        if (session.CaseId.HasValue && session.CaseId.Value != Guid.Empty)
        {
            var caseClassIds = await _unitOfWork.Context.ClassCases
                .AsNoTracking()
                .Where(cc => cc.CaseId == session.CaseId.Value)
                .Select(cc => cc.ClassId)
                .ToListAsync();

            var preferred = caseClassIds.Count > 0
                ? withExpert.FirstOrDefault(e => caseClassIds.Contains(e.ClassId))
                : null;
            classEnrollment = preferred ?? withExpert.First();
        }
        else
        {
            classEnrollment = withExpert.First();
        }

        if (string.Equals(session.Status, "EscalatedToExpert", StringComparison.Ordinal))
            throw new ConflictException("This Q&A session has already been escalated.");
        if (!CanTransitionFrom(session.Status, "EscalatedToExpert"))
            throw new ConflictException($"Cannot escalate a session from status '{session.Status}'.");

        session.Status = "EscalatedToExpert";
        session.ExpertId = classEnrollment.Class!.ExpertId!.Value;
        session.LecturerId = lecturerId;
        session.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveAsync();

        var latestUser = session.Messages
            .Where(m => m.Role == "User")
            .OrderBy(m => m.CreatedAt)
            .LastOrDefault();
        var latestAssistant = session.Messages
            .Where(m => m.Role == "Assistant")
            .OrderBy(m => m.CreatedAt)
            .LastOrDefault();
        var (targetUser, targetAssistant) = ResolveRequestedReviewPair(session);
        EnsureSelectedPairConsistency(session, targetUser, targetAssistant);

        return new EscalatedAnswerDto
        {
            AnswerId = session.Id,
            QuestionId = targetUser?.Id ?? latestUser?.Id ?? Guid.Empty,
            StudentId = session.StudentId,
            StudentName = session.Student?.FullName ?? string.Empty,
            StudentEmail = session.Student?.Email ?? string.Empty,
            CaseId = session.CaseId,
            CaseTitle = session.Case?.Title ?? string.Empty,
            QuestionText = targetUser?.Content ?? latestUser?.Content ?? string.Empty,
            CurrentAnswerText = targetAssistant?.Content ?? latestAssistant?.Content,
            StructuredDiagnosis = targetAssistant?.SuggestedDiagnosis ?? latestAssistant?.SuggestedDiagnosis,
            DifferentialDiagnoses = DeserializeJsonArray(targetAssistant?.DifferentialDiagnoses ?? latestAssistant?.DifferentialDiagnoses),
            Status = session.Status,
            EscalatedById = lecturerId,
            EscalatedAt = session.UpdatedAt,
            AiConfidenceScore = targetAssistant?.AiConfidenceScore ?? latestAssistant?.AiConfidenceScore,
            ClassId = classEnrollment.ClassId,
            ClassName = classEnrollment.Class?.ClassName ?? string.Empty,
            ReviewNote = request?.ReviewNote,
            ImageUrl = ResolveSessionImageUrl(session),
            CustomCoordinates = targetUser?.Coordinates,
            RequestedReviewMessageId = session.RequestedReviewMessageId,
            SelectedUserMessageId = targetUser?.Id,
            SelectedAssistantMessageId = targetAssistant?.Id,
            Citations = ResolveLecturerCitations(targetAssistant)
        };
    }

    public async Task RejectAnswerAsync(Guid lecturerId, Guid sessionId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Rejection reason is required.");

        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("The Q&A session to reject was not found.");

        var classEnrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.LecturerId == lecturerId);

        if (classEnrollment == null)
            throw new InvalidOperationException("The lecturer does not have permission to reject this Q&A session.");
        if (!CanTransitionFrom(session.Status, "Rejected"))
            throw new ConflictException($"Cannot reject a session from status '{session.Status}'.");

        session.Status = "Rejected";
        session.LecturerId = lecturerId;
        session.UpdatedAt = DateTime.UtcNow;

        var rejectionMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "Lecturer",
            Content = reason.Trim(),
            CreatedAt = DateTime.UtcNow,
            TargetAssistantMessageId = session.RequestedReviewMessageId
        };

        await _unitOfWork.Context.QaMessages.AddAsync(rejectionMessage);
        await _unitOfWork.SaveAsync();

        var preview = reason.Trim();
        if (preview.Length > 200)
            preview = preview[..200].TrimEnd() + "…";

        await _notificationService.SendNotificationToUserAsync(
            session.StudentId,
            "Lecturer updated your Visual QA session",
            preview,
            "visual_qa_lecturer_reply",
            $"/student/qa/image?sessionId={session.Id}");
    }

    private static List<string>? DeserializeJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static (QAMessage? User, QAMessage? Assistant) ResolveRequestedReviewPair(VisualQASession session)
    {
        var assistant = session.RequestedReviewMessageId.HasValue
            ? session.Messages
                .Where(m => m.Role == "Assistant" && m.Id == session.RequestedReviewMessageId.Value)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault()
            : session.Messages
                .Where(m => m.Role == "Assistant")
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();
        if (assistant == null)
            return (null, null);

        var user = session.Messages
            .Where(m => m.Role == "User" && m.CreatedAt <= assistant.CreatedAt)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();
        return (user, assistant);
    }

    private static void EnsureSelectedPairConsistency(VisualQASession session, QAMessage? user, QAMessage? assistant)
    {
        if (!session.RequestedReviewMessageId.HasValue)
            return;

        if (assistant == null || assistant.Id != session.RequestedReviewMessageId.Value || user == null)
            throw new ConflictException("Cannot escalate this session because the selected review pair is inconsistent.");
    }

    private static IReadOnlyList<BoneVisQA.Services.Models.VisualQA.CitationItemDto> ResolveLecturerCitations(QAMessage? assistantMessage)
    {
        if (assistantMessage == null)
            return Array.Empty<BoneVisQA.Services.Models.VisualQA.CitationItemDto>();

        var fromJson = VisualQaCitationMetadataBuilder.DeserializeMany(assistantMessage.CitationsJson);
        if (fromJson.Count > 0)
            return fromJson.Take(5).ToList();

        return assistantMessage.Citations
            .OrderBy(c => c.Chunk?.ChunkOrder ?? int.MaxValue)
            .ThenBy(c => c.Id)
            .Select(c => VisualQaCitationMetadataBuilder.FromDocumentChunk(c.Chunk))
            .Take(5)
            .ToList();
    }

    private static string? ResolveSessionImageUrl(VisualQASession session)
    {
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

    private static bool CanTransitionFrom(string currentStatus, string targetStatus)
    {
        if (string.Equals(targetStatus, "EscalatedToExpert", StringComparison.Ordinal))
        {
            // Keep in sync with LecturerService.CanTransitionFrom (Visual QA triage / respond flows).
            return string.Equals(currentStatus, "PendingExpertReview", StringComparison.Ordinal)
                   || string.Equals(currentStatus, "Active", StringComparison.Ordinal)
                   || string.Equals(currentStatus, "LecturerApproved", StringComparison.Ordinal);
        }

        if (string.Equals(targetStatus, "Rejected", StringComparison.Ordinal))
        {
            return !string.Equals(currentStatus, "ExpertApproved", StringComparison.Ordinal)
                   && !string.Equals(currentStatus, "EscalatedToExpert", StringComparison.Ordinal);
        }

        return true;
    }
}
