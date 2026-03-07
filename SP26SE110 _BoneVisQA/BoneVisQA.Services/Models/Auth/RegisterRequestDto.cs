namespace BoneVisQA.Services.Models.Auth;

public class RegisterRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? SchoolCohort { get; set; }
    public string RoleName { get; set; } = "Student";
}

