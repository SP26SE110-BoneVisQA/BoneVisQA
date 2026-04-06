namespace BoneVisQA.Services.Models.Admin;

public class MedicalVerificationRequestDto
{
    public bool IsMedicalStudent { get; set; }
    public string? MedicalSchool { get; set; }
    public string? MedicalStudentId { get; set; }
}

public class ApproveMedicalVerificationRequestDto
{
    public bool IsApproved { get; set; }
    public string? Notes { get; set; }
}

public class PendingVerificationDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? SchoolCohort { get; set; }
    public bool IsMedicalStudent { get; set; }
    public string? MedicalSchool { get; set; }
    public string? MedicalStudentId { get; set; }
    public string? VerificationStatus { get; set; }
    public DateTime? CreatedAt { get; set; }
}
