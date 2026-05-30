namespace BoneVisQA.Services.Models.Lecturer;

public class StudentProgressSummaryDto
{
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public List<StudentProgressItemDto> Students { get; set; } = new();
    public ClassProgressOverviewDto Overview { get; set; } = new();
}

public class StudentProgressItemDto
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public double AverageScore { get; set; }
    public int QuizzesCompleted { get; set; }
    public int TotalQuizzes { get; set; }
    public int CasesViewed { get; set; }
    public int TotalCases { get; set; }
    public double OverallProgress { get; set; }
    public string ProgressStatus { get; set; } = "NotStarted";
    public List<CompetencyScoreDto> Competencies { get; set; } = new();
    public DateTime? LastActivity { get; set; }
}

public class StudentProgressDetailDto
{
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? Email { get; set; }
    public Guid ClassId { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? LastActivity { get; set; }

    public QuizProgressDetailDto QuizProgress { get; set; } = new();
    public CaseProgressDetailDto CaseProgress { get; set; } = new();
    public CompetencyDetailDto CompetencyDetail { get; set; } = new();
    public List<RecentActivityDto> RecentActivities { get; set; } = new();
}

public class QuizProgressDetailDto
{
    public int TotalQuizzes { get; set; }
    public int CompletedQuizzes { get; set; }
    public int PendingQuizzes { get; set; }
    public double AverageScore { get; set; }
    public double HighestScore { get; set; }
    public double LowestScore { get; set; }
    public List<QuizScoreItemDto> QuizScores { get; set; } = new();
}

public class QuizScoreItemDto
{
    public Guid QuizId { get; set; }
    public string? QuizTitle { get; set; }
    public string? Topic { get; set; }
    public double Score { get; set; }
    public double MaxScore { get; set; }
    public double Percentage { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsPassed { get; set; }
}

public class CaseProgressDetailDto
{
    public int TotalAssignedCases { get; set; }
    public int ViewedCases { get; set; }
    public int CompletedCases { get; set; }
    public double CompletionRate { get; set; }
    public List<CaseViewItemDto> RecentCases { get; set; } = new();
}

public class CaseViewItemDto
{
    public Guid CaseId { get; set; }
    public string? CaseTitle { get; set; }
    public string? CaseImageUrl { get; set; }
    public DateTime? ViewedAt { get; set; }
    public int ViewCount { get; set; }
    public bool IsCompleted { get; set; }
}

public class CompetencyDetailDto
{
    public double OverallCompetency { get; set; }
    public List<CompetencyScoreDto> Competencies { get; set; } = new();
    public List<TopicMasteryDto> TopicMasteries { get; set; } = new();
    public List<CompetencyHistoryDto> History { get; set; } = new();
}

public class CompetencyScoreDto
{
    public Guid CompetencyId { get; set; }
    public string? CompetencyName { get; set; }
    public double Score { get; set; }
    public double MaxScore { get; set; }
    public double Percentage { get; set; }
    public string Level { get; set; } = "Beginner";
    public string? IconUrl { get; set; }
}

public class ClassCompetencyOverviewDto
{
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public int TotalStudents { get; set; }
    public double AverageCompetency { get; set; }
    public List<CompetencyScoreDto> ClassCompetencies { get; set; } = new();
    public List<TopicMasteryDto> TopicMasteries { get; set; } = new();
    public List<CompetencyDistributionDto> CompetencyDistribution { get; set; } = new();
    public List<WeakTopicDto> WeakTopics { get; set; } = new();
    public List<StrongTopicDto> StrongTopics { get; set; } = new();
}

public class CompetencyDistributionDto
{
    public string Level { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public double Percentage { get; set; }
}

public class TopicMasteryDto
{
    public Guid TopicId { get; set; }
    public string? TopicName { get; set; }
    public string? Category { get; set; }
    public double MasteryScore { get; set; }
    public double MaxScore { get; set; }
    public double MasteryPercentage { get; set; }
    public string MasteryLevel { get; set; } = "Beginner";
    public int StudentsAssessed { get; set; }
    public double ClassAverage { get; set; }
}

public class CompetencyHistoryDto
{
    public DateTime Date { get; set; }
    public double Score { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
}

public class WeakTopicDto
{
    public string TopicName { get; set; } = string.Empty;
    public double AverageScore { get; set; }
    public int StudentsNeedingHelp { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class StrongTopicDto
{
    public string TopicName { get; set; } = string.Empty;
    public double AverageScore { get; set; }
    public int StudentsMastered { get; set; }
}

public class RecentActivityDto
{
    public Guid ActivityId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
    public double? Score { get; set; }
}

public class ClassProgressOverviewDto
{
    public double ClassAverageScore { get; set; }
    public double ClassAverageProgress { get; set; }
    public int TotalQuizzes { get; set; }
    public int TotalCases { get; set; }
    public double QuizCompletionRate { get; set; }
    public double CaseCompletionRate { get; set; }
    public DateTime CalculatedAt { get; set; }
}
