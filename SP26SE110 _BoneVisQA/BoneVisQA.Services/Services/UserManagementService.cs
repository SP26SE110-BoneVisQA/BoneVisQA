using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Admin;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services
{
    public class UserManagementService : IUserManagementService
    {
        public readonly IUnitOfWork _unitOfWork;

        public UserManagementService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<List<UserManagementDTO>> GetUserByRoleAsync(string role)
        {
            var users = await _unitOfWork.UserRepository.GetAllAsync(q =>
                q.Include(u => u.UserRoles)
                 .ThenInclude(ur => ur.Role)
            );

            var result = users
                .Where(u => u.UserRoles.Any(r => r.Role.Name == role))
                .Select(u => new UserManagementDTO
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    SchoolCohort = u.SchoolCohort,
                    LastLogin = u.LastLogin,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt
                })
                .ToList();

            return result;
        }

        public async Task<bool> ActivateUserAccountAsync(Guid userId)
        {
            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId);

            if (user == null) return false;

            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            return true;
        }

        public async Task<bool> DeactivateUserAccountAsync(Guid userId)
        {
            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId);

            if (user == null) return false;

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            return true;
        }
        public async Task<bool> AssignRoleAsync(Guid userId, string roleName)
        {
            var role = await _unitOfWork.RoleRepository
                .FirstOrDefaultAsync(r => r.Name == roleName);

            if (role == null) return false;

            var exists = await _unitOfWork.UserRoleRepository
                .ExistsAsync(x => x.UserId == userId && x.RoleId == role.Id);

            if (exists) return true;

            var userRole = new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            };

            await _unitOfWork.UserRoleRepository.AddAsync(userRole);
            await _unitOfWork.SaveAsync();

            return true;
        }

        public async Task<bool> RevokeRoleAsync(Guid userId, string roleName)
        {
            var role = await _unitOfWork.RoleRepository
                .FirstOrDefaultAsync(r => r.Name == roleName);

            if (role == null) return false;

            var userRole = await _unitOfWork.UserRoleRepository
                .FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == role.Id);

            if (userRole == null) return false;

            await _unitOfWork.UserRoleRepository.RemoveAsync(userRole);
            await _unitOfWork.SaveAsync();

            return true;
        }
    }
}
