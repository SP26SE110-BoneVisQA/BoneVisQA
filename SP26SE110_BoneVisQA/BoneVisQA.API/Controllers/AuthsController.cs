using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

    private string GenerateJwtToken(AuthResultDto authResult)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
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

