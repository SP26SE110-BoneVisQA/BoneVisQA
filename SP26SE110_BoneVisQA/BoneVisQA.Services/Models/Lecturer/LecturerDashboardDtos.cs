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
