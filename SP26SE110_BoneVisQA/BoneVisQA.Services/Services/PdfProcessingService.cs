using BoneVisQA.Services.Interfaces;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace BoneVisQA.Services.Services;

public class PdfProcessingService : IPdfProcessingService
{
    public const string HttpClientName = "PdfProcessing";

    private const int ChunkSize = 1000;
    private const int ChunkOverlap = 200;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PdfProcessingService> _logger;

    public PdfProcessingService(IHttpClientFactory httpClientFactory, ILogger<PdfProcessingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> DownloadAndChunkPdfAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        await using var stream = await client.GetStreamAsync(fileUrl, cancellationToken);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        _logger.LogInformation("PDF downloaded successfully. Starting PdfPig text extraction...");

        var fullText = ExtractText(ms);
        return ChunkText(fullText);
    }

    private static string ExtractText(Stream pdfStream)
    {
        var sb = new System.Text.StringBuilder();
        using var document = PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private static List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        text = text.Trim();
        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + ChunkSize, text.Length);
            var chunk = text[start..end].Trim();
            if (chunk.Length > 0)
                chunks.Add(chunk);
            var next = end - ChunkOverlap;
            if (next <= start)
                next = start + 1;
            start = next;
            if (start >= text.Length)
                break;
        }

        return chunks;
    }
}
