using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    public interface IUserManagementService
    {
        Task<List<UserManagementDTO>> GetAllUsersAsync();    
        Task<PagedUsersResultDto> GetUsersPagedAsync(int page, int pageSize);
        Task<List<UserManagementDTO>> GetUserByRoleAsync(string role);
        Task<UserManagementDTO?> GetUserByIdAsync(Guid userId);
       
      
        Task<UserManagementDTO?> ActivateUserAccountAsync(Guid userId);
        Task<UserManagementDTO?> DeactivateUserAccountAsync(Guid userId);
       
      
        Task<UserManagementDTO?> ToggleUserStatusAsync(Guid userId, bool? isActive);
        Task<UserManagementDTO?> AssignRoleAsync(Guid userId, string roleName);
        Task<UserManagementDTO?> RevokeRoleAsync(Guid userId);

      
        // ── CRUD ─────────────────────────────────────────────────────────────     
        Task<UserManagementDTO?> CreateUserAsync(CreateUserRequestDto request);
        Task<UserManagementDTO?> UpdateUserAsync(Guid userId, UpdateUserRequestDto request);
        Task<bool> DeleteUserAsync(Guid userId);

        // ── Bulk Import Users ─────────────────────────────────────────────────────
        Task<BulkCreateUsersResultDto> BulkCreateUsersAsync(BulkCreateUsersRequestDto request);

      
        // ── Medical Student Verification ─────────────────────────────────────────
        Task<List<PendingVerificationDto>> GetPendingVerificationsAsync();
        Task<UserManagementDTO?> ApproveMedicalVerificationAsync(Guid userId, bool isApproved, string? notes, Guid adminId);
    }
}
