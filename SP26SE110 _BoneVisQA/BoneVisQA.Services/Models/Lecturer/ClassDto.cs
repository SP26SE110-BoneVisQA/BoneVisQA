using System;

namespace BoneVisQA.Services.Models.Lecturer;

public class ClassDto
{
    public Guid Id { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public Guid? LecturerId { get; set; }
    public DateTime? CreatedAt { get; set; }
}

