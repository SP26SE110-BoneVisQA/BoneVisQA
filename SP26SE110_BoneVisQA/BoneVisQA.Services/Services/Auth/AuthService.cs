using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Auth;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace BoneVisQA.Services.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly BoneVisQADbContext _dbContext;
    private readonly IHostEnvironment _env;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public AuthService(IUnitOfWork unitOfWork, BoneVisQADbContext dbContext, IHostEnvironment env, IEmailService emailService, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _env = env;
        _emailService = emailService;
        _configuration = configuration;
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
                Message = "Email is already in use."
            };
        }

        var role = await _unitOfWork.RoleRepository
            .FindByCondition(r => r.Name == "Guest")
            .FirstOrDefaultAsync();

        if (role == null)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Default role 'Guest' is not configured in the system."
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
            MedicalSchool = request.MedicalSchool,
            MedicalStudentId = request.MedicalStudentId,
            VerificationStatus = "Pending",
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

        // Gửi email chào mừng (không blocking - tiếp tục dù user không nhận được mail)
        _ = _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);

        return new AuthResultDto
        {
            Success = true,
            Message = "Registration successful. Please wait for admin account activation.",
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
                Message = "Incorrect email or password."
            };
        }

        var incomingHash = HashPassword(request.Password);
        if (!string.Equals(user.Password, incomingHash, StringComparison.Ordinal))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Incorrect email or password."
            };
        }

        if (!user.IsActive)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Account is not activated yet. Please wait for admin approval."
            };
        }

        var isGuest = user.UserRoles.Any(ur => ur.Role.Name == "Guest");
        if (isGuest)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Account has not been assigned a role. Please contact admin for support."
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
            Message = "Login successful.",
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

    public async Task<AuthResultDto> ForgotPasswordAsync(ForgotPasswordRequestDto request)
    {
        var user = await _unitOfWork.UserRepository
            .FindByCondition(u => u.Email == request.Email)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return new AuthResultDto
            {
                Success = true,
                Message = "If the email exists in the system, password reset instructions will be sent."
            };
        }

        var existingTokens = await _unitOfWork.PasswordResetTokenRepository
            .FindByCondition(t => t.UserId == user.Id && !t.IsUsed)
            .ToListAsync();

        foreach (var oldToken in existingTokens)
        {
            oldToken.IsUsed = true;
            await _unitOfWork.PasswordResetTokenRepository.UpdateAsync(oldToken);
        }

        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = GenerateSecureToken(),
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.PasswordResetTokenRepository.AddAsync(resetToken);
        await _unitOfWork.SaveAsync();


        //    var baseUrl = _configuration["App:BaseUrl"] ?? "http://localhost:3000";
        //         var resetLink = $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(resetToken.Token)}";
        //var feBaseUrl = _configuration["App:FrontendUrl"] ?? "http://localhost:3000";
        //var resetLink = $"{feBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(resetToken.Token)}";


        // Development: dùng localhost:3000 | Production: dùng FrontendUrl
        var feBaseUrl = _env.IsDevelopment() 
            ? "http://localhost:3000" 
            : (_configuration["App:FrontendUrl"] ?? "http://localhost:3000");
        var resetLink = $"{feBaseUrl.TrimEnd('/')}/auth/reset-password?token={Uri.EscapeDataString(resetToken.Token)}";

        var emailSent = await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);

        if (!emailSent)
        {
            var devMessage = _env.IsDevelopment()
                ? $"Failed to send email. Dev token: {resetToken.Token}"
                : "Unable to send email. Please try again later.";
            return new AuthResultDto
            {
                Success = true, // vẫn true để UI không hiện "lỗi" rõ ràng
                Message = $"If the email exists, password reset instructions will be sent.\n[Dev: {devMessage}]"
            };
        }

        return new AuthResultDto
        {
            Success = true,
            Message = "If the email exists in the system, password reset instructions will be sent."
        };
    }

    public async Task<AuthResultDto> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "New password must be at least 6 characters."
            };
        }

        var token = request.Token?.Trim() ?? "";
        if (string.IsNullOrEmpty(token))
        {
            return new AuthResultDto { Success = false, Message = "Token is required." };
        }

        var resetToken = await _unitOfWork.PasswordResetTokenRepository
            .FindByCondition(t => t.Token == token && !t.IsUsed)
            .Include(t => t.User)
            .FirstOrDefaultAsync();

        if (resetToken == null)
        {
            var expiredToken = await _unitOfWork.PasswordResetTokenRepository
                .FindByCondition(t => t.Token == token)
                .FirstOrDefaultAsync();
            if (expiredToken != null)
            {
                return new AuthResultDto
                {
                    Success = false,
                    Message = expiredToken.IsUsed
                        ? "Token has already been used. Please request a new password reset."
                        : "Token has expired. Please request a new password reset."
                };
            }
            return new AuthResultDto
            {
                Success = false,
                Message = "Token is invalid or does not exist. Please use a new link from the email."
            };
        }

        if (resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Token has expired. Please request a new password reset."
            };
        }

        resetToken.IsUsed = true;
        resetToken.User.Password = HashPassword(request.NewPassword);
        resetToken.User.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.PasswordResetTokenRepository.UpdateAsync(resetToken);
        await _unitOfWork.UserRepository.UpdateAsync(resetToken.User);
        await _unitOfWork.SaveAsync();

        return new AuthResultDto
        {
            Success = true,
            Message = "Password reset successful. You can now log in with your new password."
        };
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public async Task<AuthResultDto> GoogleRegisterAsync(GoogleLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Google ID Token is required."
            };
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Google:ClientId"] }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (Exception)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Google token is invalid."
            };
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Google account did not provide an email."
            };
        }

        var existing = await _unitOfWork.UserRepository
            .FindByCondition(u => u.GoogleId == payload.Subject || u.Email == payload.Email)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Email is already registered. Please log in."
            };
        }

        var guestRole = await _unitOfWork.RoleRepository
            .FindByCondition(r => r.Name == "Guest")
            .FirstOrDefaultAsync();

        if (guestRole == null)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Default role 'Guest' is not configured in the system."
            };
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = payload.Name ?? payload.Email,
            Email = payload.Email,
            GoogleId = payload.Subject,
            AvatarUrl = payload.Picture,
            Password = null,
            IsActive = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _unitOfWork.UserRepository.AddAsync(user);

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = guestRole.Id,
            AssignedAt = now
        };

        await _unitOfWork.UserRoleRepository.AddAsync(userRole);
        await _unitOfWork.SaveAsync();

        _ = _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);

        return new AuthResultDto
        {
            Success = true,
            Message = "Registration successful. Please wait for admin activation and role assignment. Check your welcome email.",
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email
        };
    }

    public async Task<AuthResultDto> GoogleLoginAsync(GoogleLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Google ID Token is required."
            };
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Google:ClientId"] }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (Exception)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Google token is invalid."
            };
        }

        var user = await _unitOfWork.UserRepository
            .FindByCondition(u => u.GoogleId == payload.Subject || u.Email == payload.Email)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            var existingByEmail = await _unitOfWork.UserRepository
                .FindByCondition(u => u.Email == payload.Email)
                .FirstOrDefaultAsync();

            if (existingByEmail != null)
            {
                existingByEmail.GoogleId = payload.Subject;
                if (!string.IsNullOrEmpty(payload.Picture))
                {
                    existingByEmail.AvatarUrl = payload.Picture;
                }
                existingByEmail.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.UserRepository.UpdateAsync(existingByEmail);
                await _unitOfWork.SaveAsync();
                user = existingByEmail;
            }
            else
            {
                var guestRole = await _unitOfWork.RoleRepository
                    .FindByCondition(r => r.Name == "Guest")
                    .FirstOrDefaultAsync();

                if (guestRole == null)
                {
                    return new AuthResultDto
                    {
                        Success = false,
                        Message = "Role 'Guest' is not configured in the system."
                    };
                }

                var now = DateTime.UtcNow;
                user = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = payload.Name ?? payload.Email,
                    Email = payload.Email,
                    GoogleId = payload.Subject,
                    AvatarUrl = payload.Picture,
                    Password = null,
                    IsActive = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await _unitOfWork.UserRepository.AddAsync(user);

                var userRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = guestRole.Id,
                    AssignedAt = now
                };

                await _unitOfWork.UserRoleRepository.AddAsync(userRole);
                await _unitOfWork.SaveAsync();

                _ = _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);

                return new AuthResultDto
                {
                    Success = true,
                    Message = "Google login successful. Please verify medical information to complete registration.",
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    RequiresMedicalVerification = true
                };
            }
        }

        user = await _unitOfWork.UserRepository
            .FindByCondition(u => u.Id == user.Id)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync() ?? user;

        if (!user.IsActive)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Account is not activated yet. Please wait for admin approval."
            };
        }

        var isGuest = user.UserRoles.Any(ur => ur.Role.Name == "Guest");
        if (isGuest)
        {
            return new AuthResultDto
            {
                Success = true,
                Message = "Account is pending medical verification. Please complete your information.",
                UserId = user.Id,
                RequiresMedicalVerification = true
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
            Message = "Google login successful.",
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Roles = roles
        };
    }

    public async Task<AuthResultDto> RequestMedicalVerificationAsync(Guid userId, MedicalVerificationRequestDto request)
    {
        var user = await _unitOfWork.UserRepository
            .FindByCondition(u => u.Id == userId)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "User not found."
            };
        }

        user.MedicalSchool = request.MedicalSchool;
        user.MedicalStudentId = request.MedicalStudentId;
        user.VerificationStatus = "Pending";
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.UserRepository.UpdateAsync(user);
        await _unitOfWork.SaveAsync();

        _ = _emailService.SendMedicalVerificationRequestedEmailAsync(user.Email, user.FullName);

        return new AuthResultDto
        {
            Success = true,
            Message = "Medical student verification request has been submitted. Please wait for admin approval."
        };
    }
}
