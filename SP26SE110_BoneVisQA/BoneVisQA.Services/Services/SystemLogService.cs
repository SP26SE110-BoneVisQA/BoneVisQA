using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Interfaces;

namespace BoneVisQA.Services.Services;

public class SystemLogService : ISystemLogService
{
    private readonly BoneVisQADbContext _context;

    public SystemLogService(BoneVisQADbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string level, string category, string message, string? userEmail = null, string? ipAddress = null)
    {
        var log = new SystemLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Level = level,
            Category = category,
            Message = message,
            UserEmail = userEmail,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        _context.SystemLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task LogInfoAsync(string category, string message, string? userEmail = null, string? ipAddress = null)
        => await LogAsync("Info", category, message, userEmail, ipAddress);

    public async Task LogWarningAsync(string category, string message, string? userEmail = null, string? ipAddress = null)
        => await LogAsync("Warning", category, message, userEmail, ipAddress);

    public async Task LogErrorAsync(string category, string message, string? userEmail = null, string? ipAddress = null)
        => await LogAsync("Error", category, message, userEmail, ipAddress);

    public async Task LogSuccessAsync(string category, string message, string? userEmail = null, string? ipAddress = null)
        => await LogAsync("Success", category, message, userEmail, ipAddress);
}
