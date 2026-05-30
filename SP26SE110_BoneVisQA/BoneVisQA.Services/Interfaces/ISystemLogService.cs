namespace BoneVisQA.Services.Interfaces;

public interface ISystemLogService
{
    Task LogAsync(string level, string category, string message, string? userEmail = null, string? ipAddress = null);
    Task LogInfoAsync(string category, string message, string? userEmail = null, string? ipAddress = null);
    Task LogWarningAsync(string category, string message, string? userEmail = null, string? ipAddress = null);
    Task LogErrorAsync(string category, string message, string? userEmail = null, string? ipAddress = null);
    Task LogSuccessAsync(string category, string message, string? userEmail = null, string? ipAddress = null);
}
