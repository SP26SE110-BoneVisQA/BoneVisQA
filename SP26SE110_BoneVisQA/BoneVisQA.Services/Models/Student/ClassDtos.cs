using System;

namespace BoneVisQA.Services.Models.Student;

/// <summary>
/// Lớp học mà sinh viên đã đăng ký — dùng cho trang Classes của student.
/// </summary>
public class StudentClassDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public Guid? LecturerId { get; set; }
    public string? LecturerName { get; set; }
    public int TotalAnnouncements { get; set; }
    public int TotalQuizzes { get; set; }
    public int TotalCases { get; set; }
    public DateTime? EnrolledAt { get; set; }
}
