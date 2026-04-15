using System.Net.Http.Headers;
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
}
