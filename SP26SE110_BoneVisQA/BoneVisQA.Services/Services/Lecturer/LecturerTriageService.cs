using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerTriageService : ILecturerTriageService
{
    private readonly IUnitOfWork _unitOfWork;

    public LecturerTriageService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<EscalatedAnswerDto> EscalateAnswerAsync(Guid lecturerId, Guid sessionId, EscalateAnswerRequestDto? request)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Student)
            .Include(s => s.Case)
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("The Q&A session to escalate was not found.");

        var classEnrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.LecturerId == lecturerId);

        if (classEnrollment == null)
            throw new InvalidOperationException("The lecturer does not have permission to escalate this answer.");

        if (!classEnrollment.Class.ExpertId.HasValue)
            throw new InvalidOperationException("This class has not been assigned an expert for escalation yet.");

        if (string.Equals(session.Status, "EscalatedToExpert", StringComparison.Ordinal))
            throw new ConflictException("This Q&A session has already been escalated.");

        session.Status = "EscalatedToExpert";
        session.ExpertId = classEnrollment.Class.ExpertId.Value;
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
            ReviewNote = request?.ReviewNote
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

        session.Status = "Rejected";
        session.LecturerId = lecturerId;
        session.UpdatedAt = DateTime.UtcNow;

        var rejectionMessage = new QAMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "Lecturer",
            Content = reason.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Context.QaMessages.AddAsync(rejectionMessage);
        await _unitOfWork.SaveAsync();
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
}
