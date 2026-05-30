using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Lecturer;

public class GradeBookDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public string LecturerName { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int TotalQuizzes { get; set; }
    public int TotalCases { get; set; }
    public double ClassAverage { get; set; }
    public double PassRate { get; set; }
    public List<StudentGradeDto> StudentGrades { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class StudentGradeDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public double OverallScore { get; set; }
    public string GradeLetter { get; set; } = string.Empty;
    public double QuizScore { get; set; }
    public double CaseScore { get; set; }
    public double ParticipationScore { get; set; }
    public int QuizAttempts { get; set; }
    public int CasesViewed { get; set; }
    public int QuestionsAsked { get; set; }
    public int EscalatedQuestions { get; set; }
    public List<QuizGradeDto> QuizGrades { get; set; } = new();
    public DateTime? LastActivityAt { get; set; }
    public string Status { get; set; } = "Active";
}

public class QuizGradeDto
{
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public double? Score { get; set; }
    public int? PassingScore { get; set; }
    public bool Passed { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double Weight { get; set; }
}

public class GradeBookExportDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public List<ExportColumnDto> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}

public class ExportColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
}

public class UpdateStudentGradeRequestDto
{
    public double? OverrideScore { get; set; }
    public string? Notes { get; set; }
    public bool IsExempt { get; set; }
}

public class GradeSummaryDto
{
    public string GradeLetter { get; set; } = string.Empty;
    public string GradeRange { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public double Percentage { get; set; }
}

public static class GradeCalculator
{
    public static string CalculateGradeLetter(double score)
    {
        return score switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _ => "F"
        };
    }

    public static double CalculateOverallScore(double quizScore, double caseScore, double participationScore)
    {
        return quizScore * 0.6 + caseScore * 0.25 + participationScore * 0.15;
    }

    public static double CalculateQuizScore(List<QuizGradeDto> quizzes)
    {
        if (!quizzes.Any()) return 0;
        var weightedSum = quizzes.Sum(q => (q.Score ?? 0) * q.Weight);
        var totalWeight = quizzes.Sum(q => q.Weight);
        return totalWeight > 0 ? weightedSum / totalWeight : 0;
    }

    public static double CalculateCaseScore(int casesViewed, int questionsAsked, int escalatedQuestions)
    {
        var casePoints = Math.Min(casesViewed * 5, 50);
        var questionPoints = Math.Max(0, 50 - questionsAsked * 2 - escalatedQuestions * 5);
        return Math.Min(casePoints + questionPoints, 100);
    }

    public static double CalculateParticipationScore(int casesViewed, int questionsAsked)
    {
        var engagement = Math.Min(casesViewed + questionsAsked, 20);
        return engagement * 5;
    }
}
