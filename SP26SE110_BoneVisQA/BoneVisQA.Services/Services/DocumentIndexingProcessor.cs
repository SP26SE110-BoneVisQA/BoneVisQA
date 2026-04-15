using System.Text;
using BoneVisQA.Domain.Settings;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using UglyToad.PdfPig;

namespace BoneVisQA.Services.Services;

/// <summary>
/// Consumer-side RAG indexing: stream PDF to disk, page-by-page extraction with progress, sliding-window chunking, HuggingFace embeddings.
/// </summary>
public sealed class DocumentIndexingProcessor : IDocumentIndexingProcessor
{
    private sealed record PageTextSegment(int PageNumber, string Text);
    private sealed record ChunkWithPageRange(string Content, int StartPage, int EndPage);

    private const int ChunkSize = 1000;
    private const int ChunkOverlap = 200;
    private const int DefaultEmbeddingBatchSize = 50;
    private const int SaveProgressEveryPages = 5;
    private const int MaxExtractedCharacters = 50_000_000;

    private const string NoExtractableTextLog = "Uploaded PDF contains no extractable text-base content.";
    private const string ProgressCacheKeyPrefix = "document-ingestion-progress:";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfProcessingService _pdfProcessing;
    private readonly ISupabaseStorageService _storageService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IIndexingExecutionGate _indexingExecutionGate;
    private readonly IDocumentIndexingProgressNotifier _progressNotifier;
    private readonly IMemoryCache _memoryCache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentIndexingProcessor> _logger;
    private readonly int _embeddingBatchSize;

    public DocumentIndexingProcessor(
        IUnitOfWork unitOfWork,
        IPdfProcessingService pdfProcessing,
        ISupabaseStorageService storageService,
        IEmbeddingService embeddingService,
        IIndexingExecutionGate indexingExecutionGate,
        IDocumentIndexingProgressNotifier progressNotifier,
        IOptions<HuggingFaceSettings> huggingFaceOptions,
        IMemoryCache memoryCache,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentIndexingProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _pdfProcessing = pdfProcessing;
        _storageService = storageService;
        _embeddingService = embeddingService;
        _indexingExecutionGate = indexingExecutionGate;
        _progressNotifier = progressNotifier;
        _memoryCache = memoryCache;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _embeddingBatchSize = huggingFaceOptions.Value.BatchSize > 0
            ? huggingFaceOptions.Value.BatchSize
            : DefaultEmbeddingBatchSize;
    }

