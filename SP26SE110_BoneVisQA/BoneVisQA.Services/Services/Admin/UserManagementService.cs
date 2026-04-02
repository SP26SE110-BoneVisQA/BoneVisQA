using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly IEmailService _emailService;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(IUnitOfWork unitOfWork, IEmailService emailService, ILogger<UserManagementService> logger)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _logger = logger;
        }

        private readonly List<string> _validRoles = new()
        {
          "Student",
          "Admin",
          "Pending",
          "Expert",
          "Lecturer",
        };

        private static UserManagementDTO MapUser(User user)
        {
            return new UserManagementDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                SchoolCohort = user.SchoolCohort,
                LastLogin = user.LastLogin,
                Roles = user.UserRoles.Select(r => r.Role.Name).ToList(),
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        private async Task<User?> GetUserWithRolesAsync(Guid userId)
        {
            return await _unitOfWork.Context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<List<UserManagementDTO>> GetUserByRoleAsync(string role)
        {
            if (!_validRoles.Contains(role)) throw new ArgumentException("Role not found");

            var users = await _unitOfWork.UserRepository.GetAllAsync(q =>
                q.Include(u => u.UserRoles)
                 .ThenInclude(ur => ur.Role)
            );

            var result = users
                .Where(u => u.UserRoles.Any(r => r.Role.Name == role))
                .Select(MapUser)
                .ToList();

            return result;
        }

        public async Task<List<UserManagementDTO>> GetAllUsersAsync()
        {

            var users = await _unitOfWork.UserRepository.GetAllAsync(q =>
                q.Include(u => u.UserRoles)
                 .ThenInclude(ur => ur.Role)
            );

            return users.Select(u => new UserManagementDTO 
            {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? "",
                    SchoolCohort = u.SchoolCohort,
                    LastLogin = u.LastLogin,
                    Roles = u.UserRoles.Select(r => r.Role.Name).ToList(),
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt
            }).ToList();
        }

        public async Task<UserManagementDTO?> ActivateUserAccountAsync(Guid userId)
        {
            var user = await GetUserWithRolesAsync(userId);

            if (user == null) return null;

            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            return MapUser(user);
        }

        public async Task<UserManagementDTO?> DeactivateUserAccountAsync(Guid userId)
        {
            var user = await GetUserWithRolesAsync(userId);

            if (user == null) return null;

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            return MapUser(user);
        }

        //public async Task<UserManagementDTO?> ToggleUserStatusAsync(Guid userId, bool? isActive)
        //{
        //    var user = await GetUserWithRolesAsync(userId);
        //    if (user == null) return null;

        //    user.IsActive = isActive ?? !user.IsActive;
        //    user.UpdatedAt = DateTime.UtcNow;

        //    await _unitOfWork.UserRepository.UpdateAsync(user);
        //    await _unitOfWork.SaveAsync();

        //    return MapUser(user);
        //}
        public async Task<UserManagementDTO?> AssignRoleAsync(Guid userId, string roleName)
        {
            if (!_validRoles.Contains(roleName)) throw new ArgumentException("Role not found");

            var user = await GetUserWithRolesAsync(userId);
            if (user == null) return null;

            var role = await _unitOfWork.RoleRepository
                .FirstOrDefaultAsync(r => r.Name == roleName);

            if (role == null) return null;

            var currentRoles = await _unitOfWork.UserRoleRepository
                .FindAsync(x => x.UserId == userId);

            // Xóa role cũ
            foreach (var ur in currentRoles)
            {
                await _unitOfWork.UserRoleRepository.RemoveAsync(ur);
            }

            // Thêm role mới
            var newUserRole = new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            };
            await _unitOfWork.UserRoleRepository.AddAsync(newUserRole);

            // Tự động kích hoạt tài khoản khi được gán vai trò hợp lệ
            var wasInactive = !user.IsActive;
            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.UserRepository.UpdateAsync(user);

            await _unitOfWork.SaveAsync();

            // Gửi email thông báo (không chờ kết quả)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendRoleAssignedEmailAsync(
                        user.Email, user.FullName, roleName, wasInactive);
                    _logger.LogInformation(
                        "[AssignRoleAsync] Role '{Role}' assigned + account activated. Email sent to {Email}",
                        roleName, user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[AssignRoleAsync] Failed to send role assignment email to {Email}", user.Email);
                }
            });

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

        public async Task<UserManagementDTO?> RevokeRoleAsync(Guid userId)
        {
            var user = await GetUserWithRolesAsync(userId)
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
