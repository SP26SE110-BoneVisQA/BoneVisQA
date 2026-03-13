using System.Net.Http.Headers;
using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BoneVisQA.Services.Services;

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

    public async Task<string> UploadFileAsync(IFormFile file, string bucket, string? folder = null)
    {
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = string.IsNullOrEmpty(folder) ? fileName : $"{folder}/{fileName}";

        var uploadUrl = $"{_supabaseUrl}/storage/v1/object/{bucket}/{filePath}";

        using var stream = file.OpenReadStream();
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
        request.Headers.Add("x-upsert", "true");
        request.Content = content;

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Supabase upload failed: {error}");
        }

        var publicUrl = $"{_supabaseUrl}/storage/v1/object/public/{bucket}/{filePath}";
        return publicUrl;
    }

    public async Task<bool> DeleteFileAsync(string bucket, string filePath)
    {
        var deleteUrl = $"{_supabaseUrl}/storage/v1/object/{bucket}/{filePath}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }
}
