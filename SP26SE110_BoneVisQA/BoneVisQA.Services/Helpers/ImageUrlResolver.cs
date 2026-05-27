using Microsoft.Extensions.Configuration;

namespace BoneVisQA.Services.Helpers;

/// <summary>
/// Resolves image URLs to absolute URLs.
/// - If already absolute (http/https), return as-is.
/// - If relative with legacy path (/uploads/...), redirect to Supabase storage or return placeholder.
/// - If relative with Supabase path, return as-is (Supabase returns full URL).
/// </summary>
public static class ImageUrlResolver
{
    private static string? _supabaseUrl;

    public static void Configure(IConfiguration configuration)
    {
        _supabaseUrl = configuration["Supabase:Url"];
    }

    /// <summary>
    /// Resolve image URL to absolute URL or Supabase public URL.
    /// </summary>
    public static string Resolve(string? imageUrl, string bucket = "medical-images")
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return string.Empty;

        var url = imageUrl.Trim();

        // Already absolute - return as-is
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        // Legacy relative path /uploads/... - convert to Supabase storage URL
        if (url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            // Extract the file path after /uploads/images/ or /uploads/dicom/
            var path = url.Substring("/uploads/".Length);
            if (string.IsNullOrEmpty(_supabaseUrl))
                return url; // Return original if Supabase not configured

            // Convert to Supabase public URL
            return $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{bucket}/{path}";
        }

        // Other relative paths - return as Supabase path
        if (string.IsNullOrEmpty(_supabaseUrl))
            return url;

        return $"{_supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{bucket}/{url.TrimStart('/')}";
    }

    /// <summary>
    /// Resolve multiple image URLs.
    /// </summary>
    public static IEnumerable<string> ResolveAll(IEnumerable<string?> imageUrls, string bucket = "medical-images")
    {
        return imageUrls.Select(url => Resolve(url, bucket));
    }
}
