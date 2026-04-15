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

    Task<bool> MoveFileAsync(
        string bucket,
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    /// <summary>Lists object paths under <paramref name="prefix"/> (Storage REST list API).</summary>
    Task<IReadOnlyList<string>> ListObjectPathsAsync(
        string bucket,
        string prefix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a temporary signed URL for a private object path.
    /// Path may be either <c>bucket/path/to/file</c> or <c>path/to/file</c> (defaults to student_uploads bucket).
    /// </summary>
    Task<string?> CreateSignedUrlAsync(
        string path,
        int duration = 3600,
        CancellationToken cancellationToken = default);
}
