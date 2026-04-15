using System.Text;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pgvector;
using UglyToad.PdfPig;

namespace BoneVisQA.Services.Services;

/// <summary>
/// Consumer-side RAG indexing: stream PDF to disk, page-by-page extraction with progress, sliding-window chunking, Gemini embeddings.
/// </summary>
public sealed class DocumentIndexingProcessor : IDocumentIndexingProcessor
{
    private const int ChunkSize = 1000;
    private const int ChunkOverlap = 200;
    private const int SaveProgressEveryPages = 5;
    private const int MaxExtractedCharacters = 50_000_000;

    private const string NoExtractableTextLog = "Uploaded PDF contains no extractable text-base content.";
    private const string ProgressCacheKeyPrefix = "document-ingestion-progress:";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfProcessingService _pdfProcessing;
    private readonly IEmbeddingService _embeddingService;
    private readonly IMemoryCache _memoryCache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentIndexingProcessor> _logger;

    public DocumentIndexingProcessor(
        IUnitOfWork unitOfWork,
        IPdfProcessingService pdfProcessing,
        IEmbeddingService embeddingService,
        IMemoryCache memoryCache,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentIndexingProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _pdfProcessing = pdfProcessing;
        _embeddingService = embeddingService;
        _memoryCache = memoryCache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId);
        if (document == null || string.IsNullOrEmpty(document.FilePath))
        {
            _logger.LogWarning("[DocumentIndexing] Document {DocumentId} not found or has no file path.", documentId);
            return;
        }

        if (!string.Equals(document.IndexingStatus, DocumentIndexingStatuses.Processing, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "[DocumentIndexing] Document {DocumentId} expected status Processing but was {Status}.",
                documentId,
                document.IndexingStatus);
            return;
        }

        string? tempPdfPath = null;
        try
        {
            var existingChunks = await _unitOfWork.DocumentChunkRepository.FindAsync(c => c.DocId == documentId);
            if (existingChunks.Count > 0)
            {
                await _unitOfWork.DocumentChunkRepository.RemoveRangeAsync(existingChunks);
                await _unitOfWork.SaveAsync();
            }

            SetProgress(documentId, 5, "Downloading PDF (stream to disk)...");
            tempPdfPath = await _pdfProcessing.DownloadPdfToTempFileAsync(document.FilePath, cancellationToken);

            var sb = new StringBuilder();
            var pagesSinceSave = 0;

            var docTracked = await _unitOfWork.Context.Documents
                .FirstAsync(d => d.Id == documentId, cancellationToken);

            using (var pdfDocument = PdfDocument.Open(tempPdfPath))
            {
                var totalPages = pdfDocument.NumberOfPages;
                docTracked.TotalPages = totalPages;
                docTracked.CurrentPageIndexing = 0;
                docTracked.IndexingProgress = 0;
                await _unitOfWork.SaveAsync();

                var pageIndex = 0;
                foreach (var page in pdfDocument.GetPages())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pageIndex++;

                    if (sb.Length < MaxExtractedCharacters)
                    {
                        if (sb.Length > 0)
                            sb.AppendLine();
                        sb.Append(page.Text);
                    }

                    docTracked.CurrentPageIndexing = pageIndex;
                    docTracked.TotalPages = totalPages;
                    docTracked.IndexingProgress = totalPages > 0
                        ? Math.Clamp((int)(pageIndex * 50.0 / totalPages), 0, 50)
                        : 0;

                    pagesSinceSave++;
                    if (pagesSinceSave >= SaveProgressEveryPages || pageIndex == totalPages)
                    {
                        await _unitOfWork.SaveAsync();
                        pagesSinceSave = 0;
                        var displayPct = totalPages > 0
                            ? (int)(pageIndex * 100.0 / totalPages)
                            : 0;
                        SetProgress(
                            documentId,
                            Math.Clamp(displayPct, 0, 99),
                            $"Indexing pages: {pageIndex}/{totalPages}...");
                    }
                }
            }

            var fullText = sb.ToString();
            if (string.IsNullOrWhiteSpace(fullText))
            {
                _logger.LogError(NoExtractableTextLog);
                await MarkFailedAsync(documentId, cancellationToken);
                return;
            }

            var chunkTexts = SplitTextSlidingWindow(fullText, ChunkSize, ChunkOverlap);
            if (chunkTexts.Count == 0 || chunkTexts.Sum(c => c.Length) == 0)
            {
                _logger.LogError(NoExtractableTextLog);
                await MarkFailedAsync(documentId, cancellationToken);
                return;
            }

            _logger.LogInformation(
                "[DocumentIndexing] Extracted {ChunkCount} chunks for document {DocumentId}. Embedding...",
                chunkTexts.Count,
                documentId);

            var entities = new List<DocumentChunk>();
            for (var i = 0; i < chunkTexts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var progress = 50 + (int)Math.Round((i + 1d) / chunkTexts.Count * 50d);
                SetProgress(documentId, progress, $"Vectorizing chunk {i + 1} of {chunkTexts.Count}...");

                float[] vec;
                try
                {
                    vec = await _embeddingService.EmbedTextAsync(chunkTexts[i], cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DocumentIndexing] Embedding failed for document {DocumentId} chunk {Index}.", documentId, i);
                    await MarkFailedAsync(documentId, cancellationToken);
                    return;
                }

                entities.Add(new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocId = documentId,
                    Content = chunkTexts[i],
                    ChunkOrder = i,
                    Embedding = new Vector(vec),
                    IsFlagged = false
                });

