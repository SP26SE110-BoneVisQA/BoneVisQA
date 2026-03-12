using System;

namespace BoneVisQA.Services.Models.Lecturer;

public class CreateQuizRequestDto
{
    public string Title { get; set; } = string.Empty;
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }
}

