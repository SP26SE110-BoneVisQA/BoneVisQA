using BoneVisQA.Services.Interfaces;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace BoneVisQA.Services.Services;

public class PdfProcessingService : IPdfProcessingService
{
    public const string HttpClientName = "PdfProcessing";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PdfProcessingService> _logger;

    public PdfProcessingService(IHttpClientFactory httpClientFactory, ILogger<PdfProcessingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> DownloadAndExtractPdfTextAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var tempPdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            await using (var fileStream = new FileStream(
                             tempPdfPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await using var network = await client.GetStreamAsync(fileUrl, cancellationToken);
                await network.CopyToAsync(fileStream, cancellationToken);
            }

            _logger.LogInformation("PDF downloaded to temp file. Starting PdfPig text extraction...");

            await using var readStream = new FileStream(
                tempPdfPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 65536,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            return ExtractText(readStream);
        }
        finally
        {
            TryDeleteTemp(tempPdfPath);
        }
    }

    public async Task<string> DownloadPdfToTempFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var tempPdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        await using (var fileStream = new FileStream(
                           tempPdfPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           bufferSize: 81920,
                           FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await using var network = await client.GetStreamAsync(fileUrl, cancellationToken);
            await network.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogInformation("PDF downloaded to temp file {Path}.", tempPdfPath);
        return tempPdfPath;
    }

    private void TryDeleteTemp(string tempPdfPath)
    {
        try
        {
            if (File.Exists(tempPdfPath))
                File.Delete(tempPdfPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete temp PDF at {Path}", tempPdfPath);
        }
    }

    private static string ExtractText(Stream pdfStream)
    {
        var sb = new System.Text.StringBuilder();
        using var document = PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }
}
