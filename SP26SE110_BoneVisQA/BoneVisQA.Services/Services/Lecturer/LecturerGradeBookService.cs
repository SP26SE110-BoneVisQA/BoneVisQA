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

public class LecturerGradeBookService : ILecturerGradeBookService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LecturerGradeBookService> _logger;

    public LecturerGradeBookService(IUnitOfWork unitOfWork, ILogger<LecturerGradeBookService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<GradeBookDto> GetClassGradeBookAsync(Guid classId)
    {
        var academicClass = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId)
            .Include(c => c.Lecturer)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Class not found.");

        var enrollments = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.ClassId == classId)
            .Include(e => e.Student)
            .ToListAsync();

        var quizSessions = await _unitOfWork.Context.ClassQuizSessions
            .Where(qs => qs.ClassId == classId)
            .Include(qs => qs.Quiz)
            .ToListAsync();

        var caseIds = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId)
            .Select(cc => cc.CaseId)
            .ToListAsync();

        var studentGrades = new List<StudentGradeDto>();
        var totalScore = 0.0;
        var passCount = 0;

        foreach (var enrollment in enrollments)
        {
            if (enrollment.Student == null) continue;

            var studentGrade = await GetStudentGradeAsync(classId, enrollment.StudentId);
            if (studentGrade != null)
            {
                studentGrades.Add(studentGrade);
                totalScore += studentGrade.OverallScore;
                if (studentGrade.OverallScore >= 60) passCount++;
            }
        }

        var classAverage = enrollments.Count > 0 ? totalScore / enrollments.Count : 0;
        var passRate = enrollments.Count > 0 ? passCount * 100.0 / enrollments.Count : 0;

        return new GradeBookDto
        {
            ClassId = classId,
            ClassName = academicClass.ClassName,
            Semester = academicClass.Semester ?? "",
            LecturerName = academicClass.Lecturer?.FullName ?? "Lecturer",
            TotalStudents = enrollments.Count,
            TotalQuizzes = quizSessions.Count,
            TotalCases = caseIds.Count,
            ClassAverage = Math.Round(classAverage, 2),
            PassRate = Math.Round(passRate, 2),
            StudentGrades = studentGrades.OrderByDescending(s => s.OverallScore).ToList(),
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<StudentGradeDto?> GetStudentGradeAsync(Guid classId, Guid studentId)
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
            .Include(qs => qs.Quiz)
            .ToListAsync();

        var quizGrades = new List<QuizGradeDto>();
        var quizIds = quizSessions.Select(qs => qs.QuizId).ToList();
        var sessionMap = quizSessions.ToDictionary(qs => qs.QuizId, qs => qs);

        var attempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.StudentId == studentId && quizIds.Contains(a.QuizId))
            .ToListAsync();

        foreach (var attempt in attempts)
        {
            if (sessionMap.TryGetValue(attempt.QuizId, out var session))
            {
                var passScore = session.PassingScore ?? 50;
                quizGrades.Add(new QuizGradeDto
                {
                    QuizId = attempt.QuizId,
                    QuizTitle = session.Quiz?.Title ?? "Quiz",
                    Score = attempt.Score,
                    PassingScore = passScore,
                    Passed = attempt.Score != null && attempt.Score >= passScore,
                    CompletedAt = attempt.CompletedAt,
                    Weight = 1.0
                });
            }
        }

        var quizScore = GradeCalculator.CalculateQuizScore(quizGrades);

        var caseIds = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId)
            .Select(cc => cc.CaseId)
            .ToListAsync();

        var casesViewed = await _unitOfWork.Context.CaseViewLogs
            .Where(v => v.StudentId == studentId && caseIds.Contains(v.CaseId))
            .CountAsync();

        var questionsAsked = await _unitOfWork.Context.StudentQuestions
            .Where(q => q.StudentId == studentId && q.CaseId.HasValue && caseIds.Contains(q.CaseId.Value))
            .CountAsync();

        var escalatedCount = await _unitOfWork.Context.CaseAnswers
            .Where(a => a.Question.StudentId == studentId && a.Question.CaseId.HasValue && caseIds.Contains(a.Question.CaseId.Value))
            .CountAsync(a => CaseAnswerStatuses.IsEscalatedToExpert(a.Status));

        var caseScore = GradeCalculator.CalculateCaseScore(casesViewed, questionsAsked, escalatedCount);
        var participationScore = GradeCalculator.CalculateParticipationScore(casesViewed, questionsAsked);
        var overallScore = GradeCalculator.CalculateOverallScore(quizScore, caseScore, participationScore);

        var lastActivity = attempts.Any()
            ? attempts.Max(a => a.StartedAt)
            : (DateTime?)null;

        var isActive = lastActivity.HasValue && lastActivity.Value >= DateTime.UtcNow.AddDays(-14);

        return new StudentGradeDto
        {
            StudentId = studentId,
            StudentName = student.FullName ?? student.Email ?? "Student",
            StudentEmail = student.Email ?? "",
            StudentCode = student.SchoolCohort,
            OverallScore = Math.Round(overallScore, 2),
            GradeLetter = GradeCalculator.CalculateGradeLetter(overallScore),
            QuizScore = Math.Round(quizScore, 2),
            CaseScore = Math.Round(caseScore, 2),
            ParticipationScore = Math.Round(participationScore, 2),
            QuizAttempts = attempts.Count,
            CasesViewed = casesViewed,
            QuestionsAsked = questionsAsked,
            EscalatedQuestions = escalatedCount,
            QuizGrades = quizGrades,
            LastActivityAt = lastActivity,
            Status = isActive ? "Active" : "Inactive"
        };
    }

    public async Task<IReadOnlyList<StudentGradeDto>> GetAllStudentGradesAsync(Guid classId)
    {
        var enrollments = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.ClassId == classId)
            .ToListAsync();

        var grades = new List<StudentGradeDto>();

        foreach (var enrollment in enrollments)
        {
            var grade = await GetStudentGradeAsync(classId, enrollment.StudentId);
            if (grade != null)
            {
                grades.Add(grade);
            }
        }

        return grades.OrderByDescending(g => g.OverallScore).ToList();
    }

    public async Task<GradeBookExportDto> ExportGradeBookAsync(Guid classId)
    {
        var academicClass = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Class not found.");

        var quizSessions = await _unitOfWork.Context.ClassQuizSessions
            .Where(qs => qs.ClassId == classId)
            .Include(qs => qs.Quiz)
            .OrderBy(qs => qs.OpenTime)
            .ToListAsync();

        var columns = new List<ExportColumnDto>
        {
            new() { Key = "StudentName", Header = "Student Name", Type = "string" },
            new() { Key = "StudentEmail", Header = "Email", Type = "string" }
        };

        foreach (var qs in quizSessions)
        {
            columns.Add(new ExportColumnDto
            {
                Key = $"Quiz_{qs.QuizId}",
                Header = qs.Quiz?.Title ?? "Quiz",
                Type = "number"
            });
        }

        columns.Add(new ExportColumnDto { Key = "QuizScore", Header = "Quiz Average", Type = "number" });
        columns.Add(new ExportColumnDto { Key = "CasesViewed", Header = "Cases Viewed", Type = "number" });
        columns.Add(new ExportColumnDto { Key = "QuestionsAsked", Header = "Questions Asked", Type = "number" });
        columns.Add(new ExportColumnDto { Key = "OverallScore", Header = "Overall Score", Type = "number" });
        columns.Add(new ExportColumnDto { Key = "Grade", Header = "Grade", Type = "string" });

        var grades = await GetAllStudentGradesAsync(classId);
        var rows = new List<Dictionary<string, object?>>();

        foreach (var grade in grades)
        {
            var row = new Dictionary<string, object?>
            {
                { "StudentName", grade.StudentName },
                { "StudentEmail", grade.StudentEmail }
            };

            foreach (var quiz in grade.QuizGrades)
            {
                row[$"Quiz_{quiz.QuizId}"] = quiz.Score;
            }

            row["QuizScore"] = grade.QuizScore;
            row["CasesViewed"] = grade.CasesViewed;
            row["QuestionsAsked"] = grade.QuestionsAsked;
            row["OverallScore"] = grade.OverallScore;
            row["Grade"] = grade.GradeLetter;

            rows.Add(row);
        }

        return new GradeBookExportDto
        {
            ClassId = classId,
            ClassName = academicClass.ClassName,
            Semester = academicClass.Semester ?? "",
            Columns = columns,
            Rows = rows
        };
    }

    public async Task<bool> UpdateStudentGradeAsync(Guid classId, Guid studentId, UpdateStudentGradeRequestDto request)
    {
        // Placeholder for grade override functionality
        // In a real implementation, this would store override scores in a separate table
        await Task.CompletedTask;
        return true;
    }

    public async Task<IReadOnlyList<GradeSummaryDto>> GetGradeSummaryAsync(Guid classId)
    {
        var grades = await GetAllStudentGradesAsync(classId);

        var gradeGroups = grades
            .GroupBy(g => g.GradeLetter)
            .Select(g => new GradeSummaryDto
            {
                GradeLetter = g.Key,
                GradeRange = GetGradeRange(g.Key),
                StudentCount = g.Count(),
                Percentage = grades.Count > 0 ? Math.Round(g.Count() * 100.0 / grades.Count, 2) : 0
            })
            .OrderByDescending(g => g.GradeLetter)
            .ToList();

        return gradeGroups;
    }

    private static string GetGradeRange(string gradeLetter)
    {
        return gradeLetter switch
        {
            "A" => "90-100",
            "B" => "80-89",
            "C" => "70-79",
            "D" => "60-69",
            "F" => "0-59",
            _ => "N/A"
        };
    }
}
