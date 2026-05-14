using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.Analytics;

public class LecturerAnalyticsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LecturerAnalyticsService> _logger;

    public LecturerAnalyticsService(IUnitOfWork unitOfWork, ILogger<LecturerAnalyticsService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public class ClassAnalyticsOverview
    {
        public Guid ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public double AverageScore { get; set; }
        public double CompletionRate { get; set; }
        public int AtRiskStudentCount { get; set; }
        public int TotalQuizzes { get; set; }
        public Dictionary<string, double> TopicAverages { get; set; } = new();
    }

    public class StudentAnalyticsDetail
    {
        public Guid StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public double AverageScore { get; set; }
        public int TotalQuizzesTaken { get; set; }
        public string MasteryLevel { get; set; } = "Beginner";
        public List<CompetencyItem> Competencies { get; set; } = new();
        public List<ErrorPatternItem> ErrorPatterns { get; set; } = new();
        public List<RecentQuizResult> RecentQuizzes { get; set; } = new();
        public bool IsAtRisk { get; set; }
    }

    public class CompetencyItem
    {
        public Guid? BoneSpecialtyId { get; set; }
        public string TopicName { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public string MasteryLevel { get; set; } = "Beginner";
        public int TotalAttempts { get; set; }
    }

    public class ErrorPatternItem
    {
        public Guid PatternId { get; set; }
        public string Topic { get; set; } = string.Empty;
        public int ErrorCount { get; set; }
        public bool IsResolved { get; set; }
    }

    public class RecentQuizResult
    {
        public Guid QuizId { get; set; }
        public string QuizTitle { get; set; } = string.Empty;
        public double? Score { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int CorrectAnswers { get; set; }
        public int TotalQuestions { get; set; }
    }

    public async Task<ClassAnalyticsOverview?> GetClassAnalyticsOverviewAsync(Guid classId)
    {
        var classEntity = await _unitOfWork.AcademicClassRepository
            .GetQueryable()
            .Include(c => c.ClassEnrollments)
                .ThenInclude(e => e.Student)
            .Include(c => c.ClassQuizSessions)
                .ThenInclude(s => s.Quiz)
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (classEntity == null) return null;

        var studentIds = classEntity.ClassEnrollments.Select(e => e.StudentId).ToList();
        
        var quizIds = classEntity.ClassQuizSessions.Select(s => s.QuizId).ToList();
        var allAttempts = await _unitOfWork.QuizAttemptRepository
            .GetQueryable()
            .Where(a => studentIds.Contains(a.StudentId) && quizIds.Contains(a.QuizId) && a.CompletedAt != null)
            .Include(a => a.Quiz)
            .ToListAsync();

        var activeStudentIds = allAttempts.Select(a => a.StudentId).Distinct().ToList();
        
        var avgScore = allAttempts.Any() ? allAttempts.Average(a => a.Score ?? 0) : 0;
        var completionRate = studentIds.Count > 0 ? (double)activeStudentIds.Count / studentIds.Count * 100 : 0;

        var atRiskCount = await GetAtRiskStudentCountAsync(classId, studentIds);

        var topicAverages = allAttempts
            .Where(a => a.Quiz != null && !string.IsNullOrEmpty(a.Quiz.Topic))
            .GroupBy(a => a.Quiz!.Topic!)
            .ToDictionary(
                g => g.Key,
                g => g.Average(a => a.Score ?? 0)
            );

        return new ClassAnalyticsOverview
        {
            ClassId = classId,
            ClassName = classEntity.ClassName ?? "Unknown",
            TotalStudents = studentIds.Count,
            ActiveStudents = activeStudentIds.Count,
            AverageScore = avgScore,
            CompletionRate = completionRate,
            AtRiskStudentCount = atRiskCount,
            TotalQuizzes = quizIds.Count,
            TopicAverages = topicAverages
        };
    }

    public async Task<List<StudentAnalyticsDetail>> GetClassStudentAnalyticsAsync(Guid classId)
    {
        var enrollments = await _unitOfWork.ClassEnrollmentRepository
            .GetQueryable()
            .Where(e => e.ClassId == classId)
            .Include(e => e.Student)
            .ToListAsync();

        var result = new List<StudentAnalyticsDetail>();

        foreach (var enrollment in enrollments)
        {
            var studentAnalytics = await GetStudentAnalyticsDetailAsync(enrollment.StudentId, classId);
            if (studentAnalytics != null)
            {
                result.Add(studentAnalytics);
            }
        }

        return result;
    }

    public async Task<StudentAnalyticsDetail?> GetStudentAnalyticsDetailAsync(Guid studentId, Guid? classId = null)
    {
        var student = await _unitOfWork.UserRepository.GetByIdAsync(studentId);
        if (student == null) return null;

        var query = _unitOfWork.QuizAttemptRepository
            .GetQueryable()
            .Where(a => a.StudentId == studentId && a.CompletedAt != null)
            .Include(a => a.Quiz)
            .AsQueryable();

        if (classId.HasValue)
        {
            var classQuizIds = await _unitOfWork.ClassQuizSessionRepository
                .GetQueryable()
                .Where(s => s.ClassId == classId.Value)
                .Select(s => s.QuizId)
                .ToListAsync();
            
            query = query.Where(a => classQuizIds.Contains(a.QuizId));
        }

        var attempts = await query.ToListAsync();

        var competencies = await _unitOfWork.StudentCompetencyRepository
            .GetQueryable()
            .Where(c => c.StudentId == studentId)
            .Include(c => c.BoneSpecialty)
            .ToListAsync();

        var errorPatterns = await _unitOfWork.ErrorPatternRepository
            .GetQueryable()
            .Where(e => e.StudentId == studentId && !e.IsResolved)
            .OrderByDescending(e => e.ErrorCount)
            .Take(5)
            .ToListAsync();

        var avgScore = attempts.Any() ? attempts.Average(a => a.Score ?? 0) : 0;
        var overallMastery = CalculateOverallMastery(competencies);

        return new StudentAnalyticsDetail
        {
            StudentId = studentId,
            StudentName = student.FullName ?? "Unknown",
            StudentEmail = student.Email ?? "",
            AverageScore = avgScore,
            TotalQuizzesTaken = attempts.Count,
            MasteryLevel = overallMastery,
            Competencies = competencies.Select(c => new CompetencyItem
            {
                BoneSpecialtyId = c.BoneSpecialtyId,
                TopicName = c.BoneSpecialty?.Name ?? "Unknown",
                Score = c.Score,
                MasteryLevel = c.MasteryLevel,
                TotalAttempts = c.TotalAttempts
            }).ToList(),
            ErrorPatterns = errorPatterns.Select(e => new ErrorPatternItem
            {
                PatternId = e.Id,
                Topic = e.ErrorTopic ?? "Unknown",
                ErrorCount = e.ErrorCount,
                IsResolved = e.IsResolved
            }).ToList(),
            RecentQuizzes = attempts
                .OrderByDescending(a => a.CompletedAt)
                .Take(5)
                .Select(a => new RecentQuizResult
                {
                    QuizId = a.QuizId,
                    QuizTitle = a.Quiz?.Title ?? "Unknown Quiz",
                    Score = a.Score,
                    CompletedAt = a.CompletedAt,
                    CorrectAnswers = (int)(a.Score ?? 0) / 10,
                    TotalQuestions = a.StudentQuizAnswers?.Count ?? 0
                }).ToList(),
            IsAtRisk = IsStudentAtRisk(competencies, errorPatterns, avgScore)
        };
    }

    public async Task<int> GetAtRiskStudentCountAsync(Guid classId, List<Guid>? studentIds = null)
    {
        var ids = studentIds ?? await _unitOfWork.ClassEnrollmentRepository
            .GetQueryable()
            .Where(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        int atRiskCount = 0;

        foreach (var studentId in ids)
        {
            var competencies = await _unitOfWork.StudentCompetencyRepository
                .FindAsync(c => c.StudentId == studentId);

            var errorPatterns = await _unitOfWork.ErrorPatternRepository
                .FindAsync(e => e.StudentId == studentId && !e.IsResolved);

            var avgScore = competencies.Any() ? (double)competencies.Average(c => c.Score) : 0;

            if (IsStudentAtRisk(competencies, errorPatterns, avgScore))
            {
                atRiskCount++;
            }
        }

        return atRiskCount;
    }

    public async Task<List<StudentAnalyticsDetail>> GetAtRiskStudentsAsync(Guid classId)
    {
        var students = await GetClassStudentAnalyticsAsync(classId);
        return students.Where(s => s.IsAtRisk).ToList();
    }

    public async Task<Dictionary<string, int>> GetClassErrorDistributionAsync(Guid classId)
    {
        var enrollments = await _unitOfWork.ClassEnrollmentRepository
            .GetQueryable()
            .Where(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var errorPatterns = await _unitOfWork.ErrorPatternRepository
            .GetQueryable()
            .Where(e => enrollments.Contains(e.StudentId))
            .GroupBy(e => e.ErrorTopic ?? "Unknown")
            .Select(g => new { Topic = g.Key, Count = g.Sum(e => e.ErrorCount) })
            .ToListAsync();

        return errorPatterns.ToDictionary(x => x.Topic, x => x.Count);
    }

    public async Task<Dictionary<string, Dictionary<string, double>>> GetCompetencyMatrixAsync(Guid classId)
    {
        var enrollments = await _unitOfWork.ClassEnrollmentRepository
            .GetQueryable()
            .Where(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var competencies = await _unitOfWork.StudentCompetencyRepository
            .GetQueryable()
            .Where(c => enrollments.Contains(c.StudentId) && c.BoneSpecialty != null)
            .Include(c => c.BoneSpecialty)
            .Include(c => c.Student)
            .ToListAsync();

        var matrix = new Dictionary<string, Dictionary<string, double>>();

        var topics = competencies.Select(c => c.BoneSpecialty!.Name ?? "Unknown").Distinct();

        foreach (var topic in topics)
        {
            var topicCompetencies = competencies.Where(c => c.BoneSpecialty?.Name == topic);
            var students = topicCompetencies.Select(c => c.Student?.FullName ?? "Unknown").Distinct();

            matrix[topic] = new Dictionary<string, double>();
            foreach (var student in students)
            {
                var studentScore = topicCompetencies
                    .FirstOrDefault(c => c.Student?.FullName == student)?.Score ?? 0;
                matrix[topic][student] = (double)studentScore;
            }
        }

        return matrix;
    }

    private bool IsStudentAtRisk(List<StudentCompetency> competencies, List<ErrorPattern> errorPatterns, double avgScore)
    {
        if (avgScore < 40 && competencies.Any()) return true;
        
        var weakTopics = competencies.Count(c => c.Score < 40);
        if (weakTopics >= 3) return true;

        var unresolvedPatterns = errorPatterns.Count(e => e.ErrorCount >= 3);
        if (unresolvedPatterns >= 2) return true;

        return false;
    }

    private string CalculateOverallMastery(List<StudentCompetency> competencies)
    {
        if (!competencies.Any()) return "Not Started";

        var avgScore = competencies.Average(c => c.Score);
        
        if (avgScore >= 80) return "Expert";
        if (avgScore >= 60) return "Proficient";
        if (avgScore >= 40) return "Intermediate";
        return "Beginner";
    }
}
