using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Admin
{
    public class UserManagementService : IUserManagementService
    {
        public readonly IUnitOfWork _unitOfWork;

        public UserManagementService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        private readonly List<string> _validRoles = new()
        {
          "Student",
          "Admin",
          "Pending",
          "Expert",
          "Lecturer",
        };
        public async Task<List<UserManagementDTO>> GetUserByRoleAsync(string role)
        {
            if (!_validRoles.Contains(role)) throw new ArgumentException("Role not found");
          
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
                    Roles = u.UserRoles.Select(r => r.Role.Name).ToList(),
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt
                })
                .ToList();

            return result;
        }

        public async Task<UserManagementDTO> ActivateUserAccountAsync(Guid userId)
        {
            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId);

            if (user == null) return null;

            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            return new UserManagementDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                SchoolCohort = user.SchoolCohort,
                LastLogin = user.LastLogin,
                IsActive = user.IsActive,
                Roles = user.UserRoles.Select(r => r.Role.Name).ToList(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        public async Task<UserManagementDTO> DeactivateUserAccountAsync(Guid userId)
        {
            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId);

            if (user == null) return null;

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            return new UserManagementDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Roles = user.UserRoles.Select(r => r.Role.Name).ToList(),
                SchoolCohort = user.SchoolCohort,
                LastLogin = user.LastLogin,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
        public async Task<UserManagementDTO> AssignRoleAsync(Guid userId, string roleName)
        {
            if (!_validRoles.Contains(roleName)) throw new ArgumentException("Role not found");
           
            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var role = await _unitOfWork.RoleRepository
                .FirstOrDefaultAsync(r => r.Name == roleName);

            if (role == null) return null;

            var currentRoles = await _unitOfWork.UserRoleRepository
         .FindAsync(x => x.UserId == userId);

            // xóa role cũ
            foreach (var ur in currentRoles)
            {
                await _unitOfWork.UserRoleRepository.RemoveAsync(ur);
            }

            // thêm role mới
            var newUserRole = new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            };

            await _unitOfWork.UserRoleRepository.AddAsync(newUserRole);

            await _unitOfWork.SaveAsync();

            return new UserManagementDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Roles = new List<string> { roleName },
                SchoolCohort = user.SchoolCohort,
                LastLogin = user.LastLogin,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        public async Task<UserManagementDTO> RevokeRoleAsync(Guid userId)
        {
            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found");

            var pendingRole = await _unitOfWork.RoleRepository
                .FirstOrDefaultAsync(r => r.Name == "Pending")
                ?? throw new Exception("Pending role not found");

            var hasPending = await _unitOfWork.UserRoleRepository
                .ExistsAsync(x => x.UserId == userId && x.RoleId == pendingRole.Id);
            if (hasPending)
                throw new InvalidOperationException("User already has Pending role.");

            // Xóa tất cả role hiện tại
            var userRoles = await _unitOfWork.UserRoleRepository
                .FindAsync(x => x.UserId == userId);
            foreach (var ur in userRoles)
                await _unitOfWork.UserRoleRepository.RemoveAsync(ur);

            await _unitOfWork.UserRoleRepository.AddAsync(new UserRole
            {
                UserId = userId,
                RoleId = pendingRole.Id
            });

            await _unitOfWork.SaveAsync();

            return new UserManagementDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Roles = new List<string> { "Pending" },
                SchoolCohort = user.SchoolCohort,
                LastLogin = user.LastLogin,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
    }
}
