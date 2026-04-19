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

/// <summary>
/// Thông tin assignment được gắn với announcement (để hiển thị badge cho sinh viên).
/// </summary>
public class AnnouncementAssignmentInfoDto
{
    public Guid? AssignmentId { get; set; }
    public string? AssignmentTitle { get; set; }
    /// <summary>"case" hoặc "quiz"</summary>
    public string? AssignmentType { get; set; }
}

public class StudentAnnouncementDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    /// <summary>Thông tin assignment liên quan (nếu có)</summary>
    public AnnouncementAssignmentInfoDto? RelatedAssignment { get; set; }
}
