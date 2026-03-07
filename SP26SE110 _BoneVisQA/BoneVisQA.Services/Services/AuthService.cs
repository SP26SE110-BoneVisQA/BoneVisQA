using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BoneVisQA.Repositories;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Auth;

namespace BoneVisQA.Services.Services;

public class AuthService : IAuthService
{
    private readonly AuthRepository _authRepository;

    public AuthService(AuthRepository authRepository)
    {
        _authRepository = authRepository;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterRequestDto request)
    {
        var existing = await _authRepository.GetByEmailAsync(request.Email);
        if (existing != null)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Email đã được sử dụng."
            };
        }

        var role = await _authRepository.GetRoleByNameAsync(request.RoleName);
        if (role == null)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = $"Role '{request.RoleName}' không tồn tại."
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
            CreatedAt = now,
            UpdatedAt = now
        };

        await _authRepository.CreateUserAsync(user);

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = role.Id,
            AssignedAt = now
        };

        await _authRepository.AddUserRoleAsync(userRole);

        return new AuthResultDto
        {
            Success = true,
            Message = "Đăng ký thành công.",
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email
        };
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _authRepository.GetByEmailAsync(request.Email);
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

        user.LastLogin = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _authRepository.UpdateUserAsync(user);

        return new AuthResultDto
        {
            Success = true,
            Message = "Đăng nhập thành công.",
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email
        };
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}

