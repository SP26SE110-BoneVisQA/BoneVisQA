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

    /// <summary>Danh sách user có phân trang (mới nhất theo CreatedAt).</summary>
    public class PagedUsersResultDto
    {
        public List<UserManagementDTO> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
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

    
    
    
    
    //// ── User Class Management ─────────────────────────────────────────────────

    ///// <summary>Thông tin một lớp trong danh sách.</summary>
    //public class UserClassInfo
    //{
    //    public Guid Id { get; set; }
    //    public string ClassName { get; set; } = null!;
    //    /// "Lecturer" | "Student"
    //    public string RelationType { get; set; } = null!;
    //    public DateTime? EnrolledAt { get; set; }
    //}

    ///// <summary>Danh sách lớp của một user (gộp AcademicClass + ClassEnrollment).</summary>
    //public class UserClassListDto
    //{
    //    public Guid UserId { get; set; }
    //    public List<UserClassInfo> Classes { get; set; } = new();
    //}

    ///// <summary>Tất cả lớp có sẵn trong hệ thống để admin gán.</summary>
    //public class AvailableClassDto
    //{
    //    public Guid Id { get; set; }
    //    public string ClassName { get; set; } = null!;
    //    public string? LecturerName { get; set; }
    //    public int StudentCount { get; set; }
    //}

    //public class AssignUserToClassRequestDto
    //{
    //    [Required]
    //    public Guid ClassId { get; set; }
    //}
}
