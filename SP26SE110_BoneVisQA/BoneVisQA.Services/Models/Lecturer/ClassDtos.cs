using System;

namespace BoneVisQA.Services.Models.Lecturer;

public class ClassDto
{
    public Guid Id { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public Guid? LecturerId { get; set; }
    public Guid? ExpertId { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CreateClassRequestDto
{
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public Guid LecturerId { get; set; }
    public Guid? ExpertId { get; set; }
}

public class UpdateClassRequestDto
{
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public Guid? ExpertId { get; set; }
}

public class ClassStatsDto
{
    public Guid ClassId { get; set; }
    public int TotalStudents { get; set; }
    public int TotalCasesViewed { get; set; }
    public int TotalQuestionsAsked { get; set; }
    public double? AvgQuizScore { get; set; }
    public int TotalAssignments { get; set; }
    public int CompletedAssignments { get; set; }
}
