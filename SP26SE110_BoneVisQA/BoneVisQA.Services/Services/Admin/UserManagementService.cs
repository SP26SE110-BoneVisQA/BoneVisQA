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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
          "Guest",
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
                UpdatedAt = user.UpdatedAt,
                ClassAssignments = new List<UserClassAssignmentDto>()
            };
        }

        private async Task<Dictionary<Guid, List<UserClassAssignmentDto>>> LoadClassAssignmentsForUserIdsAsync(
            IReadOnlyList<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            var result = userIds.Distinct().ToDictionary(id => id, _ => new List<UserClassAssignmentDto>());
            if (result.Count == 0)
                return result;

            var enrollments = await _unitOfWork.Context.ClassEnrollments
                .AsNoTracking()
                .Where(e => userIds.Contains(e.StudentId))
                .Select(e => new
                {
                    e.StudentId,
                    e.ClassId,
                    Name = e.Class.ClassName,
                    e.EnrolledAt
                })
                .ToListAsync(cancellationToken);
            foreach (var e in enrollments)
            {
                if (!result.TryGetValue(e.StudentId, out var list))
                    continue;
                list.Add(new UserClassAssignmentDto
                {
                    ClassId = e.ClassId,
                    ClassName = e.Name,
                    RoleInClass = "Student",
                    EnrolledAt = e.EnrolledAt
                });
            }

            var lecturerClasses = await _unitOfWork.Context.AcademicClasses
                .AsNoTracking()
                .Where(c => c.LecturerId != null && userIds.Contains(c.LecturerId.Value))
                .Select(c => new { UserId = c.LecturerId!.Value, c.Id, c.ClassName })
                .ToListAsync(cancellationToken);
            foreach (var c in lecturerClasses)
            {
                if (!result.TryGetValue(c.UserId, out var list))
                    continue;
                list.Add(new UserClassAssignmentDto
                {
                    ClassId = c.Id,
                    ClassName = c.ClassName,
                    RoleInClass = "Lecturer",
                    EnrolledAt = null
                });
            }

            var expertClasses = await _unitOfWork.Context.AcademicClasses
                .AsNoTracking()
                .Where(c => c.ExpertId != null && userIds.Contains(c.ExpertId.Value))
                .Select(c => new { UserId = c.ExpertId!.Value, c.Id, c.ClassName })
                .ToListAsync(cancellationToken);
            foreach (var c in expertClasses)
            {
                if (!result.TryGetValue(c.UserId, out var list))
                    continue;
                list.Add(new UserClassAssignmentDto
                {
                    ClassId = c.Id,
                    ClassName = c.ClassName,
                    RoleInClass = "Expert",
                    EnrolledAt = null
                });
            }

            return result;
        }

        private async Task EnrichClassAssignmentsAsync(List<UserManagementDTO> items, CancellationToken cancellationToken = default)
        {
            if (items.Count == 0)
                return;
            var map = await LoadClassAssignmentsForUserIdsAsync(items.Select(i => i.Id).ToList(), cancellationToken);
            foreach (var dto in items)
            {
                if (!map.TryGetValue(dto.Id, out var list))
                    continue;
                dto.ClassAssignments = list
                    .OrderBy(x => x.ClassName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.RoleInClass, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
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

            var result = await _unitOfWork.Context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles.Any(r => r.Role.Name == role))
                .OrderByDescending(u => u.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(u => u.Id)
                .Select(u => new UserManagementDTO
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? string.Empty,
                    SchoolCohort = u.SchoolCohort,
                    LastLogin = u.LastLogin,
                    Roles = u.UserRoles.Select(r => r.Role.Name).ToList(),
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt
                })
                .ToListAsync();

            return result;
        }

        public async Task<List<UserManagementDTO>> GetAllUsersAsync()
        {
            var users = await _unitOfWork.Context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .OrderByDescending(u => u.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(u => u.Id)
                // Exclude Admin role; must be translatable to SQL (no local helper in Where).
                .Where(u => !u.UserRoles.Any(r => r.Role.Name.ToLower() == "admin"))
                .ToListAsync();

            var items = users.Select(MapUser).ToList();
            await EnrichClassAssignmentsAsync(items);
            return items;
        }

        public async Task<PagedUsersResultDto> GetUsersPagedAsync(int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var baseQuery = _unitOfWork.Context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(u => !u.UserRoles.Any(r => r.Role.Name.ToLower() == "admin"));

            var totalCount = await baseQuery.CountAsync();

            var users = await baseQuery
                .OrderByDescending(u => u.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = users.Select(MapUser).ToList();
            await EnrichClassAssignmentsAsync(items);

            return new PagedUsersResultDto
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<UserManagementDTO?> ActivateUserAccountAsync(Guid userId)
        {
            var user = await GetUserWithRolesAsync(userId);
            if (user == null) return null;

            var wasInactive = !user.IsActive;
            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            // Send notification email (fire-and-forget)
            if (wasInactive)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendAccountActivatedEmailAsync(user.Email, user.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ActivateUserAccountAsync] Failed to send activation email to {Email}", user.Email);
                    }
                });
            }

            return MapUser(user);
        }

        public async Task<UserManagementDTO?> DeactivateUserAccountAsync(Guid userId)
        {
            var user = await GetUserWithRolesAsync(userId);
            if (user == null) return null;

            var wasActive = user.IsActive;
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            // Send notification email (fire-and-forget)
            if (wasActive)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendAccountDeactivatedEmailAsync(user.Email, user.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[DeactivateUserAccountAsync] Failed to send deactivation email to {Email}", user.Email);
                    }
                });
            }

            return MapUser(user);
        }

        public async Task<UserManagementDTO?> ToggleUserStatusAsync(Guid userId, bool? isActive)
        {
            var user = await GetUserWithRolesAsync(userId);
            if (user == null) return null;

            var newState = isActive ?? !user.IsActive;
            var wasChanging = user.IsActive != newState;
            user.IsActive = newState;
            user.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            if (wasChanging)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (newState)
                            await _emailService.SendAccountActivatedEmailAsync(user.Email, user.FullName);
                        else
                            await _emailService.SendAccountDeactivatedEmailAsync(user.Email, user.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ToggleUserStatusAsync] Failed to send status email to {Email}", user.Email);
                    }
                });
            }

            return MapUser(user);
        }

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

            // Snapshot cho Task.Run — không đụng DbContext sau khi request kết thúc
            var emailForMail = user.Email ?? string.Empty;
            var fullNameForMail = user.FullName ?? string.Empty;
            var verificationApproved = string.Equals(user.VerificationStatus, "Approved", StringComparison.Ordinal);
            var assignedRoleName = roleName;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (verificationApproved)
                    {
                        await _emailService.SendWelcomeWithRoleEmailAsync(emailForMail, fullNameForMail, assignedRoleName);
                        _logger.LogInformation(
                            "[AssignRoleAsync] Role '{Role}' assigned + verified. Welcome email sent to {Email}",
                            assignedRoleName, emailForMail);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "[AssignRoleAsync] Role '{Role}' assigned but not verified yet. No email sent to {Email}",
                            assignedRoleName, emailForMail);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[AssignRoleAsync] Failed to send email to {Email}", emailForMail);
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

            var guestRole = await _unitOfWork.RoleRepository
                .FirstOrDefaultAsync(r => r.Name == "Guest")
                ?? throw new Exception("Guest role not found");

            var hasGuest = await _unitOfWork.UserRoleRepository
                .ExistsAsync(x => x.UserId == userId && x.RoleId == guestRole.Id);
            if (hasGuest)
                throw new InvalidOperationException("User already has Guest role.");

            // Xóa tất cả role hiện tại
            var userRoles = await _unitOfWork.UserRoleRepository
                .FindAsync(x => x.UserId == userId);
            foreach (var ur in userRoles)
                await _unitOfWork.UserRoleRepository.RemoveAsync(ur);

            await _unitOfWork.UserRoleRepository.AddAsync(new UserRole
            {
                UserId = userId,
                RoleId = guestRole.Id
            });

            await _unitOfWork.SaveAsync();

            return new UserManagementDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Roles = new List<string> { "Guest" },
                SchoolCohort = user.SchoolCohort,
                LastLogin = user.LastLogin,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        // ── GET by ID ───────────────────────────────────────────────────────
        public async Task<UserManagementDTO?> GetUserByIdAsync(Guid userId)
        {
            var user = await GetUserWithRolesAsync(userId);
            return user == null ? null : MapUser(user);
        }

        // ── CREATE ─────────────────────────────────────────────────────────
        public async Task<UserManagementDTO?> CreateUserAsync(CreateUserRequestDto request)
        {
            if (!_validRoles.Contains(request.Role))
                throw new ArgumentException($"Invalid role '{request.Role}'.");

            // Check duplicate email
            var existing = await _unitOfWork.UserRepository
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());
            if (existing != null)
                throw new InvalidOperationException($"Email '{request.Email}' is already in use.");

            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = request.FullName,
                Email = request.Email,
                Password = HashPassword(request.Password),
                SchoolCohort = request.SchoolCohort,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            var role = await _unitOfWork.RoleRepository
                .FirstOrDefaultAsync(r => r.Name == request.Role)
                ?? throw new InvalidOperationException($"Role '{request.Role}' not found in database.");

            var userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = role.Id,
                AssignedAt = now
            };

            await _unitOfWork.UserRepository.AddAsync(user);
            await _unitOfWork.UserRoleRepository.AddAsync(userRole);
            await _unitOfWork.SaveAsync();

            _logger.LogInformation("[CreateUserAsync] User {Email} created with role {Role}.", user.Email, request.Role);

            // Send welcome email (fire-and-forget)
            if (request.SendWelcomeEmail)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendRoleAssignedEmailAsync(user.Email, user.FullName, request.Role, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[CreateUserAsync] Failed to send welcome email to {Email}", user.Email);
                    }
                });
            }

            return MapUser(user);
        }

        // ── UPDATE ─────────────────────────────────────────────────────────
        public async Task<UserManagementDTO?> UpdateUserAsync(Guid userId, UpdateUserRequestDto request)
        {
            var user = await GetUserWithRolesAsync(userId);
            if (user == null) return null;

            user.FullName = request.FullName;
            user.SchoolCohort = request.SchoolCohort;
            user.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            _logger.LogInformation("[UpdateUserAsync] User {Id} updated by admin.", userId);
            return MapUser(user);
        }

        // ── DELETE ─────────────────────────────────────────────────────────
        public async Task<bool> DeleteUserAsync(Guid userId)
        {
            var user = await GetUserWithRolesAsync(userId);
            if (user == null) return false;

            // Remove all UserRole associations first
            var userRoles = await _unitOfWork.UserRoleRepository.FindAsync(ur => ur.UserId == userId);
            foreach (var ur in userRoles)
                await _unitOfWork.UserRoleRepository.RemoveAsync(ur);

            // Remove the user
            await _unitOfWork.UserRepository.RemoveAsync(user);
            await _unitOfWork.SaveAsync();

            _logger.LogWarning("[DeleteUserAsync] User {Id} ({Email}) permanently deleted by admin.",
                userId, user.Email);
            return true;
        }

        // ── Medical Student Verification ─────────────────────────────────────────

        public async Task<List<PendingVerificationDto>> GetPendingVerificationsAsync()
        {
            var pendingUsers = await _unitOfWork.UserRepository.GetAllAsync(q =>
                q.Include(u => u.UserRoles)
                 .ThenInclude(ur => ur.Role)
                .Where(u => u.VerificationStatus == "Pending")
            );

            return pendingUsers.Select(u => new PendingVerificationDto
            {
                UserId = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                SchoolCohort = u.SchoolCohort,
                MedicalSchool = u.MedicalSchool,
                MedicalStudentId = u.MedicalStudentId,
                VerificationStatus = u.VerificationStatus,
                CreatedAt = u.CreatedAt
            }).ToList();
        }

        public async Task<UserManagementDTO?> ApproveMedicalVerificationAsync(Guid userId, bool isApproved, string? notes, Guid adminId)
        {
            var user = await GetUserWithRolesAsync(userId);
            if (user == null) return null;

            user.VerificationStatus = isApproved ? "Approved" : "Rejected";
            user.VerificationNotes = notes;
            user.VerifiedAt = DateTime.UtcNow;
            user.VerifiedBy = adminId;
            user.UpdatedAt = DateTime.UtcNow;

            if (isApproved)
            {
                user.IsActive = true;

                // Explicitly resolve role ids from roles table (DB-first contract).
                var studentRole = await _unitOfWork.Context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Name == "Student")
                    ?? throw new InvalidOperationException("Role 'Student' not found in database.");

                var guestRole = await _unitOfWork.Context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Name == "Guest");

                var currentUserRoles = await _unitOfWork.Context.UserRoles
                    .Where(ur => ur.UserId == userId)
                    .ToListAsync();

                // Replace any Guest roles with Student.
                if (guestRole != null)
                {
                    var guestAssignments = currentUserRoles
                        .Where(ur => ur.RoleId == guestRole.Id)
                        .ToList();
                    foreach (var assignment in guestAssignments)
                    {
                        await _unitOfWork.UserRoleRepository.RemoveAsync(assignment);
                    }
                }

                var hasStudentRole = currentUserRoles.Any(ur => ur.RoleId == studentRole.Id);
                if (!hasStudentRole)
                {
                    await _unitOfWork.UserRoleRepository.AddAsync(new UserRole
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        RoleId = studentRole.Id,
                        AssignedAt = DateTime.UtcNow
                    });
                }

                _logger.LogInformation(
                    "[ApproveMedicalVerificationAsync] Verified user {UserId}; Guest role replaced and Student role ensured.",
                    userId);
            }

            await _unitOfWork.UserRepository.UpdateAsync(user);
            await _unitOfWork.SaveAsync();

            // Không dùng DbContext trong Task.Run — capture dữ liệu trước
            var emailForMail = user.Email ?? string.Empty;
            var fullNameForMail = user.FullName ?? string.Empty;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (isApproved)
                    {
                        await _emailService.SendWelcomeWithRoleEmailAsync(emailForMail, fullNameForMail, "Student");
                        _logger.LogInformation("[ApproveMedicalVerificationAsync] Verification approved. Welcome email sent to {Email}.", emailForMail);
                    }
                    else
                    {
                        await _emailService.SendMedicalVerificationRejectedEmailAsync(emailForMail, fullNameForMail, notes);
                        _logger.LogInformation("[ApproveMedicalVerificationAsync] Verification rejected for {Email}.", emailForMail);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ApproveMedicalVerificationAsync] Failed to send email to {Email}", emailForMail);
                }
            });

            return MapUser(user);
        }

        // ── Hash helper ─────────────────────────────────────────────────────────
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var sb = new StringBuilder();
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
