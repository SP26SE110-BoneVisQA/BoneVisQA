using System;
using BoneVisQA.Services.Models.Lecturer;

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
    public Guid? ExpertId { get; set; }
    public string? ExpertName { get; set; }
    public int TotalAnnouncements { get; set; }
    public int TotalQuizzes { get; set; }
    public int TotalCases { get; set; }
    public DateTime? EnrolledAt { get; set; }
}

/// <summary>
/// Chi tiết đầy đủ của một lớp học — dùng khi student mở rộng một lớp.
/// </summary>
public class StudentClassDetailDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public Guid? LecturerId { get; set; }
    public string? LecturerName { get; set; }
    public Guid? ExpertId { get; set; }
    public string? ExpertName { get; set; }
    public string? ExpertEmail { get; set; }
    public string? ExpertAvatarUrl { get; set; }
    public DateTime? EnrolledAt { get; set; }

    public List<StudentCaseAssignmentDto> AssignedCases { get; set; } = new();
    public List<ClassQuizSummaryDto> Quizzes { get; set; } = new();
    public List<ClassStudentSummaryDto> Students { get; set; } = new();
    public List<ClassAnnouncementDto> Announcements { get; set; } = new();
}

/// <summary>
/// Case assignment được gán cho sinh viên trong một lớp học.
/// </summary>
public class StudentCaseAssignmentDto
{
    public Guid CaseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public bool IsMandatory { get; set; }
}

public class ClassQuizSummaryDto
{
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int TotalQuestions { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }
    public bool IsCompleted { get; set; }
    public double? Score { get; set; }
}

public class ClassStudentSummaryDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
}

public class ClassAnnouncementDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    /// <summary>Thông tin assignment liên quan (nếu có)</summary>
    public AnnouncementAssignmentInfoDto? RelatedAssignment { get; set; }
}
