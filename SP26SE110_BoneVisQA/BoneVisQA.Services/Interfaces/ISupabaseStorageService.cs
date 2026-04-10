using Microsoft.AspNetCore.Http;

namespace BoneVisQA.Services.Interfaces;

public interface ISupabaseStorageService
{
    Task<string> UploadFileAsync(
        IFormFile file,
        string bucket,
        string? folder = null,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string bucket, string filePath);
}
