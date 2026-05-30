using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerReportService : ILecturerReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LecturerReportService> _logger;

    public LecturerReportService(IUnitOfWork unitOfWork, ILogger<LecturerReportService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<LecturerOverallReportDto> GetOverallReportAsync(Guid lecturerId)
    {
        var classes = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.LecturerId == lecturerId)
            .Include(c => c.Lecturer)
            .ToListAsync();

        var classIds = classes.Select(c => c.Id).ToList();

        var totalStudents = await _unitOfWork.Context.ClassEnrollments
            .Where(e => classIds.Contains(e.ClassId))
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync();

        var quizSessionClassIds = await _unitOfWork.Context.ClassQuizSessions
            .Where(qs => classIds.Contains(qs.ClassId))
            .Select(qs => qs.Id)
            .ToListAsync();

        var quizIds = await _unitOfWork.Context.ClassQuizSessions
            .Where(qs => classIds.Contains(qs.ClassId))
            .Select(qs => qs.QuizId)
            .Distinct()
            .ToListAsync();

        var attempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => quizIds.Contains(a.QuizId))
            .ToListAsync();

        var sessionMap = await _unitOfWork.Context.ClassQuizSessions
            .Where(qs => classIds.Contains(qs.ClassId))
            .ToDictionaryAsync(qs => qs.QuizId, qs => qs.PassingScore ?? 50);

        var totalAttempts = attempts.Count;
        var avgScore = totalAttempts > 0 ? attempts.Average(a => a.Score ?? 0) : 0;
        var passRate = 0.0;
        if (totalAttempts > 0)
        {
            var passCount = 0;
            foreach (var a in attempts)
            {
                var passScore = sessionMap.TryGetValue(a.QuizId, out var ps) ? ps : 50;
                if (a.Score != null && a.Score >= passScore) passCount++;
            }
            passRate = passCount * 100.0 / totalAttempts;
        }

        var recentActivity = DateTime.UtcNow.AddDays(-30);
        var activeStudents = await _unitOfWork.Context.QuizAttempts
            .Where(a => quizIds.Contains(a.QuizId) && a.StartedAt >= recentActivity)
            .Select(a => a.StudentId)
            .Distinct()
            .CountAsync();

        return new LecturerOverallReportDto
        {
            LecturerId = lecturerId,
            LecturerName = classes.FirstOrDefault()?.Lecturer?.FullName ?? "Lecturer",
            TotalClasses = classes.Count,
            TotalStudents = totalStudents,
            TotalQuizzes = quizSessionClassIds.Count,
            TotalCases = await _unitOfWork.Context.ClassCases
                .Where(cc => classIds.Contains(cc.ClassId))
                .CountAsync(),
            AverageQuizScore = Math.Round(avgScore, 2),
            TotalQuizAttempts = totalAttempts,
            PassRate = Math.Round(passRate, 2),
            ActiveStudents = activeStudents,
            InactiveStudents = totalStudents - activeStudents,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<ClassReportDto>> GetClassesReportAsync(Guid lecturerId)
    {
        var classes = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.LecturerId == lecturerId)
            .ToListAsync();

        var result = new List<ClassReportDto>();

        foreach (var cls in classes)
        {
            var studentCount = await _unitOfWork.Context.ClassEnrollments
                .CountAsync(e => e.ClassId == cls.Id);

            var quizSessions = await _unitOfWork.Context.ClassQuizSessions
                .Where(qs => qs.ClassId == cls.Id)
                .ToListAsync();

            var quizIds = quizSessions.Select(qs => qs.QuizId).ToList();
            var sessionMap = quizSessions.ToDictionary(qs => qs.QuizId, qs => qs.PassingScore ?? 50);

            var attempts = await _unitOfWork.Context.QuizAttempts
                .Where(a => quizIds.Contains(a.QuizId))
                .ToListAsync();

            var avgScore = attempts.Count > 0 ? attempts.Average(a => a.Score ?? 0) : 0;
            var passCount = 0;
            foreach (var a in attempts)
            {
                var passScore = sessionMap.TryGetValue(a.QuizId, out var ps) ? ps : 50;
                if (a.Score != null && a.Score >= passScore) passCount++;
            }
            var passRate = attempts.Count > 0 ? passCount * 100.0 / attempts.Count : 0;

            result.Add(new ClassReportDto
            {
                ClassId = cls.Id,
                ClassName = cls.ClassName,
                Semester = cls.Semester ?? "",
                StudentCount = studentCount,
                QuizCount = quizSessions.Count,
                CaseCount = await _unitOfWork.Context.ClassCases
                    .CountAsync(cc => cc.ClassId == cls.Id),
                AverageScore = Math.Round(avgScore, 2),
                TotalAttempts = attempts.Count,
                PassRate = Math.Round(passRate, 2)
            });
        }

        return result;
    }

    public async Task<ClassDetailedReportDto> GetClassDetailedReportAsync(Guid classId)
    {
        var academicClass = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId)
            .Include(c => c.Lecturer)
            .Include(c => c.Expert)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Class not found.");

        var studentCount = await _unitOfWork.Context.ClassEnrollments
            .CountAsync(e => e.ClassId == classId);

        var quizSessions = await _unitOfWork.Context.ClassQuizSessions
            .Where(qs => qs.ClassId == classId)
            .Include(qs => qs.Quiz)
            .ToListAsync();

        var quizIds = quizSessions.Select(qs => qs.QuizId).ToList();
        var sessionMap = quizSessions.ToDictionary(qs => qs.QuizId, qs => qs.PassingScore ?? 50);

        var attempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => quizIds.Contains(a.QuizId))
            .ToListAsync();

        var avgScore = attempts.Count > 0 ? attempts.Average(a => a.Score ?? 0) : 0;
        var passCount = 0;
        foreach (var a in attempts)
        {
            var passScore = sessionMap.TryGetValue(a.QuizId, out var ps) ? ps : 50;
            if (a.Score != null && a.Score >= passScore) passCount++;
        }
        var passRate = attempts.Count > 0 ? passCount * 100.0 / attempts.Count : 0;

        var quizSummaries = quizSessions.Select(qs =>
        {
            var qsAttempts = attempts.Where(a => a.QuizId == qs.QuizId).ToList();
            var qsAvg = qsAttempts.Count > 0 ? qsAttempts.Average(a => a.Score ?? 0) : 0;
            var qsPassCount = 0;
            foreach (var a in qsAttempts)
            {
                if (a.Score != null && a.Score >= (qs.PassingScore ?? 50)) qsPassCount++;
            }
            var qsPassRate = qsAttempts.Count > 0 ? qsPassCount * 100.0 / qsAttempts.Count : 0;
            return new QuizSummaryDto
            {
                QuizId = qs.QuizId,
                QuizTitle = qs.Quiz?.Title ?? "Quiz",
                TotalAttempts = qsAttempts.Count,
                AverageScore = Math.Round(qsAvg, 2),
                PassRate = Math.Round(qsPassRate, 2)
            };
        }).ToList();

        var topStudents = await GetTopStudentsAsync(classId, 5);

        var caseIds = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId)
            .Select(cc => cc.CaseId)
            .ToListAsync();

        var totalQuestions = await _unitOfWork.Context.StudentQuestions
            .Where(q => q.CaseId.HasValue && caseIds.Contains(q.CaseId.Value))
            .CountAsync();

        var escalatedCount = await _unitOfWork.Context.CaseAnswers
            .Where(a => a.Question.CaseId.HasValue && caseIds.Contains(a.Question.CaseId.Value))
            .CountAsync(a => CaseAnswerStatuses.IsEscalatedToExpert(a.Status));

        return new ClassDetailedReportDto
        {
            ClassId = classId,
            ClassName = academicClass.ClassName,
            Semester = academicClass.Semester ?? "",
            LecturerName = academicClass.Lecturer?.FullName ?? "Lecturer",
            ExpertName = academicClass.Expert?.FullName,
            StudentCount = studentCount,
            QuizzesAssigned = quizSessions.Count,
            CasesAssigned = caseIds.Count,
            TotalQuizAttempts = attempts.Count,
            AverageScore = Math.Round(avgScore, 2),
            PassRate = Math.Round(passRate, 2),
            AverageQuestionsPerStudent = studentCount > 0 ? Math.Round((double)totalQuestions / studentCount, 2) : 0,
            TotalQuestionsAsked = totalQuestions,
            EscalatedAnswers = escalatedCount,
            QuizSummaries = quizSummaries,
            TopStudents = topStudents,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<StudentReportDto?> GetStudentReportAsync(Guid classId, Guid studentId)
    {
        var student = await _unitOfWork.UserRepository
            .FindByCondition(u => u.Id == studentId)
            .FirstOrDefaultAsync();

        if (student == null)
            return null;

        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.ClassId == classId);

        if (enrollment == null)
            return null;

        var quizSessions = await _unitOfWork.Context.ClassQuizSessions
            .Where(qs => qs.ClassId == classId)
            .ToListAsync();

        var quizIds = quizSessions.Select(qs => qs.QuizId).ToList();
        var sessionMap = quizSessions.ToDictionary(qs => qs.QuizId, qs => qs.PassingScore ?? 50);

        var attempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.StudentId == studentId && quizIds.Contains(a.QuizId))
            .ToListAsync();

        var avgScore = attempts.Count > 0 ? attempts.Average(a => a.Score ?? 0) : 0;
        var passCount = 0;
        foreach (var a in attempts)
        {
            var passScore = sessionMap.TryGetValue(a.QuizId, out var ps) ? ps : 50;
            if (a.Score != null && a.Score >= passScore) passCount++;
        }
        var passRate = attempts.Count > 0 ? passCount * 100.0 / attempts.Count : (double?)null;

        var lastActivity = attempts.Any()
            ? attempts.Max(a => a.StartedAt)
            : (await _unitOfWork.Context.CaseViewLogs
                .Where(v => v.StudentId == studentId && v.Case != null && v.Case.ClassCases.Any(cc => cc.ClassId == classId))
                .OrderByDescending(v => v.ViewedAt)
                .Select(v => (DateTime?)v.ViewedAt)
                .FirstOrDefaultAsync());

        var caseIds = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId)
            .Select(cc => cc.CaseId)
            .ToListAsync();

        var questionsAsked = await _unitOfWork.Context.StudentQuestions
            .Where(q => q.StudentId == studentId && q.CaseId.HasValue && caseIds.Contains(q.CaseId.Value))
            .CountAsync();

        var escalatedCount = await _unitOfWork.Context.CaseAnswers
            .Where(a => a.Question.StudentId == studentId && a.Question.CaseId.HasValue && caseIds.Contains(a.Question.CaseId.Value))
            .CountAsync(a => CaseAnswerStatuses.IsEscalatedToExpert(a.Status));

        var casesViewed = await _unitOfWork.Context.CaseViewLogs
            .Where(v => v.StudentId == studentId && v.Case != null && v.Case.ClassCases.Any(cc => cc.ClassId == classId))
            .CountAsync();

        var isActive = lastActivity.HasValue && lastActivity.Value >= DateTime.UtcNow.AddDays(-7);

        return new StudentReportDto
        {
            StudentId = studentId,
            StudentName = student.FullName ?? student.Email ?? "Student",
            StudentEmail = student.Email ?? "",
            QuizAttempts = attempts.Count,
            AverageScore = Math.Round(avgScore, 2),
            PassRate = passRate.HasValue ? Math.Round(passRate.Value, 2) : 0,
            CasesViewed = casesViewed,
            QuestionsAsked = questionsAsked,
            EscalatedQuestions = escalatedCount,
            LastActivityAt = lastActivity,
            ActivityStatus = isActive ? "Active" : "Inactive"
        };
    }

    public async Task<IReadOnlyList<StudentReportDto>> GetClassStudentsReportAsync(Guid classId)
    {
        var enrollments = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.ClassId == classId)
            .Include(e => e.Student)
            .ToListAsync();

        var result = new List<StudentReportDto>();

        foreach (var enrollment in enrollments)
        {
            if (enrollment.Student == null) continue;

            var studentReport = await GetStudentReportAsync(classId, enrollment.StudentId);
            if (studentReport != null)
            {
                result.Add(studentReport);
            }
            else
            {
                result.Add(new StudentReportDto
                {
                    StudentId = enrollment.StudentId,
                    StudentName = enrollment.Student.FullName ?? "Student",
                    StudentEmail = enrollment.Student.Email ?? "",
                    QuizAttempts = 0,
                    AverageScore = 0,
                    PassRate = 0,
                    CasesViewed = 0,
                    QuestionsAsked = 0,
                    EscalatedQuestions = 0,
                    ActivityStatus = "No Activity"
                });
            }
        }

        return result.OrderByDescending(s => s.AverageScore).ToList();
    }

    public async Task<QuizReportDto> GetQuizReportAsync(Guid quizSessionId)
    {
        var session = await _unitOfWork.Context.ClassQuizSessions
            .Where(qs => qs.Id == quizSessionId)
            .Include(qs => qs.Quiz)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Quiz session not found.");

        var attempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.QuizId == session.QuizId)
            .ToListAsync();

        var completedAttempts = attempts.Where(a => a.CompletedAt.HasValue).ToList();

        return new QuizReportDto
        {
            QuizId = session.QuizId,
            SessionId = quizSessionId,
            QuizTitle = session.Quiz?.Title ?? "Quiz",
            TotalAttempts = attempts.Count,
            CompletedAttempts = completedAttempts.Count,
            AverageScore = completedAttempts.Any() ? Math.Round(completedAttempts.Average(a => a.Score ?? 0), 2) : 0,
            PassRate = completedAttempts.Any()
                ? Math.Round(completedAttempts.Count(a => a.Score != null && a.Score >= (session.PassingScore ?? 50)) * 100.0 / completedAttempts.Count, 2)
                : 0,
            HighestScore = completedAttempts.Any() ? (int)completedAttempts.Max(a => a.Score ?? 0) : 0,
            LowestScore = completedAttempts.Any() ? (int)completedAttempts.Min(a => a.Score ?? 0) : 0,
            PassingScore = session.PassingScore ?? 50,
            OpenTime = session.OpenTime,
            CloseTime = session.CloseTime
        };
    }

    public async Task<IReadOnlyList<QuizReportDto>> GetClassQuizReportsAsync(Guid classId)
    {
        var sessions = await _unitOfWork.Context.ClassQuizSessions
            .Where(qs => qs.ClassId == classId)
            .Include(qs => qs.Quiz)
            .ToListAsync();

        var result = new List<QuizReportDto>();
        foreach (var session in sessions)
        {
            result.Add(await GetQuizReportAsync(session.Id));
        }

        return result;
    }

    public async Task<AIQualityReportDto> GetAIQualityReportAsync(Guid classId)
    {
        var academicClass = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Class not found.");

        var caseIds = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId)
            .Select(cc => cc.CaseId)
            .ToListAsync();

        var answers = await _unitOfWork.Context.CaseAnswers
            .Where(a => a.Question.CaseId.HasValue && caseIds.Contains(a.Question.CaseId.Value))
            .ToListAsync();

        var totalAnswers = answers.Count;
        var escalatedCount = answers.Count(a => CaseAnswerStatuses.IsEscalatedToExpert(a.Status));
        var approvedCount = answers.Count(a => a.Status == CaseAnswerStatuses.Approved || a.Status == CaseAnswerStatuses.ExpertApproved);
        var rejectedCount = answers.Count(a => a.Status == CaseAnswerStatuses.Rejected);
        var avgConfidence = answers.Where(a => a.AiConfidenceScore.HasValue).Any()
            ? answers.Where(a => a.AiConfidenceScore.HasValue).Average(a => a.AiConfidenceScore ?? 0)
            : 0;
        var autoApprovalRate = totalAnswers > 0 ? (totalAnswers - escalatedCount - approvedCount) * 100.0 / totalAnswers : 0;

        var distribution = new List<AIScoreDistributionDto>
        {
            new() { Range = "0-20%", Count = answers.Count(a => a.AiConfidenceScore.HasValue && a.AiConfidenceScore < 20), Percentage = 0 },
            new() { Range = "20-40%", Count = answers.Count(a => a.AiConfidenceScore is >= 20 and < 40), Percentage = 0 },
            new() { Range = "40-60%", Count = answers.Count(a => a.AiConfidenceScore is >= 40 and < 60), Percentage = 0 },
            new() { Range = "60-80%", Count = answers.Count(a => a.AiConfidenceScore is >= 60 and < 80), Percentage = 0 },
            new() { Range = "80-100%", Count = answers.Count(a => a.AiConfidenceScore >= 80), Percentage = 0 }
        };

        foreach (var d in distribution)
        {
            d.Percentage = totalAnswers > 0 ? Math.Round(d.Count * 100.0 / totalAnswers, 2) : 0;
        }

        return new AIQualityReportDto
        {
            ClassId = classId,
            ClassName = academicClass.ClassName,
            TotalAnswers = totalAnswers,
            AIAnswers = totalAnswers,
            EscalatedAnswers = escalatedCount,
            ApprovedByLecturer = approvedCount,
            RejectedAnswers = rejectedCount,
            AverageConfidenceScore = Math.Round(avgConfidence, 2),
            AutoApprovalRate = Math.Round(autoApprovalRate, 2),
            ScoreDistribution = distribution
        };
    }

    public async Task<ActivityReportDto> GetActivityReportAsync(Guid classId, DateTime? fromDate, DateTime? toDate)
    {
        var academicClass = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Class not found.");

        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        var enrollments = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.ClassId == classId)
            .ToListAsync();

        var quizSessions = await _unitOfWork.Context.ClassQuizSessions
            .Where(qs => qs.ClassId == classId)
            .Include(qs => qs.Quiz)
            .Where(qs => qs.OpenTime >= from && qs.OpenTime <= to)
            .ToListAsync();

        var quizIds = quizSessions.Select(qs => qs.QuizId).ToList();

        var recentActivity = DateTime.UtcNow.AddDays(-7);
        var currentlyActive = await _unitOfWork.Context.QuizAttempts
            .Where(a => quizIds.Contains(a.QuizId) && a.StartedAt >= recentActivity)
            .Select(a => a.StudentId)
            .Distinct()
            .CountAsync();

        var quizActivities = quizSessions.Select(qs => new QuizActivityDto
        {
            QuizId = qs.QuizId,
            QuizTitle = qs.Quiz != null ? qs.Quiz.Title : "Quiz",
            OpenTime = qs.OpenTime,
            CloseTime = qs.CloseTime,
            TotalAttempts = 0,
            CompletedAttempts = 0
        }).ToList();

        return new ActivityReportDto
        {
            ClassId = classId,
            ClassName = academicClass.ClassName,
            FromDate = from,
            ToDate = to,
            ActiveStudents = currentlyActive,
            InactiveStudents = enrollments.Count - currentlyActive,
            DailyActivities = new List<DailyActivityDto>(),
            QuizActivities = quizActivities
        };
    }

    private async Task<List<StudentPerformanceDto>> GetTopStudentsAsync(Guid classId, int count)
    {
        var enrollments = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.ClassId == classId)
            .Include(e => e.Student)
            .ToListAsync();

        var result = new List<StudentPerformanceDto>();

        foreach (var enrollment in enrollments)
        {
            if (enrollment.Student == null) continue;

            var quizSessions = await _unitOfWork.Context.ClassQuizSessions
                .Where(qs => qs.ClassId == classId)
                .ToListAsync();

            var quizIds = quizSessions.Select(qs => qs.QuizId).ToList();

            var attempts = await _unitOfWork.Context.QuizAttempts
                .Where(a => a.StudentId == enrollment.StudentId && quizIds.Contains(a.QuizId))
                .ToListAsync();

            var avgScore = attempts.Count > 0 ? attempts.Average(a => a.Score ?? 0) : 0;

            var caseIds = await _unitOfWork.Context.ClassCases
                .Where(cc => cc.ClassId == classId)
                .Select(cc => cc.CaseId)
                .ToListAsync();

            var questionsAsked = await _unitOfWork.Context.StudentQuestions
                .Where(q => q.StudentId == enrollment.StudentId && q.CaseId.HasValue && caseIds.Contains(q.CaseId.Value))
                .CountAsync();

            result.Add(new StudentPerformanceDto
            {
                StudentId = enrollment.StudentId,
                StudentName = enrollment.Student.FullName ?? "Student",
                AverageScore = Math.Round(avgScore, 2),
                QuizAttempts = attempts.Count,
                QuestionsAsked = questionsAsked
            });
        }

        return result.OrderByDescending(s => s.AverageScore).Take(count).ToList();
    }
}
