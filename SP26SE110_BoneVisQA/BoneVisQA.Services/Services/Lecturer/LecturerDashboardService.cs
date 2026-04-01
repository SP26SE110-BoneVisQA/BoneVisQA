using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerDashboardService : ILecturerDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public LecturerDashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<LecturerDashboardStatsDto> GetDashboardStatsAsync(Guid lecturerId)
    {
        var classIds = await _unitOfWork.Context.AcademicClasses
            .Where(c => c.LecturerId == lecturerId)
            .Select(c => c.Id)
            .ToListAsync();

        var studentIds = await _unitOfWork.Context.ClassEnrollments
            .Where(e => classIds.Contains(e.ClassId))
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync();

        var totalQuestions = await _unitOfWork.Context.StudentQuestions
            .CountAsync(q => studentIds.Contains(q.StudentId));

        var escalatedItems = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question)
            .CountAsync(a =>
                a.Question != null &&
                studentIds.Contains(a.Question.StudentId) &&
                a.Status == "Escalated");

        var pendingReviews = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question)
            .CountAsync(a =>
                a.Question != null &&
                studentIds.Contains(a.Question.StudentId) &&
                (a.Status == "Pending" || a.Status == "Escalated"));

        var avgQuizScore = await _unitOfWork.Context.QuizAttempts
            .Where(a => studentIds.Contains(a.StudentId) && a.Score.HasValue)
            .Where(a => _unitOfWork.Context.ClassQuizSessions.Any(cqs =>
                classIds.Contains(cqs.ClassId) &&
                cqs.QuizId == a.QuizId))
            .AverageAsync(a => (double?)a.Score);

        return new LecturerDashboardStatsDto
        {
            TotalClasses = classIds.Count,
            TotalStudents = studentIds.Count,
            TotalQuestions = totalQuestions,
            EscalatedItems = escalatedItems,
            PendingReviews = pendingReviews,
            AverageQuizScore = avgQuizScore
        };
    }

    public async Task<IReadOnlyList<ClassLeaderboardItemDto>> GetClassLeaderboardAsync(Guid lecturerId, Guid classId)
    {
        var classExists = await _unitOfWork.Context.AcademicClasses
            .AnyAsync(c => c.Id == classId && c.LecturerId == lecturerId);

        if (!classExists)
            throw new KeyNotFoundException("Không tìm thấy lớp học thuộc quyền giảng viên.");

        var students = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.ClassId == classId)
            .Select(e => new
            {
                e.StudentId,
                StudentName = e.Student.FullName
            })
            .Distinct()
            .ToListAsync();

        var studentIds = students.Select(s => s.StudentId).ToList();

        var caseViewCounts = await _unitOfWork.Context.CaseViewLogs
            .Where(v => studentIds.Contains(v.StudentId))
            .GroupBy(v => v.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count);

        var questionCounts = await _unitOfWork.Context.StudentQuestions
            .Where(q => studentIds.Contains(q.StudentId))
            .GroupBy(q => q.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count);

        var quizScores = await _unitOfWork.Context.QuizAttempts
            .Where(a => studentIds.Contains(a.StudentId) && a.Score.HasValue)
            .Where(a => _unitOfWork.Context.ClassQuizSessions.Any(cqs =>
                cqs.ClassId == classId &&
                cqs.QuizId == a.QuizId))
            .GroupBy(a => a.StudentId)
            .Select(g => new { StudentId = g.Key, Average = g.Average(x => x.Score) })
            .ToDictionaryAsync(x => x.StudentId, x => x.Average);

        return students
            .Select(s => new ClassLeaderboardItemDto
            {
                StudentId = s.StudentId,
                StudentName = s.StudentName,
                TotalCasesViewed = caseViewCounts.GetValueOrDefault(s.StudentId),
                AverageQuizScore = quizScores.GetValueOrDefault(s.StudentId),
                TotalQuestionsAsked = questionCounts.GetValueOrDefault(s.StudentId)
            })
            .OrderByDescending(x => x.AverageQuizScore ?? double.MinValue)
            .ThenByDescending(x => x.TotalQuestionsAsked)
            .ThenBy(x => x.StudentName)
            .ToList();
    }
}
