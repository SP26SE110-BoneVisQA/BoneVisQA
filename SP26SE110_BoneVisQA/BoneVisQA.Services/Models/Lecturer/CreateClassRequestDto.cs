using System;

namespace BoneVisQA.Services.Models.Lecturer;

public class CreateClassRequestDto
{
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public Guid LecturerId { get; set; }
}

