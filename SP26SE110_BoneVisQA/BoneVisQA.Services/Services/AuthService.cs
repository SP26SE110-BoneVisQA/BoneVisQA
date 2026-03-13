using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Auth;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BoneVisQA.Services.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHostEnvironment _env;

    public AuthService(IUnitOfWork unitOfWork, IHostEnvironment env)
    {
        _unitOfWork = unitOfWork;
        _env = env;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterRequestDto request)
    {
        var existing = await _unitOfWork.UserRepository
            .FindByCondition(u => u.Email == request.Email)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Email đã được sử dụng."
            };
        }

        var role = await _unitOfWork.RoleRepository
            .FindByCondition(r => r.Name == "Pending")
            .FirstOrDefaultAsync();

        if (role == null)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Role mặc định 'Pending' chưa được cấu hình trong hệ thống."
            };
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            Password = HashPassword(request.Password),
            SchoolCohort = request.SchoolCohort,
            IsActive = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _unitOfWork.UserRepository.AddAsync(user);

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = role.Id,
            AssignedAt = now
        };

        await _unitOfWork.UserRoleRepository.AddAsync(userRole);
        await _unitOfWork.SaveAsync();

        return new AuthResultDto
        {
            Success = true,
            Message = "Đăng ký thành công. Vui lòng chờ admin kích hoạt tài khoản.",
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email
        };
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _unitOfWork.UserRepository
            .FindByCondition(u => u.Email == request.Email)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Email hoặc mật khẩu không đúng."
            };
        }

        var incomingHash = HashPassword(request.Password);
        if (!string.Equals(user.Password, incomingHash, StringComparison.Ordinal))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Email hoặc mật khẩu không đúng."
            };
        }

        if (!user.IsActive)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Tài khoản chưa được kích hoạt. Vui lòng liên hệ admin để được hỗ trợ."
            };
        }

        var isPending = user.UserRoles.Any(ur => ur.Role.Name == "Pending");
        if (isPending)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Tài khoản chưa được gán vai trò, liên hệ admin để được hỗ trợ."
            };
        }

        user.LastLogin = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.UserRepository.UpdateAsync(user);
        await _unitOfWork.SaveAsync();

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        return new AuthResultDto
        {
            Success = true,
            Message = "Đăng nhập thành công.",
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Roles = roles
        };
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
