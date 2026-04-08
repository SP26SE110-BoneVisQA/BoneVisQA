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

    public DateOnly? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Gender { get; set; }
    public string? StudentSchoolId { get; set; }
    public string? ClassCode { get; set; }
    public string? Address { get; set; }
    public string? Bio { get; set; }
    public string? EmergencyContact { get; set; }
}

public class UpdateStudentProfileRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string? SchoolCohort { get; set; }
    public string? AvatarUrl { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Gender { get; set; }
    public string? StudentSchoolId { get; set; }
    public string? ClassCode { get; set; }
    public string? Address { get; set; }
    public string? Bio { get; set; }
    public string? EmergencyContact { get; set; }
}
