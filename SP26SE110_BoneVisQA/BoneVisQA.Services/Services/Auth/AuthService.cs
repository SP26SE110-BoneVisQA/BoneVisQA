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
            IsMedicalStudent = request.IsMedicalStudent,
            MedicalSchool = request.MedicalSchool,
            MedicalStudentId = request.MedicalStudentId,
            VerificationStatus = request.IsMedicalStudent ? "Pending" : null,
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
                Message = "Tài khoản chưa được kích hoạt. Vui lòng chờ admin phê duyệt."
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
                Message = "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu sẽ được gửi."
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
                ? $"Email gửi thất bại. Token dev: {resetToken.Token}"
                : "Không thể gửi email. Vui lòng thử lại sau.";
            return new AuthResultDto
            {
                Success = true, // vẫn true để UI không hiện "lỗi" rõ ràng
                Message = $"Nếu email tồn tại, hướng dẫn đặt lại mật khẩu sẽ được gửi.\n[Dev: {devMessage}]"
            };
        }

        return new AuthResultDto
        {
            Success = true,
            Message = "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu sẽ được gửi."
        };
    }

    public async Task<AuthResultDto> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Mật khẩu mới phải có ít nhất 6 ký tự."
            };
        }

        var token = request.Token?.Trim() ?? "";
        if (string.IsNullOrEmpty(token))
        {
            return new AuthResultDto { Success = false, Message = "Token là bắt buộc." };
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
                        ? "Token đã được sử dụng. Vui lòng yêu cầu đặt lại mật khẩu mới."
                        : "Token đã hết hạn. Vui lòng yêu cầu đặt lại mật khẩu mới."
                };
            }
            return new AuthResultDto
            {
                Success = false,
                Message = "Token không hợp lệ hoặc không tồn tại. Vui lòng dùng link mới từ email."
            };
        }

        if (resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Token đã hết hạn. Vui lòng yêu cầu đặt lại mật khẩu mới."
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
            Message = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập với mật khẩu mới."
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
                Message = "Google ID Token là bắt buộc."
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
                Message = "Token Google không hợp lệ."
            };
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Tài khoản Google không cung cấp email."
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
                Message = "Email đã được đăng ký. Vui lòng đăng nhập."
            };
        }

        var pendingRole = await _unitOfWork.RoleRepository
            .FindByCondition(r => r.Name == "Pending")
            .FirstOrDefaultAsync();

        if (pendingRole == null)
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
            RoleId = pendingRole.Id,
            AssignedAt = now
        };

        await _unitOfWork.UserRoleRepository.AddAsync(userRole);
        await _unitOfWork.SaveAsync();

        _ = _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);

        return new AuthResultDto
        {
            Success = true,
            Message = "Đăng ký thành công. Vui lòng chờ admin kích hoạt và gán vai trò. Kiểm tra email chào mừng.",
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
                Message = "Google ID Token là bắt buộc."
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
                Message = "Token Google không hợp lệ."
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
                var pendingRole = await _unitOfWork.RoleRepository
                    .FindByCondition(r => r.Name == "Pending")
                    .FirstOrDefaultAsync();

                if (pendingRole == null)
                {
                    return new AuthResultDto
                    {
                        Success = false,
                        Message = "Role 'Pending' chưa được cấu hình trong hệ thống."
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
                    RoleId = pendingRole.Id,
                    AssignedAt = now
                };

                await _unitOfWork.UserRoleRepository.AddAsync(userRole);
                await _unitOfWork.SaveAsync();

                _ = _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);

                return new AuthResultDto
                {
                    Success = true,
                    Message = "Đăng nhập Google thành công. Vui lòng xác nhận thông tin y khoa để hoàn tất đăng ký.",
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
                Message = "Tài khoản chưa được kích hoạt. Vui lòng chờ admin phê duyệt."
            };
        }

        var isPending = user.UserRoles.Any(ur => ur.Role.Name == "Pending");
        if (isPending)
        {
            return new AuthResultDto
            {
                Success = true,
                Message = "Tài khoản đang chờ xác minh y khoa. Vui lòng hoàn tất thông tin.",
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
            Message = "Đăng nhập Google thành công.",
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
                Message = "Không tìm thấy người dùng."
            };
        }

        user.IsMedicalStudent = true;
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
            Message = "Yêu cầu xác nhận sinh viên y khoa đã được gửi. Vui lòng chờ admin duyệt."
        };
    }
}
