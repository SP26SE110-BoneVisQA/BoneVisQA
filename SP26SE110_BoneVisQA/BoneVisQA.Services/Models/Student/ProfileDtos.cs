using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Student;

public class StudentProfileDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Primary role for compact UI (first assigned role).</summary>
    public string? Role { get; set; }
    public string? SchoolCohort { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class UpdateStudentProfileRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string? SchoolCohort { get; set; }
    public string? AvatarUrl { get; set; }
}
