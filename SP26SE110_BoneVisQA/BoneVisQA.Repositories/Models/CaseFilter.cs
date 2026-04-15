using System;

namespace BoneVisQA.Repositories.Models;

public class CaseFilter
{
    public Guid? CategoryId { get; set; }
    public string? Difficulty { get; set; }
    public string? Location { get; set; }
    public string? LesionType { get; set; }
    public string? LessonType { get; set; }
}
