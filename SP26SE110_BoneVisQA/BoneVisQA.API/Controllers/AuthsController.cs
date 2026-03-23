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

namespace BoneVisQA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthsController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthsController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var result = await _authService.RegisterAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Success)
        {
            return Unauthorized(result);
        }

        if (result.UserId.HasValue)
        {
            result.Token = GenerateJwtToken(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Quên mật khẩu - gửi email chứa link reset. Token nằm trong link (?token=XXX)
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new AuthResultDto
            {
                Success = false,
                Message = "Email là bắt buộc."
            });
        }

        var result = await _authService.ForgotPasswordAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Đặt lại mật khẩu. Token lấy từ link trong email (sau khi gọi forgot-password).
    /// Ví dụ link: http://localhost:5046/reset-password?token=XXX → token là phần XXX
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new AuthResultDto
            {
                Success = false,
                Message = "Token là bắt buộc."
            });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new AuthResultDto
            {
                Success = false,
                Message = "Mật khẩu mới là bắt buộc."
            });
        }

        var result = await _authService.ResetPasswordAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

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

