namespace BoneVisQA.Services.Models.Auth;

public class RegisterRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? SchoolCohort { get; set; }

    public string? MedicalSchool { get; set; }
    public string? MedicalStudentId { get; set; }
}

