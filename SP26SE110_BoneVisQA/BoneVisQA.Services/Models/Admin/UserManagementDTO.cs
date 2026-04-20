using System.ComponentModel.DataAnnotations;

namespace BoneVisQA.Services.Models.Admin
{
    /// <summary>One class relationship for admin user table (student enrollment or class staff).</summary>
    public class UserClassAssignmentDto
    {
        public Guid ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        /// <summary><c>Student</c>, <c>Lecturer</c>, or <c>Expert</c>.</summary>
        public string RoleInClass { get; set; } = string.Empty;
        public DateTime? EnrolledAt { get; set; }
    }

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

        /// <summary>Classes where the user is enrolled as a student or assigned as lecturer/expert.</summary>
        public List<UserClassAssignmentDto> ClassAssignments { get; set; } = new();
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

    // ── Bulk Import Users ───────────────────────────────────────────────────────

    /// <summary>Request payload for importing multiple users from a file.</summary>
    public class BulkCreateUsersRequestDto
    {
        [Required]
        [MinLength(1)]
        public List<ImportUserItemDto> Users { get; set; } = new();
    }

    /// <summary>Single user record within the bulk import request.</summary>
    public class ImportUserItemDto
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

    /// <summary>Result of a bulk import operation.</summary>
    public class BulkCreateUsersResultDto
    {
        public int TotalRequested { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<ImportUserSuccessDto> Successes { get; set; } = new();
        public List<ImportUserErrorDto> Errors { get; set; } = new();
    }

    public class ImportUserSuccessDto
    {
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
    }

    public class ImportUserErrorDto
    {
        public string Email { get; set; } = null!;
        public string? FullName { get; set; }
        public string Error { get; set; } = null!;
        public int Row { get; set; }
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