                await UpdateProgressDbAsync(documentId, progress, cancellationToken);
            }

            if (entities.Count > 0)
                await _unitOfWork.DocumentChunkRepository.AddRangeAsync(entities);

            await _unitOfWork.SaveAsync();

            var finalDoc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId);
            if (finalDoc != null)
            {
                finalDoc.IndexingStatus = DocumentIndexingStatuses.Completed;
                finalDoc.IndexingProgress = 100;
                finalDoc.IsOutdated = false;
                if (finalDoc.TotalPages > 0)
                    finalDoc.CurrentPageIndexing = finalDoc.TotalPages;
                await _unitOfWork.DocumentRepository.UpdateAsync(finalDoc);
                await _unitOfWork.SaveAsync();
            }

            SetProgress(documentId, 100, "Completed.");
            _logger.LogInformation("[DocumentIndexing] Completed document {DocumentId}.", documentId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EmbeddingFailedException ex)
        {
            _logger.LogError(ex, "[DocumentIndexing] Embedding pipeline failed for {DocumentId}.", documentId);
            await MarkFailedAsync(documentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocumentIndexing] Fatal error for document {DocumentId}.", documentId);
            await MarkFailedAsync(documentId, cancellationToken);
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempPdfPath))
                TryDeleteTempPdf(tempPdfPath);
        }
    }

    private static void TryDeleteTempPdf(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort
        }
    }

    private async Task MarkFailedAsync(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
            var doc = await uow.DocumentRepository.GetByIdAsync(documentId);
            if (doc != null)
            {
                doc.IndexingStatus = DocumentIndexingStatuses.Failed;
                doc.IndexingProgress = 100;
                await uow.DocumentRepository.UpdateAsync(doc);
                await uow.SaveAsync();
            }

            cache.Set(
                GetProgressCacheKey(documentId),
                new DocumentIngestionStatusDto
                {
                    Status = DocumentIndexingStatuses.Failed,
                    ProgressPercentage = 100,
                    CurrentOperation = "Failed."
                },
                TimeSpan.FromHours(4));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocumentIndexing] Could not persist Failed status for {DocumentId}.", documentId);
        }
    }

    private async Task UpdateProgressDbAsync(Guid documentId, int percentage, CancellationToken cancellationToken)
    {
        var doc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId);
        if (doc == null)
            return;
        doc.IndexingProgress = Math.Clamp(percentage, 0, 100);
        await _unitOfWork.DocumentRepository.UpdateAsync(doc);
        await _unitOfWork.SaveAsync();
    }

    private void SetProgress(Guid documentId, int percentage, string operation)
    {
        var statusLabel = DocumentIndexingStatuses.Processing;
        if (percentage >= 100 && string.Equals(operation, "Completed.", StringComparison.OrdinalIgnoreCase))
            statusLabel = DocumentIndexingStatuses.Completed;
        else if (percentage >= 100 && string.Equals(operation, "Failed.", StringComparison.OrdinalIgnoreCase))
            statusLabel = DocumentIndexingStatuses.Failed;

        var value = new DocumentIngestionStatusDto
        {
            Status = statusLabel,
            ProgressPercentage = Math.Clamp(percentage, 0, 100),
            CurrentOperation = operation
        };
        _memoryCache.Set(GetProgressCacheKey(documentId), value, TimeSpan.FromHours(4));
    }

    private static string GetProgressCacheKey(Guid documentId) => $"{ProgressCacheKeyPrefix}{documentId}";

    private static List<string> SplitTextSlidingWindow(string text, int maxSize, int overlap)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        text = text.Trim()
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        var remaining = text;
        while (remaining.Length > 0)
        {
            if (remaining.Length <= maxSize)
            {
                var last = remaining.Trim();
                if (last.Length > 0)
                    chunks.Add(last);
                break;
            }

            var cutIndex = FindBestSplitIndex(remaining, maxSize);
            if (cutIndex <= 0 || cutIndex > remaining.Length)
                cutIndex = maxSize;

            var chunk = remaining[..cutIndex].Trim();
            if (chunk.Length > 0)
                chunks.Add(chunk);

            if (cutIndex >= remaining.Length)
                break;

            if (overlap <= 0)
            {
                remaining = remaining[cutIndex..].TrimStart();
                continue;
            }

            var overlapLen = Math.Min(overlap, chunk.Length);
            var overlapTail = overlapLen > 0 ? chunk[^overlapLen..] : string.Empty;
            var rest = remaining[cutIndex..];
            remaining = (overlapTail + rest).TrimStart();
        }

        return chunks;
    }

    private static int FindBestSplitIndex(string text, int maxSize)
    {
        var windowLen = Math.Min(maxSize, text.Length);
        var window = text[..windowLen];

        var paraIdx = window.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (paraIdx >= 0)
            return paraIdx + 2;

        var newLineIdx = window.LastIndexOf('\n');
        if (newLineIdx >= 0)
            return newLineIdx + 1;

        var bestSentenceIdx = -1;
        var sentenceDelims = new[] { ". ", "? ", "! " };
        foreach (var delim in sentenceDelims)
        {
            var idx = window.LastIndexOf(delim, StringComparison.Ordinal);
            if (idx > bestSentenceIdx)
                bestSentenceIdx = idx;
        }
        if (bestSentenceIdx >= 0)
            return bestSentenceIdx + 2;

        var spaceIdx = window.LastIndexOf(' ');
        if (spaceIdx > 0)
            return spaceIdx;

        return windowLen;
    }
}
