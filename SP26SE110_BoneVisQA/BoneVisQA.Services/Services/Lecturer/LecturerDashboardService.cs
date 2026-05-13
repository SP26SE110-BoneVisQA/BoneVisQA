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

    #region Student Progress Methods

    public async Task<StudentProgressSummaryDto> GetClassStudentProgressAsync(Guid classId)
    {
        var academicClass = await _unitOfWork.Context.AcademicClasses
            .Include(c => c.Lecturer)
            .Include(c => c.Expert)
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (academicClass == null)
            throw new KeyNotFoundException("Class not found.");

        var students = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.ClassId == classId)
            .Include(e => e.Student)
            .ToListAsync();

        var classCases = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId)
            .CountAsync();

        var classQuizzes = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => cqs.ClassId == classId)
            .Select(cqs => cqs.QuizId)
            .Distinct()
            .ToListAsync();

        var studentProgressList = new List<StudentProgressItemDto>();
        double totalAverageScore = 0;
        int scoreCount = 0;

        foreach (var enrollment in students)
        {
            var studentId = enrollment.StudentId;

            var quizAttempts = await _unitOfWork.Context.QuizAttempts
                .Where(a => a.StudentId == studentId && a.Score.HasValue && classQuizzes.Contains(a.QuizId))
                .ToListAsync();

            var averageScore = quizAttempts.Count > 0 ? quizAttempts.Average(a => a.Score!.Value) : 0;
            var completedQuizzes = quizAttempts.Count;
            totalAverageScore += averageScore;
            scoreCount++;

            var casesViewed = await _unitOfWork.Context.CaseViewLogs
                .CountAsync(v => v.StudentId == studentId && v.ClassId == classId);

            var competencyEntities = await _unitOfWork.Context.StudentCompetencies
                .Where(sc => sc.StudentId == studentId)
                .Include(sc => sc.BoneSpecialty)
                .Take(5)
                .ToListAsync();

            var competencies = competencyEntities
                .Select(sc => new CompetencyScoreDto
                {
                    CompetencyId = sc.Id,
                    CompetencyName = sc.BoneSpecialty != null ? sc.BoneSpecialty.Name : "Unknown",
                    Score = (double)sc.Score,
                    MaxScore = 100,
                    Percentage = (double)sc.Score
                })
                .ToList();

            var lastActivity = await _unitOfWork.Context.CaseViewLogs
                .Where(v => v.StudentId == studentId)
                .OrderByDescending(v => v.ViewedAt)
                .Select(v => (DateTime?)v.ViewedAt)
                .FirstOrDefaultAsync();

            var overallProgress = classCases > 0
                ? (double)casesViewed / classCases * 100
                : 0;

            var progressStatus = overallProgress switch
            {
                0 => "NotStarted",
                < 30 => "InProgress",
                < 70 => "Halfway",
                < 100 => "AlmostDone",
                _ => "Completed"
            };

            studentProgressList.Add(new StudentProgressItemDto
            {
                StudentId = studentId,
                StudentName = enrollment.Student?.FullName,
                Email = enrollment.Student?.Email,
                AvatarUrl = enrollment.Student?.AvatarUrl,
                AverageScore = Math.Round(averageScore, 1),
                QuizzesCompleted = completedQuizzes,
                TotalQuizzes = classQuizzes.Count,
                CasesViewed = casesViewed,
                TotalCases = classCases,
                OverallProgress = Math.Round(overallProgress, 1),
                ProgressStatus = progressStatus,
                Competencies = competencies,
                LastActivity = lastActivity
            });
        }

        var classAverageScore = scoreCount > 0 ? totalAverageScore / scoreCount : 0;
        var classAverageProgress = students.Count > 0
            ? studentProgressList.Average(s => s.OverallProgress)
            : 0;

        return new StudentProgressSummaryDto
        {
            ClassId = classId,
            ClassName = academicClass.ClassName,
            TotalStudents = students.Count,
            ActiveStudents = students.Count(s => studentProgressList.Any(p => p.StudentId == s.StudentId && p.OverallProgress > 0)),
            Students = studentProgressList.OrderByDescending(s => s.AverageScore).ToList(),
            Overview = new ClassProgressOverviewDto
            {
                ClassAverageScore = Math.Round(classAverageScore, 1),
                ClassAverageProgress = Math.Round(classAverageProgress, 1),
                TotalQuizzes = classQuizzes.Count,
                TotalCases = classCases,
                QuizCompletionRate = classQuizzes.Count > 0
                    ? Math.Round(studentProgressList.Average(s => s.TotalQuizzes > 0 ? (double)s.QuizzesCompleted / s.TotalQuizzes * 100 : 0), 1)
                    : 0,
                CaseCompletionRate = classCases > 0
                    ? Math.Round(studentProgressList.Average(s => s.TotalCases > 0 ? s.OverallProgress : 0), 1)
                    : 0,
                CalculatedAt = DateTime.UtcNow
            }
        };
    }

    public async Task<StudentProgressDetailDto?> GetStudentProgressDetailAsync(Guid classId, Guid studentId)
    {
        var enrollment = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Student)
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.ClassId == classId && e.StudentId == studentId);

        if (enrollment == null)
            return null;

        var student = enrollment.Student!;
        var @class = enrollment.Class!;

        var classQuizzes = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => cqs.ClassId == classId)
            .Select(cqs => cqs.QuizId)
            .Distinct()
            .ToListAsync();

        var quizAttempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.StudentId == studentId && classQuizzes.Contains(a.QuizId))
            .Include(a => a.Quiz)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync();

        var completedAttempts = quizAttempts.Where(a => a.Score.HasValue).ToList();
        var quizScores = completedAttempts.Select(a => new QuizScoreItemDto
        {
            QuizId = a.QuizId,
            QuizTitle = a.Quiz?.Title,
            Topic = a.Quiz?.Topic,
            Score = a.Score ?? 0,
            MaxScore = 100,
            Percentage = a.Score ?? 0,
            CompletedAt = a.CompletedAt,
            IsPassed = (a.Score ?? 0) >= 60
        }).ToList();

        var classCases = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId)
            .Select(cc => cc.CaseId)
            .ToListAsync();

        var caseViews = await _unitOfWork.Context.CaseViewLogs
            .Where(v => v.StudentId == studentId && classCases.Contains(v.CaseId))
            .Include(v => v.Case)
            .GroupBy(v => v.CaseId)
            .Select(g => new
            {
                CaseId = g.Key,
                Case = g.OrderByDescending(v => v.ViewedAt).FirstOrDefault().Case,
                ViewCount = g.Count(),
                LastViewedAt = g.Max(v => v.ViewedAt),
                IsCompleted = g.Any(v => v.IsCompleted ?? false)
            })
            .OrderByDescending(x => x.LastViewedAt)
            .Take(10)
            .ToListAsync();

        var competencies = await _unitOfWork.Context.StudentCompetencies
            .Where(sc => sc.StudentId == studentId)
            .Include(sc => sc.BoneSpecialty)
            .ToListAsync();

        var competencyDtos = competencies
            .Select(sc => new CompetencyScoreDto
            {
                CompetencyId = sc.Id,
                CompetencyName = sc.BoneSpecialty != null ? sc.BoneSpecialty.Name : "Unknown",
                Score = (double)sc.Score,
                MaxScore = 100,
                Percentage = (double)sc.Score,
                Level = sc.Score switch
                {
                    >= 80 => "Expert",
                    >= 60 => "Proficient",
                    >= 40 => "Intermediate",
                    >= 20 => "Beginner",
                    _ => "Novice"
                }
            })
            .ToList();

        var recentActivities = new List<RecentActivityDto>();

        foreach (var attempt in quizAttempts.Take(5))
        {
            recentActivities.Add(new RecentActivityDto
            {
                ActivityId = attempt.Id,
                ActivityType = "Quiz",
                Description = $"Completed quiz: {attempt.Quiz?.Title ?? "Unknown"}",
                Timestamp = attempt.CompletedAt ?? attempt.StartedAt ?? DateTime.UtcNow,
                Score = attempt.Score
            });
        }

        foreach (var view in caseViews.Take(5))
        {
            recentActivities.Add(new RecentActivityDto
            {
                ActivityId = view.CaseId, // Use CaseId as unique identifier for activity
                ActivityType = "CaseView",
                Description = $"Viewed case: {view.Case?.Title ?? "Unknown"} ({view.ViewCount} times)",
                Timestamp = view.LastViewedAt ?? DateTime.UtcNow
            });
        }

        var overallCompetency = competencyDtos.Count > 0 ? competencyDtos.Average(c => c.Percentage) : 0;

        return new StudentProgressDetailDto
        {
            StudentId = studentId,
            StudentName = student.FullName,
            Email = student.Email,
            ClassId = classId,
            EnrolledAt = enrollment.EnrolledAt ?? DateTime.UtcNow,
            LastActivity = recentActivities.FirstOrDefault()?.Timestamp,
            QuizProgress = new QuizProgressDetailDto
            {
                TotalQuizzes = classQuizzes.Count,
                CompletedQuizzes = completedAttempts.Count,
                PendingQuizzes = classQuizzes.Count - completedAttempts.Count,
                AverageScore = completedAttempts.Count > 0 ? completedAttempts.Average(a => a.Score ?? 0) : 0,
                HighestScore = completedAttempts.Count > 0 ? completedAttempts.Max(a => a.Score ?? 0) : 0,
                LowestScore = completedAttempts.Count > 0 ? completedAttempts.Min(a => a.Score ?? 0) : 0,
                QuizScores = quizScores
            },
            CaseProgress = new CaseProgressDetailDto
            {
                TotalAssignedCases = classCases.Count,
                ViewedCases = caseViews.Count,
                CompletedCases = caseViews.Count(v => v.IsCompleted),
                CompletionRate = classCases.Count > 0 ? (double)caseViews.Count / classCases.Count * 100 : 0,
                RecentCases = caseViews.Select(v => new CaseViewItemDto
                {
                    CaseId = v.CaseId,
                    CaseTitle = v.Case?.Title,
                    CaseImageUrl = null,
                    ViewedAt = v.LastViewedAt,
                    ViewCount = v.ViewCount,
                    IsCompleted = v.IsCompleted
                }).ToList()
            },
            CompetencyDetail = new CompetencyDetailDto
            {
                OverallCompetency = overallCompetency,
                Competencies = competencyDtos,
                TopicMasteries = competencyDtos.Select(c => new TopicMasteryDto
                {
                    TopicId = c.CompetencyId,
                    TopicName = c.CompetencyName,
                    MasteryScore = c.Score,
                    MaxScore = c.MaxScore,
                    MasteryPercentage = c.Percentage,
                    MasteryLevel = c.Level
                }).ToList(),
                History = new List<CompetencyHistoryDto>()
            },
            RecentActivities = recentActivities.OrderByDescending(a => a.Timestamp).Take(10).ToList()
        };
    }

    public async Task<ClassCompetencyOverviewDto> GetClassCompetencyOverviewAsync(Guid classId)
    {
        var academicClass = await _unitOfWork.Context.AcademicClasses
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (academicClass == null)
            throw new KeyNotFoundException("Class not found.");

        var students = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var allCompetencies = await _unitOfWork.Context.StudentCompetencies
            .Where(sc => students.Contains(sc.StudentId))
            .Include(sc => sc.BoneSpecialty)
            .ToListAsync();

        var classCompetencies = allCompetencies
            .GroupBy(c => c.BoneSpecialty?.Name ?? "Unknown")
            .Select(g => new CompetencyScoreDto
            {
                CompetencyId = g.First().Id,
                CompetencyName = g.Key,
                Score = (double)g.Average(c => c.Score),
                MaxScore = 100,
                Percentage = Math.Round((double)g.Average(c => c.Score), 1),
                Level = g.Average(c => c.Score) switch
                {
                    >= 80 => "Expert",
                    >= 60 => "Proficient",
                    >= 40 => "Intermediate",
                    >= 20 => "Beginner",
                    _ => "Novice"
                }
            })
            .OrderByDescending(c => c.Percentage)
            .ToList();

        var topicMasteries = classCompetencies.Select(comp => new TopicMasteryDto
        {
            TopicId = comp.CompetencyId,
            TopicName = comp.CompetencyName,
            MasteryScore = comp.Score,
            MaxScore = 100,
            MasteryPercentage = comp.Percentage,
            MasteryLevel = comp.Level,
            StudentsAssessed = allCompetencies.Count(c => c.BoneSpecialty?.Name == comp.CompetencyName),
            ClassAverage = comp.Percentage
        }).ToList();

        var competencyDistribution = new List<CompetencyDistributionDto>
        {
            new() { Level = "Expert", StudentCount = allCompetencies.Count(c => (double)c.Score >= 80), Percentage = 0 },
            new() { Level = "Proficient", StudentCount = allCompetencies.Count(c => (double)c.Score >= 60 && (double)c.Score < 80), Percentage = 0 },
            new() { Level = "Intermediate", StudentCount = allCompetencies.Count(c => (double)c.Score >= 40 && (double)c.Score < 60), Percentage = 0 },
            new() { Level = "Beginner", StudentCount = allCompetencies.Count(c => (double)c.Score >= 20 && (double)c.Score < 40), Percentage = 0 },
            new() { Level = "Novice", StudentCount = allCompetencies.Count(c => (double)c.Score < 20), Percentage = 0 }
        };

        if (students.Count > 0)
        {
            foreach (var dist in competencyDistribution)
            {
                dist.Percentage = Math.Round((double)dist.StudentCount / students.Count * 100, 1);
            }
        }

        var weakTopics = topicMasteries
            .Where(t => t.MasteryPercentage < 50)
            .Select(t => new WeakTopicDto
            {
                TopicName = t.TopicName,
                AverageScore = t.MasteryScore,
                StudentsNeedingHelp = students.Count / 2,
                Recommendation = $"Consider assigning more cases related to {t.TopicName}"
            })
            .ToList();

        var strongTopics = topicMasteries
            .Where(t => t.MasteryPercentage >= 70)
            .Select(t => new StrongTopicDto
            {
                TopicName = t.TopicName,
                AverageScore = t.MasteryScore,
                StudentsMastered = students.Count
            })
            .ToList();

        return new ClassCompetencyOverviewDto
        {
            ClassId = classId,
            ClassName = academicClass.ClassName,
            TotalStudents = students.Count,
            AverageCompetency = classCompetencies.Count > 0 ? Math.Round(classCompetencies.Average(c => c.Percentage), 1) : 0,
            ClassCompetencies = classCompetencies,
            TopicMasteries = topicMasteries,
            CompetencyDistribution = competencyDistribution,
            WeakTopics = weakTopics,
            StrongTopics = strongTopics
        };
    }

    public async Task<List<TopicMasteryDto>> GetClassTopicsMasteryAsync(Guid classId)
    {
        var students = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var competencies = await _unitOfWork.Context.StudentCompetencies
            .Where(sc => students.Contains(sc.StudentId))
            .Include(sc => sc.BoneSpecialty)
            .ToListAsync();

        return competencies
            .GroupBy(c => new { c.BoneSpecialtyId, Name = c.BoneSpecialty?.Name ?? "Unknown" })
            .Select(g => new TopicMasteryDto
            {
                TopicId = g.Key.BoneSpecialtyId ?? Guid.Empty,
                TopicName = g.Key.Name,
                Category = g.First().BoneSpecialty?.Parent?.Name,
                MasteryScore = (double)g.Average(c => c.Score),
                MaxScore = 100,
                MasteryPercentage = Math.Round((double)g.Average(c => c.Score), 1),
                MasteryLevel = g.Average(c => c.Score) switch
                {
                    >= 80 => "Expert",
                    >= 60 => "Proficient",
                    >= 40 => "Intermediate",
                    >= 20 => "Beginner",
                    _ => "Novice"
                },
                StudentsAssessed = g.Count(),
                ClassAverage = Math.Round((double)g.Average(c => c.Score), 1)
            })
            .OrderByDescending(t => t.MasteryPercentage)
            .ToList();
    }

    #endregion
}
