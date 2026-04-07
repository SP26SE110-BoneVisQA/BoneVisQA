using System;

namespace BoneVisQA.Services.Models.Lecturer;

public class LecturerDashboardStatsDto
{
    public int TotalClasses { get; set; }
    public int TotalStudents { get; set; }
    public int TotalQuestions { get; set; }
    public int EscalatedItems { get; set; }
    public int PendingReviews { get; set; }
    public double? AverageQuizScore { get; set; }
}

public class ClassLeaderboardItemDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int TotalCasesViewed { get; set; }
    public double? AverageQuizScore { get; set; }
    public int TotalQuestionsAsked { get; set; }
}

/// <summary>Performance summary for one lecturer-owned class.</summary>
public class ClassPerformanceDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int TotalCasesViewed { get; set; }
    public double? AvgQuizScore { get; set; }
    public int CompletionRate { get; set; }
    public int TrendPercent { get; set; }
    public int TotalQuestions { get; set; }
    public int EscalatedCount { get; set; }
}

/// <summary>Quiz performance broken down by topic for a lecturer's class.</summary>
public class TopicScoreDto
{
    public string Topic { get; set; } = string.Empty;
    public double AvgScore { get; set; }
    public int Attempts { get; set; }
    public string[] CommonErrors { get; set; } = Array.Empty<string>();
}

/// <summary>Aggregated analytics for a lecturer: classes + topics + top/bottom students.</summary>
public class LecturerAnalyticsDto
{
    public IReadOnlyList<ClassPerformanceDto> ClassPerformance { get; set; } = Array.Empty<ClassPerformanceDto>();
    public IReadOnlyList<TopicScoreDto> TopicScores { get; set; } = Array.Empty<TopicScoreDto>();
    public IReadOnlyList<ClassLeaderboardItemDto> TopStudents { get; set; } = Array.Empty<ClassLeaderboardItemDto>();
    public IReadOnlyList<ClassLeaderboardItemDto> BottomStudents { get; set; } = Array.Empty<ClassLeaderboardItemDto>();
}
