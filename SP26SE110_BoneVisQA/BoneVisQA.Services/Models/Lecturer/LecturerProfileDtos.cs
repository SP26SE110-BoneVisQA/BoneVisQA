using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Lecturer;

public class LecturerProfileDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Department { get; set; }
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

    // Lecturer-specific preferences
    public int DefaultQuizDuration { get; set; } = 30;
    public int LowScoreThreshold { get; set; } = 50;
    public bool NotifyNewStudent { get; set; } = true;
    public bool NotifyQuizComplete { get; set; } = true;
    public bool NotifyLowScore { get; set; } = true;
    public bool NotifyNewQuestion { get; set; } = false;
}

public class UpdateLecturerProfileRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? AvatarUrl { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Gender { get; set; }
    public string? StudentSchoolId { get; set; }
    public string? ClassCode { get; set; }
    public string? Address { get; set; }
    public string? Bio { get; set; }
    public string? EmergencyContact { get; set; }

    public int DefaultQuizDuration { get; set; } = 30;
    public int LowScoreThreshold { get; set; } = 50;
    public bool NotifyNewStudent { get; set; } = true;
    public bool NotifyQuizComplete { get; set; } = true;
    public bool NotifyLowScore { get; set; } = true;
    public bool NotifyNewQuestion { get; set; } = false;
}
