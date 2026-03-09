using BoneVisQA.Services.Models.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces
{
    public interface IUserManagementService
    {
        Task<List<UserManagementDTO>> GetUserByRoleAsync(string role);

        Task<bool> ActivateUserAccountAsync(Guid userId);

        Task<bool> DeactivateUserAccountAsync(Guid userId);

        Task<bool> AssignRoleAsync(Guid userId, string roleName);

        Task<bool> RevokeRoleAsync(Guid userId, string roleName);
    }
}
