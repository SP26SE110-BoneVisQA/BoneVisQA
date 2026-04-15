using Microsoft.AspNetCore.Http;

namespace BoneVisQA.Services.Interfaces;

public interface ISupabaseStorageService
{
    Task<string> UploadFileAsync(
        IFormFile file,
        string bucket,
        string? folder = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads to an exact object path under the bucket (upsert), e.g. <c>documents/{documentId}.pdf</c> for overwrites.
    /// </summary>
    Task<string> UploadFileToPathAsync(
        IFormFile file,
        string bucket,
        string objectPath,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(string bucket, string filePath, CancellationToken cancellationToken = default);
}
