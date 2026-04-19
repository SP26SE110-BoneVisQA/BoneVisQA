using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
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

        var totalQuestions = await _unitOfWork.Context.QaMessages
            .CountAsync(m => m.Role == "User" && studentIds.Contains(m.Session.StudentId));

        var escalatedItems = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question)
            .CountAsync(a =>
                a.Question != null &&
                studentIds.Contains(a.Question.StudentId) &&
                (a.Status == CaseAnswerStatuses.EscalatedToExpert || a.Status == CaseAnswerStatuses.Escalated));

        var pendingReviews = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question)
            .CountAsync(a =>
                a.Question != null &&
                studentIds.Contains(a.Question.StudentId) &&
                (a.Status == CaseAnswerStatuses.EscalatedToExpert
                 || a.Status == CaseAnswerStatuses.Escalated
                 || (
                     a.Status != CaseAnswerStatuses.Approved
                     && a.Status != CaseAnswerStatuses.Revised
                     && a.Status != CaseAnswerStatuses.Edited
                     && a.Status != CaseAnswerStatuses.ExpertApproved
                     && (a.AiConfidenceScore == null
                         || a.AiConfidenceScore < LecturerTriageThresholds.MinConfidenceToBypassTriage))));

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
            throw new KeyNotFoundException("No class under this lecturer was found.");

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

        var questionCounts = await _unitOfWork.Context.QaMessages
            .Where(m => m.Role == "User" && studentIds.Contains(m.Session.StudentId))
            .GroupBy(m => m.Session.StudentId)
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

    public async Task<LecturerAnalyticsDto> GetAnalyticsAsync(Guid lecturerId)
    {
        var classes = await _unitOfWork.Context.AcademicClasses
            .Where(c => c.LecturerId == lecturerId)
            .ToListAsync();

        var classIds = classes.Select(c => c.Id).ToList();

        // ── Class Performance ─────────────────────────────────────────────────
        var classPerformance = new List<ClassPerformanceDto>();
        foreach (var cls in classes)
        {
            var studentIds = await _unitOfWork.Context.ClassEnrollments
                .Where(e => e.ClassId == cls.Id)
                .Select(e => e.StudentId)
                .ToListAsync();

            var casesViewed = await _unitOfWork.Context.CaseViewLogs
                .Where(v => studentIds.Contains(v.StudentId))
                .CountAsync();

            var questions = await _unitOfWork.Context.QaMessages
                .Where(m => m.Role == "User" && studentIds.Contains(m.Session.StudentId))
                .CountAsync();

            var escalated = await _unitOfWork.Context.CaseAnswers
                .Include(a => a.Question)
                .CountAsync(a =>
                    a.Question != null && studentIds.Contains(a.Question.StudentId) &&
                    a.Status == "Escalated");

            var quizScores = await _unitOfWork.Context.QuizAttempts
                .Where(a => studentIds.Contains(a.StudentId) && a.Score.HasValue)
                .Where(a => _unitOfWork.Context.ClassQuizSessions.Any(cqs =>
                    cqs.ClassId == cls.Id && cqs.QuizId == a.QuizId))
                .Select(a => a.Score!.Value)
                .ToListAsync();

            var avgScore = quizScores.Count > 0 ? quizScores.Average() : (double?)null;
            var completionRate = studentIds.Count > 0
                ? (int)Math.Round(casesViewed / (double)Math.Max(1, studentIds.Count * 5) * 100)
                : 0;

            classPerformance.Add(new ClassPerformanceDto
            {
                ClassId = cls.Id,
                ClassName = cls.ClassName,
                Semester = cls.Semester ?? string.Empty,
                StudentCount = studentIds.Count,
                TotalCasesViewed = casesViewed,
                AvgQuizScore = avgScore,
                CompletionRate = Math.Min(100, completionRate),
                TrendPercent = 0,
                TotalQuestions = questions,
                EscalatedCount = escalated
            });
        }

        // ── Topic Scores ──────────────────────────────────────────────────────
        var topicScores = new List<TopicScoreDto>();

        var quizzesWithTopic = await _unitOfWork.Context.Quizzes
            .Where(q => q.Topic != null && q.Topic != "")
            .Select(q => new { q.Id, q.Topic })
            .ToListAsync();

        var quizIdsByTopic = quizzesWithTopic
            .GroupBy(q => q.Topic!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        foreach (var (topic, quizIds) in quizIdsByTopic)
        {
            var attempts = await _unitOfWork.Context.QuizAttempts
                .Where(a => quizIds.Contains(a.QuizId) && a.Score.HasValue)
                .ToListAsync();

            if (attempts.Count == 0) continue;

            var avgScore = attempts.Average(a => a.Score!.Value);
            var commonErrors = new List<string>();

            var belowAvg = attempts.Where(a => a.Score!.Value < avgScore).ToList();
            if (belowAvg.Count > 0)
                commonErrors.Add("Score below class average — review key concepts");

            if (avgScore < 60)
                commonErrors.Add("Low topic mastery — recommend additional study materials");

            topicScores.Add(new TopicScoreDto
            {
                Topic = topic,
                AvgScore = Math.Round(avgScore, 1),
                Attempts = attempts.Count,
                CommonErrors = commonErrors.Take(2).ToArray()
            });
        }

        // ── Top & Bottom Students (across all lecturer classes) ──────────────
        var allStudentIds = await _unitOfWork.Context.ClassEnrollments
            .Where(e => classIds.Contains(e.ClassId))
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync();

        var studentScores = new Dictionary<Guid, (string name, double? avg, int cases, int questions)>();

        foreach (var sid in allStudentIds)
        {
            var name = await _unitOfWork.Context.Users
                .Where(u => u.Id == sid)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();

            var cases = await _unitOfWork.Context.CaseViewLogs
                .CountAsync(v => v.StudentId == sid);

            var questions = await _unitOfWork.Context.QaMessages
                .CountAsync(m => m.Role == "User" && m.Session.StudentId == sid);

            var scores = await _unitOfWork.Context.QuizAttempts
                .Where(a => a.StudentId == sid && a.Score.HasValue)
                .Where(a => _unitOfWork.Context.ClassQuizSessions.Any(cqs =>
                    classIds.Contains(cqs.ClassId) && cqs.QuizId == a.QuizId))
                .Select(a => a.Score!.Value)
                .ToListAsync();

            studentScores[sid] = (name ?? "Unknown", scores.Count > 0 ? scores.Average() : null, cases, questions);
        }

        var rankedStudents = studentScores
            .Select(kv => new ClassLeaderboardItemDto
            {
                StudentId = kv.Key,
                StudentName = kv.Value.name,
                AverageQuizScore = kv.Value.avg,
                TotalCasesViewed = kv.Value.cases,
                TotalQuestionsAsked = kv.Value.questions
            })
            .OrderByDescending(s => s.AverageQuizScore ?? double.MinValue)
            .ThenByDescending(s => s.TotalCasesViewed)
            .ToList();

        var topStudents = rankedStudents.Take(5).ToList();
        var bottomStudents = rankedStudents
            .Where(s => s.AverageQuizScore < 60)
            .Take(5)
            .ToList();

        return new LecturerAnalyticsDto
        {
            ClassPerformance = classPerformance,
            TopicScores = topicScores.OrderByDescending(t => t.AvgScore).ToList(),
            TopStudents = topStudents,
            BottomStudents = bottomStudents
        };
    }
}
