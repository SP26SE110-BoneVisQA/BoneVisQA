using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BoneVisQA.Services.Services.Storage;

public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;

    public SupabaseStorageService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _supabaseUrl = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url not configured");
        _supabaseKey = configuration["Supabase:ServiceKey"]
            ?? throw new InvalidOperationException("Supabase:ServiceKey not configured");
    }

    public async Task<string> UploadFileAsync(
        IFormFile file,
        string bucket,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = string.IsNullOrEmpty(folder) ? fileName : $"{folder}/{fileName}";

        var uploadUrl = $"{_supabaseUrl}/storage/v1/object/{bucket}/{filePath}";

        var capacity = file.Length is > 0 and <= int.MaxValue ? (int)file.Length : 0;
        var buffer = new MemoryStream(capacity);
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        using var content = new StreamContent(buffer);
        var mediaType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
        request.Headers.Add("x-upsert", "true");
        request.Content = content;

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var message =
                $"Supabase upload failed ({(int)response.StatusCode} {response.ReasonPhrase}): {errorBody}";
            throw new HttpRequestException(message, inner: null, statusCode: response.StatusCode);
        }

        var publicUrl = $"{_supabaseUrl}/storage/v1/object/public/{bucket}/{filePath}";
        return publicUrl;
    }

    public async Task<string> UploadFileToPathAsync(
        IFormFile file,
        string bucket,
        string objectPath,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = objectPath.Trim().Replace('\\', '/').TrimStart('/');
        var uploadUrl = $"{_supabaseUrl}/storage/v1/object/{bucket}/{normalizedPath}";

        var capacity = file.Length is > 0 and <= int.MaxValue ? (int)file.Length : 0;
        var buffer = new MemoryStream(capacity);
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        using var content = new StreamContent(buffer);
        var mediaType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
        request.Headers.Add("x-upsert", "true");
        request.Content = content;

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var message =
                $"Supabase upload failed ({(int)response.StatusCode} {response.ReasonPhrase}): {errorBody}";
            throw new HttpRequestException(message, inner: null, statusCode: response.StatusCode);
        }

        return $"{_supabaseUrl}/storage/v1/object/public/{bucket}/{normalizedPath}";
    }

    public async Task<bool> DeleteFileAsync(string bucket, string filePath, CancellationToken cancellationToken = default)
    {
        var deleteUrl = $"{_supabaseUrl}/storage/v1/object/{bucket}/{filePath}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<string>> ListObjectPathsAsync(
        string bucket,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        var normalizedPrefix = prefix.Trim().Replace('\\', '/').TrimStart('/');
        var listUrl = $"{_supabaseUrl}/storage/v1/object/list/{bucket.Trim()}";

        using var request = new HttpRequestMessage(HttpMethod.Post, listUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
        request.Content = JsonContent.Create(new Dictionary<string, object?>
        {
            ["prefix"] = normalizedPrefix,
            ["limit"] = 1000,
            ["offset"] = 0
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return Array.Empty<string>();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
                continue;
            if (!el.TryGetProperty("name", out var nameEl))
                continue;
            var name = nameEl.GetString();
            if (string.IsNullOrEmpty(name))
                continue;
            list.Add(name.Replace('\\', '/'));
        }

        return list;
    }

    public async Task<string?> CreateSignedUrlAsync(
        string path,
        int duration = 3600,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!TryExtractBucketAndPath(path, out var bucket, out var objectPath))
            return null;

        var signUrl = $"{_supabaseUrl}/storage/v1/object/sign/{bucket}/{objectPath}";
        using var request = new HttpRequestMessage(HttpMethod.Post, signUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
        request.Content = JsonContent.Create(new Dictionary<string, object?>
        {
            ["expiresIn"] = Math.Max(60, duration)
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!doc.RootElement.TryGetProperty("signedURL", out var signedEl))
            return null;

        var signed = signedEl.GetString();
        if (string.IsNullOrWhiteSpace(signed))
            return null;

        if (signed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            signed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return signed;

        var normalized = signed.TrimStart('/');
        return $"{_supabaseUrl.TrimEnd('/')}/{normalized}";
    }

    private static bool TryExtractBucketAndPath(string raw, out string bucket, out string objectPath)
    {
        bucket = "student_uploads";
        objectPath = string.Empty;

        var value = raw.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return false;

            const string publicMarker = "/storage/v1/object/public/";
            const string objectMarker = "/storage/v1/object/";
            var path = uri.AbsolutePath;
            var idx = path.IndexOf(publicMarker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                path = path[(idx + publicMarker.Length)..];
            else
            {
                idx = path.IndexOf(objectMarker, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    return false;
                path = path[(idx + objectMarker.Length)..];
            }

            var slash = path.IndexOf('/');
            if (slash <= 0 || slash >= path.Length - 1)
                return false;
            bucket = path[..slash];
            objectPath = path[(slash + 1)..];
            return true;
        }

        var normalized = value.Replace('\\', '/').TrimStart('/');
        var firstSlash = normalized.IndexOf('/');
        if (firstSlash > 0)
        {
            var firstPart = normalized[..firstSlash];
            if (!firstPart.Contains('.'))
            {
                bucket = firstPart;
                objectPath = normalized[(firstSlash + 1)..];
                return !string.IsNullOrWhiteSpace(objectPath);
            }
        }

        objectPath = normalized;
        return !string.IsNullOrWhiteSpace(objectPath);
    }
}
