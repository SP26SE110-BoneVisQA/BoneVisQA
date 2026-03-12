using System;

namespace BoneVisQA.Services.Models.Lecturer;

public class ClassStatsDto
{
    public Guid ClassId { get; set; }
    public int TotalStudents { get; set; }
    public int TotalCasesViewed { get; set; }
    public int TotalQuestionsAsked { get; set; }
    public double? AvgQuizScore { get; set; }
}

