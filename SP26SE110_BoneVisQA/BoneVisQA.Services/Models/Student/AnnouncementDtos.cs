using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Student;

public class CaseFilterRequestDto
{
    public Guid? CategoryId { get; set; }
    public string? Difficulty { get; set; }
    public string? Location { get; set; }
    public string? LesionType { get; set; }
    public string? LessonType { get; set; }
}

public class StudentAnnouncementDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}
