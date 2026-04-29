using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BoneVisQA.API.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
[Tags("Auth")]
[AllowAnonymous]
public class AuthsController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly ISystemLogService _systemLogService;

    public AuthsController(IAuthService authService, IConfiguration configuration, ISystemLogService systemLogService)
    {
        _authService = authService;
        _configuration = configuration;
        _systemLogService = systemLogService;
    }

    private string? GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var ip = GetClientIp();

        var result = await _authService.RegisterAsync(request);
        if (!result.Success)
        {
            await _systemLogService.LogWarningAsync("Auth", $"Registration failed: {result.Message}", request.Email, ip);
            return BadRequest(result);
        }

        await _systemLogService.LogSuccessAsync("Auth", "New user registered successfully", request.Email, ip);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var ip = GetClientIp();

        var result = await _authService.LoginAsync(request);
        if (!result.Success)
        {
            await _systemLogService.LogWarningAsync("Auth", $"Login failed - invalid credentials for {request.Email}", request.Email, ip);
            return Unauthorized(result);
        }

        if (result.UserId.HasValue)
        {
            result.Token = GenerateJwtToken(result);
        }

        await _systemLogService.LogSuccessAsync("Auth", "User login successful", request.Email, ip);
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value
                       ?? User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var ip = GetClientIp();

        await _systemLogService.LogInfoAsync("Auth", "User logout", userEmail, ip);
        return Ok(new { success = true, message = "Logged out successfully" });
    }

    /// <summary>
    /// Quên mật khẩu - gửi email chứa link reset. Token nằm trong link (?token=XXX)
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        var ip = GetClientIp();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new AuthResultDto
            {
                Success = false,
                Message = "Email is required."
            });
        }

        var result = await _authService.ForgotPasswordAsync(request);
        await _systemLogService.LogInfoAsync("Auth", "Password reset requested", request.Email, ip);
        return Ok(result);
    }

    /// <summary>
    /// Reset Password. Token lấy từ link trong email (sau khi gọi forgot-password).
    /// Ví dụ link: http://localhost:5046/reset-password?token=XXX → token là phần XXX
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        var ip = GetClientIp();

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new AuthResultDto
            {
                Success = false,
                Message = "Token is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new AuthResultDto
            {
                Success = false,
                Message = "New password is required."
            });
        }

        var result = await _authService.ResetPasswordAsync(request);
        if (!result.Success)
        {
            await _systemLogService.LogWarningAsync("Auth", $"Password reset failed: {result.Message}", null, ip);
            return BadRequest(result);
        }

        await _systemLogService.LogSuccessAsync("Auth", "Password reset completed", null, ip);
        return Ok(result);
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto request)
    {
        var ip = GetClientIp();

        var result = await _authService.GoogleLoginAsync(request);
        if (!result.Success)
        {
            await _systemLogService.LogWarningAsync("Auth", $"Google login failed for {result.Email}: {result.Message}", result.Email, ip);
            return Unauthorized(result);
        }

        if (result.UserId.HasValue)
        {
            result.Token = GenerateJwtToken(result);
        }

        await _systemLogService.LogSuccessAsync("Auth", "Google login successful", result.Email, ip);
        return Ok(result);
    }

    /// <summary>
    /// Đăng ký bằng Google: tạo tài khoản Pending, gửi email chào mừng, chờ admin gán vai trò.
    /// </summary>
    [HttpPost("google-register")]
    public async Task<IActionResult> GoogleRegister([FromBody] GoogleLoginRequestDto request)
    {
        var ip = GetClientIp();

        var result = await _authService.GoogleRegisterAsync(request);
        if (!result.Success)
        {
            await _systemLogService.LogWarningAsync("Auth", $"Google registration failed: {result.Message}", result.Email, ip);
            return BadRequest(result);
        }

        await _systemLogService.LogSuccessAsync("Auth", "Google user registered (pending verification)", result.Email, ip);
        return Ok(result);
    }

    /// <summary>
    /// Gửi yêu cầu xác minh sinh viên y khoa (dành cho user đã đăng nhập bằng Google).
    /// </summary>
    [HttpPost("request-medical-verification")]
    public async Task<IActionResult> RequestMedicalVerification(
        [FromBody] MedicalVerificationRequestDto request,
        [FromHeader(Name = "X-User-Id")] string? headerUserId = null)
    {
        var ip = GetClientIp();
        Guid userId;

        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out var claimUserId))
        {
            userId = claimUserId;
        }
        else if (!string.IsNullOrWhiteSpace(headerUserId) && Guid.TryParse(headerUserId, out var headerUid))
        {
            userId = headerUid;
        }
        else
        {
            return Unauthorized(new AuthResultDto
            {
                Success = false,
                Message = "Unable to identify the user."
            });
        }

        var result = await _authService.RequestMedicalVerificationAsync(userId, request);
        if (!result.Success)
        {
            await _systemLogService.LogWarningAsync("Auth", $"Medical verification request failed: {result.Message}", userEmail ?? headerUserId, ip);
            return BadRequest(result);
        }

        await _systemLogService.LogInfoAsync("Auth", "Medical verification requested", userEmail ?? headerUserId, ip);
        return Ok(result);
    }

    private string GenerateJwtToken(AuthResultDto authResult)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];

        // HS256 requires at least 256 bits (32 bytes). Derive key via SHA256 if needed.
        var keyBytes = Encoding.UTF8.GetBytes(key);
        if (keyBytes.Length < 32)
            keyBytes = SHA256.HashData(keyBytes);

        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, authResult.UserId?.ToString() ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Email, authResult.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.UniqueName, authResult.FullName ?? string.Empty)
        };

        if (authResult.Roles != null)
        {
            foreach (var role in authResult.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