    public async Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var queueLease = await _indexingExecutionGate.AcquireAsync(cancellationToken);
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId);
        var sourceFilePath = document?.PendingReindexPath ?? document?.FilePath;
        if (document == null || string.IsNullOrEmpty(sourceFilePath))
        {
            _logger.LogWarning("[DocumentIndexing] Document {DocumentId} not found or has no active/pending file path.", documentId);
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
        var completed = false;
        var failoverMarked = false;
        var isAtomicReindex = !string.IsNullOrWhiteSpace(document.PendingReindexPath);
        string? computedContentHash = null;
        try
        {
            await _unitOfWork.Context.Database.ExecuteSqlRawAsync(
                "DELETE FROM pending_document_chunks WHERE doc_id = {0}",
                new object[] { documentId },
                cancellationToken);

            SetProgress(documentId, 5, "Downloading PDF (stream to disk)...", document.TotalPages, document.TotalChunks, document.CurrentPageIndexing);
            tempPdfPath = await _pdfProcessing.DownloadPdfToTempFileAsync(sourceFilePath, cancellationToken);

            var pageSegments = new List<PageTextSegment>();
            var pagesSinceSave = 0;

            var docTracked = await _unitOfWork.Context.Documents
                .FirstAsync(d => d.Id == documentId, cancellationToken);

            using (var pdfDocument = PdfDocument.Open(tempPdfPath))
            {
                var totalPages = pdfDocument.NumberOfPages;
                docTracked.TotalPages = totalPages;
                docTracked.TotalChunks = 0;
                docTracked.CurrentPageIndexing = 0;
                docTracked.IndexingProgress = 0;
                await _unitOfWork.SaveAsync();
                await _progressNotifier.NotifyProgressAsync(
                    documentId,
                    docTracked.TotalPages,
                    docTracked.TotalChunks,
                    docTracked.CurrentPageIndexing,
                    docTracked.IndexingProgress,
                    "PDF parsed. Waiting for chunking...",
                    cancellationToken);

                var pageIndex = 0;
                foreach (var page in pdfDocument.GetPages())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pageIndex++;

                    var pageText = page.Text;
                    if (!string.IsNullOrWhiteSpace(pageText))
                        pageSegments.Add(new PageTextSegment(pageIndex, pageText));

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
                            $"Indexing pages: {pageIndex}/{totalPages}...",
                            docTracked.TotalPages,
                            docTracked.TotalChunks,
                            docTracked.CurrentPageIndexing);
                        await _progressNotifier.NotifyProgressAsync(
                            documentId,
                            docTracked.TotalPages,
                            docTracked.TotalChunks,
                            docTracked.CurrentPageIndexing,
                            docTracked.IndexingProgress,
                            $"Indexing pages: {pageIndex}/{totalPages}...",
                            cancellationToken);
                    }
                }
            }

            var totalExtractedCharacters = pageSegments.Sum(x => x.Text.Length);
            if (totalExtractedCharacters <= 0)
            {
                _logger.LogError(NoExtractableTextLog);
                await MarkFailedAsync(documentId, cancellationToken);
                failoverMarked = true;
                return;
            }

            computedContentHash = await ComputeSha256HashForFileAsync(tempPdfPath, cancellationToken);

            var chunkPayload = SplitTextSlidingWindowWithPageRanges(pageSegments, ChunkSize, ChunkOverlap, MaxExtractedCharacters);
            if (chunkPayload.Count == 0 || chunkPayload.Sum(c => c.Content.Length) == 0)
            {
                _logger.LogError(NoExtractableTextLog);
                await MarkFailedAsync(documentId, cancellationToken);
                failoverMarked = true;
                return;
            }

            _logger.LogInformation(
                "[DocumentIndexing] Extracted {ChunkCount} chunks for document {DocumentId}. Embedding...",
                chunkPayload.Count,
                documentId);
            docTracked.TotalChunks = chunkPayload.Count;
            await _unitOfWork.SaveAsync();
            await _progressNotifier.NotifyProgressAsync(
                documentId,
                docTracked.TotalPages,
                docTracked.TotalChunks,
                docTracked.CurrentPageIndexing,
                docTracked.IndexingProgress,
                "Chunking completed. Starting embedding batches.",
                cancellationToken);
            var estimatedRequests = (int)Math.Ceiling(chunkPayload.Count / (double)_embeddingBatchSize);
            _logger.LogInformation(
                "[Queue] Starting indexing for Document {Id}. Estimated requests: {Count}",
                documentId,
                estimatedRequests);

            var processedChunks = 0;
            for (var batchStart = 0; batchStart < chunkPayload.Count; batchStart += _embeddingBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var take = Math.Min(_embeddingBatchSize, chunkPayload.Count - batchStart);
                var batchChunks = chunkPayload.GetRange(batchStart, take);
                var batchTexts = batchChunks.Select(x => x.Content).ToList();
                _logger.LogInformation(
                    "[BatchEmbed] Sending batch of {Count} chunks using HuggingFace embedding service.",
                    batchTexts.Count);
                var vectors = await _embeddingService.BatchEmbedContentsAsync(batchTexts, cancellationToken);
                if (vectors.Count != batchTexts.Count)
                    throw new EmbeddingFailedException($"Batch embedding result size mismatch. Expected {batchTexts.Count}, got {vectors.Count}.");

                var pendingBatch = new List<PendingDocumentChunk>(take);
                for (var offset = 0; offset < take; offset++)
                {
                    var chunkIndex = batchStart + offset;
                    pendingBatch.Add(new PendingDocumentChunk
                    {
                        Id = Guid.NewGuid(),
                        DocId = documentId,
                        Content = chunkPayload[chunkIndex].Content,
                        ChunkOrder = chunkIndex,
                        StartPage = chunkPayload[chunkIndex].StartPage,
                        EndPage = chunkPayload[chunkIndex].EndPage,
                        Embedding = new Vector(vectors[offset]),
                    });
                }
                await _unitOfWork.Context.PendingDocumentChunks.AddRangeAsync(pendingBatch, cancellationToken);
                await _unitOfWork.SaveAsync();

                processedChunks += take;
                var progress = Math.Clamp((int)Math.Round(processedChunks * 100d / chunkPayload.Count), 0, 100);
                var currentIndexedPage = docTracked.TotalPages > 0
                    ? Math.Clamp((int)Math.Ceiling(processedChunks * docTracked.TotalPages / (double)chunkPayload.Count), 0, docTracked.TotalPages)
                    : docTracked.CurrentPageIndexing;
                docTracked.IndexingProgress = progress;
                docTracked.CurrentPageIndexing = currentIndexedPage;

                await _unitOfWork.SaveAsync();
                SetProgress(
                    documentId,
                    progress,
                    $"Vectorizing chunk {processedChunks} of {chunkPayload.Count}...",
                    docTracked.TotalPages,
                    docTracked.TotalChunks,
                    docTracked.CurrentPageIndexing);
                await _progressNotifier.NotifyProgressAsync(
                    documentId,
                    docTracked.TotalPages,
                    docTracked.TotalChunks,
                    docTracked.CurrentPageIndexing,
                    progress,
                    $"Vectorizing chunk {processedChunks} of {chunkPayload.Count}...",
                    cancellationToken);
            }
            await using var swapTransaction = await _unitOfWork.Context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Atomic swap: old chunks remain active until this exact point.
                var finalDoc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId);
                if (finalDoc == null)
                    throw new InvalidOperationException($"Document {documentId} not found during atomic swap.");

                var previousActiveFilePath = finalDoc.FilePath;
                if (isAtomicReindex && !string.IsNullOrWhiteSpace(previousActiveFilePath))
                {
                    if (!TryExtractSupabaseFilePointer(previousActiveFilePath, out var oldBucket, out var oldObjectPath))
                        throw new InvalidOperationException("Could not parse old active file path for archive move.");

                    var ext = Path.GetExtension(oldObjectPath);
                    if (string.IsNullOrWhiteSpace(ext))
                        ext = ".pdf";
                    var archivePath = $"archive/{documentId}_v{finalDoc.Version}{ext}";
                    var archived = await _storageService.MoveFileAsync(oldBucket, oldObjectPath, archivePath, cancellationToken);
                    if (!archived)
                        throw new InvalidOperationException("Could not archive old file before atomic swap.");
                }

                await _unitOfWork.Context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM document_chunks WHERE doc_id = {0}",
                    new object[] { documentId },
                    cancellationToken);

                await _unitOfWork.Context.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO document_chunks
                        (id, doc_id, content, chunk_order, start_page, end_page, embedding, is_flagged, flagged_by_expert_id, flag_reason, flagged_at)
                    SELECT
                        id, doc_id, content, chunk_order, start_page, end_page, embedding, FALSE, NULL, NULL, NULL
                    FROM pending_document_chunks
                    WHERE doc_id = {0}
                    """,
                    new object[] { documentId },
                    cancellationToken);

                await _unitOfWork.Context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM pending_document_chunks WHERE doc_id = {0}",
                    new object[] { documentId },
                    cancellationToken);

                if (isAtomicReindex && !string.IsNullOrWhiteSpace(finalDoc.PendingReindexPath))
                {
                    finalDoc.FilePath = finalDoc.PendingReindexPath;
                    finalDoc.PendingReindexPath = null;
                }
                else if (!string.IsNullOrWhiteSpace(sourceFilePath))
                {
                    finalDoc.FilePath = sourceFilePath;
                }

                if (isAtomicReindex && !string.IsNullOrWhiteSpace(finalDoc.PendingReindexHash))
                {
                    finalDoc.ContentHash = finalDoc.PendingReindexHash;
                    finalDoc.PendingReindexHash = null;
                    finalDoc.Version += 1;
                }
                else if (!string.IsNullOrWhiteSpace(computedContentHash) &&
                         !string.Equals(finalDoc.ContentHash, computedContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    finalDoc.ContentHash = computedContentHash;
                    finalDoc.Version += 1;
                }

                finalDoc.IndexingStatus = DocumentIndexingStatuses.Completed;
                finalDoc.IndexingProgress = 100;
                finalDoc.IsOutdated = false;
                if (finalDoc.TotalPages > 0)
                    finalDoc.CurrentPageIndexing = finalDoc.TotalPages;
                await _unitOfWork.DocumentRepository.UpdateAsync(finalDoc);
                await _unitOfWork.SaveAsync();
                await swapTransaction.CommitAsync(cancellationToken);

                SetProgress(
                    documentId,
                    100,
                    "Completed.",
                    finalDoc.TotalPages,
                    finalDoc.TotalChunks,
                    finalDoc.CurrentPageIndexing);
                await _progressNotifier.NotifyProgressAsync(
                    documentId,
                    finalDoc.TotalPages,
                    finalDoc.TotalChunks,
                    finalDoc.CurrentPageIndexing,
                    100,
                    "Completed.",
                    cancellationToken);
            }
            catch
            {
                try { await swapTransaction.RollbackAsync(CancellationToken.None); } catch { }
                try
                {
                    await _unitOfWork.Context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM pending_document_chunks WHERE doc_id = {0}",
                        new object[] { documentId },
                        CancellationToken.None);
                }
                catch { }
                throw;
            }

            _logger.LogInformation("[DocumentIndexing] Completed document {DocumentId}.", documentId);
            completed = true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[DocumentIndexing] Processing cancelled for {DocumentId}.", documentId);
            await MarkFailedAsync(documentId, CancellationToken.None);
            failoverMarked = true;
            throw;
        }
        catch (EmbeddingFailedException ex)
        {
            _logger.LogError(ex, "[DocumentIndexing] Embedding pipeline failed for {DocumentId}.", documentId);
            await MarkFailedAsync(documentId, cancellationToken);
            failoverMarked = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocumentIndexing] Fatal error for document {DocumentId}.", documentId);
            await MarkFailedAsync(documentId, cancellationToken);
            failoverMarked = true;
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempPdfPath))
                TryDeleteTempPdf(tempPdfPath);

            if (!completed && !failoverMarked)
                await MarkFailedAsync(documentId, CancellationToken.None);
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

    private static async Task<string> ComputeSha256HashForFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task MarkFailedAsync(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
            var doc = await uow.DocumentRepository.GetByIdAsync(documentId);
            var cacheStatus = DocumentIndexingStatuses.Failed;
            var cacheOperation = "Failed.";
            if (doc != null)
            {
                try
                {
                    await uow.Context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM pending_document_chunks WHERE doc_id = {0}",
                        new object[] { documentId },
                        CancellationToken.None);
                }
                catch { }

                if (!string.IsNullOrWhiteSpace(doc.PendingReindexPath))
                {
                    var pendingPath = doc.PendingReindexPath;
                    doc.PendingReindexPath = null;
                    doc.PendingReindexHash = null;
                    doc.IndexingStatus = DocumentIndexingStatuses.Completed;
                    doc.IndexingProgress = 100;
                    cacheStatus = DocumentIndexingStatuses.Completed;
                    cacheOperation = "Reindexing failed; kept previous version active.";

                    if (TryExtractSupabaseFilePointer(pendingPath, out var pendingBucket, out var pendingObjectPath))
                    {
                        try
                        {
                            await _storageService.DeleteFileAsync(pendingBucket, pendingObjectPath, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[DocumentIndexing] Could not delete pending reindex file for document {DocumentId}.", documentId);
                        }
                    }
                }
                else
                {
                    doc.IndexingStatus = DocumentIndexingStatuses.Failed;
                    doc.IndexingProgress = 100;
                }

                await uow.DocumentRepository.UpdateAsync(doc);
                await uow.SaveAsync();
            }

            cache.Set(
                GetProgressCacheKey(documentId),
                new DocumentIngestionStatusDto
                {
                    Status = cacheStatus,
                    ProgressPercentage = 100,
                    CurrentOperation = cacheOperation,
                    TotalPages = doc?.TotalPages ?? 0,
                    TotalChunks = doc?.TotalChunks ?? 0,
                    CurrentPageIndexing = doc?.CurrentPageIndexing ?? 0
                },
                TimeSpan.FromHours(4));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocumentIndexing] Could not persist Failed status for {DocumentId}.", documentId);
        }
    }

    private static bool TryExtractSupabaseFilePointer(string imageUrl, out string bucket, out string filePath)
    {
        bucket = string.Empty;
        filePath = string.Empty;
        if (string.IsNullOrWhiteSpace(imageUrl))
            return false;

        const string marker = "/storage/v1/object/public/";
        var idx = imageUrl.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return false;

        var rest = imageUrl[(idx + marker.Length)..];
        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash >= rest.Length - 1)
            return false;

        bucket = rest[..slash];
        filePath = rest[(slash + 1)..];
        return !string.IsNullOrEmpty(bucket) && !string.IsNullOrEmpty(filePath);
    }

    private void SetProgress(
        Guid documentId,
        int percentage,
        string operation,
        int totalPages = 0,
        int totalChunks = 0,
        int currentPageIndexing = 0)
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
            CurrentOperation = operation,
            TotalPages = totalPages,
            TotalChunks = totalChunks,
            CurrentPageIndexing = currentPageIndexing
        };
        _memoryCache.Set(GetProgressCacheKey(documentId), value, TimeSpan.FromHours(4));
    }

    private static string GetProgressCacheKey(Guid documentId) => $"{ProgressCacheKeyPrefix}{documentId}";

    private static List<ChunkWithPageRange> SplitTextSlidingWindowWithPageRanges(
        IReadOnlyList<PageTextSegment> pageSegments,
        int maxSize,
        int overlap,
        int maxExtractedCharacters)
    {
        var chunks = new List<ChunkWithPageRange>();
        if (pageSegments.Count == 0)
            return chunks;

        var normalizedSegments = pageSegments
            .Select(s => new PageTextSegment(
                s.PageNumber,
                (s.Text ?? string.Empty)
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Trim()))
            .Where(s => s.Text.Length > 0)
            .ToList();

        if (normalizedSegments.Count == 0)
            return chunks;

        var globalText = new StringBuilder();
        var charToPage = new List<int>(Math.Min(maxExtractedCharacters, 1_000_000));
        foreach (var segment in normalizedSegments)
        {
            if (globalText.Length >= maxExtractedCharacters)
                break;

            if (globalText.Length > 0)
            {
                globalText.Append('\n');
                charToPage.Add(segment.PageNumber);
            }

            foreach (var ch in segment.Text)
            {
                if (globalText.Length >= maxExtractedCharacters)
                    break;

                globalText.Append(ch);
                charToPage.Add(segment.PageNumber);
            }
        }

        var text = globalText.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        var start = 0;
        while (start < text.Length)
        {
            var remainingLength = text.Length - start;
            if (remainingLength <= maxSize)
            {
                var finalRaw = text[start..];
                var finalChunk = finalRaw.Trim();
                if (finalChunk.Length > 0)
                {
                    var finalStartTrim = start + (finalRaw.Length - finalRaw.TrimStart().Length);
                    var finalEndTrim = start + finalRaw.TrimEnd().Length - 1;
                    chunks.Add(new ChunkWithPageRange(
                        finalChunk,
                        charToPage[Math.Clamp(finalStartTrim, 0, charToPage.Count - 1)],
                        charToPage[Math.Clamp(finalEndTrim, 0, charToPage.Count - 1)]));
                }
                break;
            }

            var window = text.Substring(start, maxSize);
            var cutInWindow = FindBestSplitIndex(window, maxSize);
            if (cutInWindow <= 0 || cutInWindow > window.Length)
                cutInWindow = window.Length;

            var rawChunk = text.Substring(start, cutInWindow);
            var trimmedChunk = rawChunk.Trim();
            if (trimmedChunk.Length > 0)
            {
                var trimLeft = rawChunk.Length - rawChunk.TrimStart().Length;
                var trimRight = rawChunk.TrimEnd().Length;
                var chunkStartOffset = start + trimLeft;
                var chunkEndOffset = start + trimRight - 1;
                chunks.Add(new ChunkWithPageRange(
                    trimmedChunk,
                    charToPage[Math.Clamp(chunkStartOffset, 0, charToPage.Count - 1)],
                    charToPage[Math.Clamp(chunkEndOffset, 0, charToPage.Count - 1)]));
            }

            var advance = cutInWindow - overlap;
            if (advance <= 0)
                advance = cutInWindow;

            start += advance;
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
