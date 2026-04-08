using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerTriageService : ILecturerTriageService
{
    private readonly IUnitOfWork _unitOfWork;

    public LecturerTriageService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<EscalatedAnswerDto> EscalateAnswerAsync(Guid lecturerId, Guid answerId, EscalateAnswerRequestDto? request)
    {
        var answer = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question)
                .ThenInclude(q => q.Student)
            .Include(a => a.Question)
                .ThenInclude(q => q.Case)
            .FirstOrDefaultAsync(a => a.Id == answerId)
            ?? throw new KeyNotFoundException("Không tìm thấy câu trả lời cần chuyển tuyến.");

        var classEnrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e =>
                e.StudentId == answer.Question.StudentId &&
                e.Class.LecturerId == lecturerId);

        if (classEnrollment == null)
            throw new InvalidOperationException("Giảng viên không có quyền chuyển tuyến câu trả lời này.");

        if (!classEnrollment.Class.ExpertId.HasValue)
            throw new InvalidOperationException("Lớp hiện chưa được gán chuyên gia để tiếp nhận escalation.");

        if (CaseAnswerStatuses.IsEscalatedToExpert(answer.Status))
            throw new ConflictException("Câu trả lời này đã được chuyển tuyến trước đó.");

        if (!CaseAnswerStatuses.CanEscalateFromLecturer(answer.Status))
            throw new InvalidOperationException(
                "Chỉ có thể chuyển tuyến khi câu trả lời đang chờ giảng viên xem xét (Pending / RequiresLecturerReview / Rejected).");

        answer.Status = CaseAnswerStatuses.EscalatedToExpert;
        answer.EscalatedById = lecturerId;
        answer.EscalatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request?.ReviewNote))
        {
            var existingReview = await _unitOfWork.Context.ExpertReviews
                .FirstOrDefaultAsync(r =>
                    r.AnswerId == answerId &&
                    r.ExpertId == classEnrollment.Class.ExpertId.Value);

            if (existingReview == null)
            {
                existingReview = new ExpertReview
                {
                    Id = Guid.NewGuid(),
                    AnswerId = answerId,
                    ExpertId = classEnrollment.Class.ExpertId.Value,
                    ReviewNote = request.ReviewNote,
                    Action = "Escalated",
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.ExpertReviewRepository.AddAsync(existingReview);
            }
            else
            {
                existingReview.ReviewNote = request.ReviewNote;
                existingReview.Action = "Escalated";
                await _unitOfWork.ExpertReviewRepository.UpdateAsync(existingReview);
            }
        }

        await _unitOfWork.CaseAnswerRepository.UpdateAsync(answer);
        await _unitOfWork.SaveAsync();

        return new EscalatedAnswerDto
        {
            AnswerId = answer.Id,
            QuestionId = answer.QuestionId,
            StudentId = answer.Question.StudentId,
            StudentName = answer.Question.Student?.FullName ?? string.Empty,
            StudentEmail = answer.Question.Student?.Email ?? string.Empty,
            CaseId = answer.Question.CaseId,
            CaseTitle = answer.Question.Case?.Title ?? string.Empty,
            QuestionText = answer.Question.QuestionText,
            CurrentAnswerText = answer.AnswerText,
            StructuredDiagnosis = answer.StructuredDiagnosis,
            DifferentialDiagnoses = answer.DifferentialDiagnoses,
            Status = answer.Status,
            EscalatedById = answer.EscalatedById,
            EscalatedAt = answer.EscalatedAt,
            AiConfidenceScore = answer.AiConfidenceScore,
            ClassId = classEnrollment.ClassId,
            ClassName = classEnrollment.Class?.ClassName ?? string.Empty,
            ReviewNote = request?.ReviewNote
        };
    }
}
