using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.Admin
{
    /// <summary>
    /// DTO cho trang Admin Dashboard - Quản lý Class toàn diện
    /// Bao gồm thông tin đầy đủ về Expert Specialty
    /// </summary>

    //======================================================= CLASS DASHBOARD ===================================================
    
    /// <summary>
    /// Class với thông tin đầy đủ: Lecturer, Expert, Expert's Specialties
    /// </summary>
    public class ClassDashboardDto
    {
        public Guid Id { get; set; }
        public string ClassName { get; set; } = null!;
        public string Semester { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Classification - Bone Specialty
        public Guid? ClassSpecialtyId { get; set; }
        public string? ClassSpecialtyName { get; set; }
        public string? ClassSpecialtyCode { get; set; }
        public string? FocusLevel { get; set; }
        public string? TargetStudentLevel { get; set; }

        // Lecturer Info
        public Guid? LecturerId { get; set; }
        public string? LecturerName { get; set; }
        public string? LecturerEmail { get; set; }

        // Expert Info
        public Guid? ExpertId { get; set; }
        public string? ExpertName { get; set; }
        public string? ExpertEmail { get; set; }

        // Expert Specialties - FULL DETAIL
        public List<ExpertSpecialtyInfoDto> ExpertSpecialties { get; set; } = new();

        // Student Count
        public int StudentCount { get; set; }

        // Additional Stats
        public int TotalCases { get; set; }
        public int TotalQuizzes { get; set; }
    }

    /// <summary>
    /// Thông tin chuyên môn của Expert (đầy đủ)
    /// </summary>
    public class ExpertSpecialtyInfoDto
    {
        public Guid Id { get; set; }
        public Guid BoneSpecialtyId { get; set; }
        public string? BoneSpecialtyName { get; set; }
        public string? BoneSpecialtyCode { get; set; }
        public Guid? PathologyCategoryId { get; set; }
        public string? PathologyCategoryName { get; set; }
        public int ProficiencyLevel { get; set; }
        public int? YearsExperience { get; set; }
        public string? Certifications { get; set; }
        public bool IsPrimary { get; set; }
    }

    //======================================================= EXPERT DROPDOWN ===================================================
    
    /// <summary>
    /// Expert cho dropdown - có thêm thông tin specialties để hiển thị
    /// </summary>
    public class ExpertDropdownDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public List<ExpertSpecialtyBriefDto> Specialties { get; set; } = new();
    }

    /// <summary>
    /// Chuyên môn ngắn gọn cho dropdown
    /// </summary>
    public class ExpertSpecialtyBriefDto
    {
        public Guid BoneSpecialtyId { get; set; }
        public string? BoneSpecialtyName { get; set; }
        public string? PathologyCategoryName { get; set; }
        public int ProficiencyLevel { get; set; }
        public bool IsPrimary { get; set; }
    }

    //======================================================= LECTURER DROPDOWN ===================================================
    
    /// <summary>
    /// Lecturer cho dropdown
    /// </summary>
    public class LecturerDropdownDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
    }

    //======================================================= DASHBOARD SUMMARY ===================================================
    
    /// <summary>
    /// Tổng hợp Dashboard - Thống kê toàn hệ thống
    /// </summary>
    public class AdminDashboardSummaryDto
    {
        public int TotalClasses { get; set; }
        public int TotalLecturers { get; set; }
        public int TotalExperts { get; set; }
        public int TotalStudents { get; set; }
        public int ClassesWithoutLecturer { get; set; }
        public int ClassesWithoutExpert { get; set; }
    }

    //======================================================= PAGED RESULT ===================================================
    
    /// <summary>
    /// Kết quả phân trang generic cho Admin Dashboard
    /// (Tên khác để tránh conflict với Expert.PagedResult)
    /// </summary>
    public class AdminPagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }

    //======================================================= CLASS DETAIL VIEW ===================================================
    
    /// <summary>
    /// Chi tiết một Class - View riêng cho Admin
    /// </summary>
    public class ClassDetailDto
    {
        public Guid Id { get; set; }
        public string ClassName { get; set; } = null!;
        public string Semester { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Lecturer
        public LecturerInfoDto? Lecturer { get; set; }

        // Expert với Specialties
        public ExpertInfoDto? Expert { get; set; }

        // Students enrolled
        public List<StudentInfoDto> Students { get; set; } = new();

        // Stats
        public ClassStatsDetailDto Stats { get; set; } = new();
    }

    public class LecturerInfoDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
    }

    public class ExpertInfoDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public List<ExpertSpecialtyInfoDto> Specialties { get; set; } = new();
    }

    public class StudentInfoDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string? Email { get; set; }
        public DateTime? EnrolledAt { get; set; }
    }

    public class ClassStatsDetailDto
    {
        public int TotalStudents { get; set; }
        public int TotalCasesAssigned { get; set; }
        public int TotalQuizzesAssigned { get; set; }
        public int TotalAnnouncements { get; set; }
    }

    /// <summary>
    /// DTO cho ClassEnrollment
    /// </summary>
    public class ClassEnrollmentDto
    {
        public Guid Id { get; set; }
        public Guid ClassId { get; set; }
        public Guid StudentId { get; set; }
        public string StudentName { get; set; } = null!;
        public string? StudentEmail { get; set; }
        public DateTime EnrolledAt { get; set; }
    }

    //======================================================= CREATE / UPDATE CLASS ===================================================
    
    /// <summary>
    /// Request tạo Class mới
    /// </summary>
    public class CreateClassRequestDto
    {
        public string ClassName { get; set; } = null!;
        public string Semester { get; set; } = null!;
        public Guid? ClassSpecialtyId { get; set; }
        public string? FocusLevel { get; set; }
        public string? TargetStudentLevel { get; set; }
        public List<string>? TargetPathologyCategories { get; set; }
    }

    /// <summary>
    /// Request cập nhật Class
    /// </summary>
    public class UpdateClassRequestDto
    {
        public Guid Id { get; set; }
        public string ClassName { get; set; } = null!;
        public string Semester { get; set; } = null!;
        public Guid? ClassSpecialtyId { get; set; }
        public string? FocusLevel { get; set; }
        public string? TargetStudentLevel { get; set; }
        public List<string>? TargetPathologyCategories { get; set; }
    }

    /// <summary>
    /// Request cập nhật Specialty cho Class
    /// </summary>
    public class UpdateClassSpecialtyRequestDto
    {
        public Guid ClassId { get; set; }
        public Guid? ClassSpecialtyId { get; set; }
        public string? FocusLevel { get; set; }
        public string? TargetStudentLevel { get; set; }
        public List<string>? TargetPathologyCategories { get; set; }
    }

    /// <summary>
    /// Request gán Lecturer/Expert vào Class
    /// </summary>
    public class AssignUserToClassRequestDto
    {
        public Guid ClassId { get; set; }
        public Guid? LecturerId { get; set; }
        public Guid? ExpertId { get; set; }
    }
}
