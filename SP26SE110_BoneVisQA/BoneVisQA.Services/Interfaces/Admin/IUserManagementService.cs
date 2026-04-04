using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    public interface IUserManagementService
    {
        Task<List<UserManagementDTO>> GetAllUsersAsync();

        /// <summary>User mới nhất trước, có phân trang.</summary>
        Task<PagedUsersResultDto> GetUsersPagedAsync(int page, int pageSize);

        Task<List<UserManagementDTO>> GetUserByRoleAsync(string role);
        Task<UserManagementDTO?> GetUserByIdAsync(Guid userId);
        Task<UserManagementDTO?> ActivateUserAccountAsync(Guid userId);
        Task<UserManagementDTO?> DeactivateUserAccountAsync(Guid userId);
    //    Task<UserManagementDTO?> ToggleUserStatusAsync(Guid userId, bool? isActive);

        Task<UserManagementDTO?> AssignRoleAsync(Guid userId, string roleName);
        Task<UserManagementDTO?> RevokeRoleAsync(Guid userId);

        // ── CRUD ─────────────────────────────────────────────────────────────
        /// <summary>Creates a user with a specified role. Sends a welcome email if requested.</summary>
        Task<UserManagementDTO?> CreateUserAsync(CreateUserRequestDto request);

        /// <summary>Updates user FullName / SchoolCohort. Does NOT change role or password.</summary>
        Task<UserManagementDTO?> UpdateUserAsync(Guid userId, UpdateUserRequestDto request);

        /// <summary>Permanently deletes a user (and their UserRole associations) from the database. Only admins can call this.</summary>
        Task<bool> DeleteUserAsync(Guid userId);

        // ── Class management ────────────────────────────────────────────────────

        /// <summary>Lấy danh sách lớp (AcademicClass) mà giảng viên đang dạy.</summary>
        Task<List<UserClassInfo>> GetUserClassesAsync(Guid userId);

        /// <summary>Lấy tất cả lớp có sẵn trong hệ thống.</summary>
        Task<List<AvailableClassDto>> GetAvailableClassesAsync();

        /// <summary>
        /// Gán user vào một lớp:
        ///   - Lecturer → cập nhật AcademicClass.LecturerId
        ///   - Student   → tạo / cập nhật ClassEnrollment
        /// </summary>
        Task<UserClassInfo?> AssignUserToClassAsync(Guid userId, Guid classId);

        /// <summary>
        /// Xóa user khỏi một lớp:
        ///   - Lecturer → set AcademicClass.LecturerId = null
        ///   - Student  → xóa ClassEnrollment
        /// </summary>
        Task<bool> RemoveUserFromClassAsync(Guid userId, Guid classId);

        // ── Medical Student Verification ─────────────────────────────────────────

        /// <summary>Lấy danh sách user đang chờ xác minh sinh viên y khoa.</summary>
        Task<List<PendingVerificationDto>> GetPendingVerificationsAsync();

        /// <summary>Duyệt hoặc từ chối xác minh sinh viên y khoa của một user.</summary>
        Task<UserManagementDTO?> ApproveMedicalVerificationAsync(Guid userId, bool isApproved, string? notes, Guid adminId);
    }
}
