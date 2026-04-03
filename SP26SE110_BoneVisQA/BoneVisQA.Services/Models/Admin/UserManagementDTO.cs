using System.ComponentModel.DataAnnotations;

namespace BoneVisQA.Services.Models.Admin
{
    public class UserManagementDTO
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? SchoolCohort { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
        public DateTime? LastLogin { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateUserRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(2)]
        public string FullName { get; set; } = null!;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = null!;

        public string? SchoolCohort { get; set; }

        [Required]
        public string Role { get; set; } = null!;

        public bool SendWelcomeEmail { get; set; } = true;
    }

    public class UpdateUserRequestDto
    {
        [Required]
        [MinLength(2)]
        public string FullName { get; set; } = null!;

        public string? SchoolCohort { get; set; }
    }
}
