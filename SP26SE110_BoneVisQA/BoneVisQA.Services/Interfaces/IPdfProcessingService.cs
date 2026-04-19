namespace BoneVisQA.Services.Interfaces;

public interface IPdfProcessingService
{
    /// <summary>
    /// Downloads a PDF from URL and returns the full extracted text.
    /// </summary>
    Task<string> DownloadAndExtractPdfTextAsync(string fileUrl, CancellationToken cancellationToken = default);

    /// <summary>Streams the PDF to a temp file (caller must delete). Returns absolute path.</summary>
    Task<string> DownloadPdfToTempFileAsync(string fileUrl, CancellationToken cancellationToken = default);
}
