using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    public interface IUserManagementService
    {
        Task<List<UserManagementDTO>> GetAllUsersAsync();
        Task<List<UserManagementDTO>> GetUserByRoleAsync(string role);
        Task<UserManagementDTO?> GetUserByIdAsync(Guid userId);
        Task<UserManagementDTO?> ActivateUserAccountAsync(Guid userId);
        Task<UserManagementDTO?> DeactivateUserAccountAsync(Guid userId);
        Task<UserManagementDTO?> ToggleUserStatusAsync(Guid userId, bool? isActive);
        Task<UserManagementDTO?> AssignRoleAsync(Guid userId, string roleName);
        Task<UserManagementDTO?> RevokeRoleAsync(Guid userId);

        // ── CRUD ─────────────────────────────────────────────────────────────
        /// <summary>Creates a user with a specified role. Sends a welcome email if requested.</summary>
        Task<UserManagementDTO?> CreateUserAsync(CreateUserRequestDto request);

        /// <summary>Updates user FullName / SchoolCohort. Does NOT change role or password.</summary>
        Task<UserManagementDTO?> UpdateUserAsync(Guid userId, UpdateUserRequestDto request);

        /// <summary>Soft-deletes a user by removing them (and their UserRole associations)
        /// from the database. Only admins can call this.</summary>
        Task<bool> DeleteUserAsync(Guid userId);
    }
}
