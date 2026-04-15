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
            ?? throw new KeyNotFoundException("Không tìm thấy phiên hỏi đáp cần chuyển tuyến.");

        var classEnrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.LecturerId == lecturerId);

        if (classEnrollment == null)
            throw new InvalidOperationException("Giảng viên không có quyền chuyển tuyến câu trả lời này.");

        if (!classEnrollment.Class.ExpertId.HasValue)
            throw new InvalidOperationException("Lớp hiện chưa được gán chuyên gia để tiếp nhận escalation.");

        if (string.Equals(session.Status, "EscalatedToExpert", StringComparison.Ordinal))
            throw new ConflictException("Phiên hỏi đáp này đã được chuyển tuyến trước đó.");

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

        return new EscalatedAnswerDto
        {
            AnswerId = session.Id,
            QuestionId = latestUser?.Id ?? Guid.Empty,
            StudentId = session.StudentId,
            StudentName = session.Student?.FullName ?? string.Empty,
            StudentEmail = session.Student?.Email ?? string.Empty,
            CaseId = session.CaseId,
            CaseTitle = session.Case?.Title ?? string.Empty,
            QuestionText = latestUser?.Content ?? string.Empty,
            CurrentAnswerText = latestAssistant?.Content,
            StructuredDiagnosis = latestAssistant?.SuggestedDiagnosis,
            DifferentialDiagnoses = DeserializeJsonArray(latestAssistant?.DifferentialDiagnoses),
            Status = session.Status,
            EscalatedById = lecturerId,
            EscalatedAt = session.UpdatedAt,
            AiConfidenceScore = latestAssistant?.AiConfidenceScore,
            ClassId = classEnrollment.ClassId,
            ClassName = classEnrollment.Class?.ClassName ?? string.Empty,
            ReviewNote = request?.ReviewNote
        };
    }

    public async Task RejectAnswerAsync(Guid lecturerId, Guid sessionId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Lý do từ chối là bắt buộc.");

        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Không tìm thấy phiên hỏi đáp cần từ chối.");

        var classEnrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == session.StudentId &&
                e.Class.LecturerId == lecturerId);

        if (classEnrollment == null)
            throw new InvalidOperationException("Giảng viên không có quyền từ chối phiên hỏi đáp này.");

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
}
