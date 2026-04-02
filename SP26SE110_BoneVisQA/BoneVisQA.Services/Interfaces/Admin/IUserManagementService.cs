using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces.Admin
{
    public interface IUserManagementService
    {
        Task<List<UserManagementDTO>> GetAllUsersAsync();
       
        Task<List<UserManagementDTO>> GetUserByRoleAsync(string role);

        Task<UserManagementDTO?> ActivateUserAccountAsync(Guid userId);

        Task<UserManagementDTO?> DeactivateUserAccountAsync(Guid userId);

      //  Task<UserManagementDTO?> ToggleUserStatusAsync(Guid userId, bool? isActive);

        Task<UserManagementDTO?> AssignRoleAsync(Guid userId, string roleName);

        Task<UserManagementDTO?> RevokeRoleAsync(Guid userId);
    }
}
