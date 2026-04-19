using System;

namespace BoneVisQA.Services.Models.Notification;

/// <summary>Shared SPA path normalization for REST + SignalR notification payloads.</summary>
public static class NotificationAppRoute
{
    public static string? Normalize(string? targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
            return null;

        var t = targetUrl.Trim();
        if (Uri.TryCreate(t, UriKind.Absolute, out var abs))
        {
            var path = abs.PathAndQuery;
            if (string.IsNullOrEmpty(abs.Fragment))
                return string.IsNullOrEmpty(path) ? "/" : path;
            return path + abs.Fragment;
        }

        return t.StartsWith('/') ? t : "/" + t;
    }
}

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    /// <summary>App-relative path for SPA routers (<c>pathname + search</c>). Mirrors <see cref="TargetUrl"/> when already relative; strips origin from absolute URLs.</summary>
    public string? Route { get; set; }
    public bool IsRead { get; set; }
    public DateTime? CreatedAt { get; set; }
}
