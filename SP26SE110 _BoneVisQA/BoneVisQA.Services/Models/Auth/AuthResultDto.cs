using System;

namespace BoneVisQA.Services.Models.Auth;

public class AuthResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Token { get; set; }
}

