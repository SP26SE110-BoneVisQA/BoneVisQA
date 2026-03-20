namespace BoneVisQA.Services.Interfaces;

public interface IPdfProcessingService
{
    /// <summary>
    /// Downloads a PDF from URL and returns text chunks (1000 chars, 200 overlap).
    /// </summary>
    Task<IReadOnlyList<string>> DownloadAndChunkPdfAsync(string fileUrl, CancellationToken cancellationToken = default);
}
