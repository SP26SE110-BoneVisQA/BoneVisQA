using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Lecturer;

public class LecturerOverallReportDto
{
    public Guid LecturerId { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public int TotalClasses { get; set; }
    public int TotalStudents { get; set; }
    public int TotalQuizzes { get; set; }
    public int TotalCases { get; set; }
    public double AverageQuizScore { get; set; }
    public int TotalQuizAttempts { get; set; }
    public double PassRate { get; set; }
    public int ActiveStudents { get; set; }
    public int InactiveStudents { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class ClassReportDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int QuizCount { get; set; }
    public int CaseCount { get; set; }
    public double AverageScore { get; set; }
    public int TotalAttempts { get; set; }
    public double PassRate { get; set; }
}

public class ClassDetailedReportDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public string LecturerName { get; set; } = string.Empty;
    public string? ExpertName { get; set; }
    public int StudentCount { get; set; }
    public int QuizzesAssigned { get; set; }
    public int CasesAssigned { get; set; }
    public int TotalQuizAttempts { get; set; }
    public double AverageScore { get; set; }
    public double PassRate { get; set; }
    public double AverageQuestionsPerStudent { get; set; }
    public int TotalQuestionsAsked { get; set; }
    public int EscalatedAnswers { get; set; }
    public List<QuizSummaryDto> QuizSummaries { get; set; } = new();
    public List<StudentPerformanceDto> TopStudents { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class StudentReportDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public int QuizAttempts { get; set; }
    public double AverageScore { get; set; }
    public double? PassRate { get; set; }
    public int CasesViewed { get; set; }
    public int QuestionsAsked { get; set; }
    public int EscalatedQuestions { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public string ActivityStatus { get; set; } = "Active";
}

public class QuizReportDto
{
    public Guid QuizId { get; set; }
    public Guid SessionId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public double AverageScore { get; set; }
    public double PassRate { get; set; }
    public int HighestScore { get; set; }
    public int LowestScore { get; set; }
    public int PassingScore { get; set; }
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
}

public class QuizSummaryDto
{
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int TotalAttempts { get; set; }
    public double AverageScore { get; set; }
    public double PassRate { get; set; }
}

public class StudentPerformanceDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public double AverageScore { get; set; }
    public int QuizAttempts { get; set; }
    public int QuestionsAsked { get; set; }
}

public class AIQualityReportDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int TotalAnswers { get; set; }
    public int AIAnswers { get; set; }
    public int EscalatedAnswers { get; set; }
    public int ApprovedByLecturer { get; set; }
    public int RejectedAnswers { get; set; }
    public double AverageConfidenceScore { get; set; }
    public double AutoApprovalRate { get; set; }
    public List<AIScoreDistributionDto> ScoreDistribution { get; set; } = new();
}

public class AIScoreDistributionDto
{
    public string Range { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class ActivityReportDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int ActiveStudents { get; set; }
    public int InactiveStudents { get; set; }
    public List<DailyActivityDto> DailyActivities { get; set; } = new();
    public List<QuizActivityDto> QuizActivities { get; set; } = new();
}

public class DailyActivityDto
{
    public DateTime Date { get; set; }
    public int Logins { get; set; }
    public int CasesViewed { get; set; }
    public int QuestionsAsked { get; set; }
    public int QuizAttempts { get; set; }
}

public class QuizActivityDto
{
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
}
