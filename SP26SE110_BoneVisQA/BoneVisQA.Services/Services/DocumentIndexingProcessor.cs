using System.Text;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace BoneVisQA.Services.Services;

/// <summary>
/// Consumer-side indexing: PDF extraction + chunking. Vector generation is delegated to BoneVisQA.AI (chunks stored with null embeddings until backfilled).
/// </summary>
public sealed class DocumentIndexingProcessor : IDocumentIndexingProcessor
{
    private sealed record PageTextSegment(int PageNumber, string Text);
    private sealed record ChunkWithPageRange(string Content, int StartPage, int EndPage);

    private const int PendingChunkInsertBatchSize = 50;
    private const int SaveProgressEveryPages = 5;
    private const int MaxExtractedCharacters = 50_000_000;
    private const int DefaultChunkSize = 2000;
    private const int DefaultChunkOverlap = 250;

    private const string NoExtractableTextLog = "Uploaded PDF contains no extractable text-base content.";
    private const string ProgressCacheKeyPrefix = "document-ingestion-progress:";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfProcessingService _pdfProcessing;
    private readonly ISupabaseStorageService _storageService;
    private readonly IIndexingExecutionGate _indexingExecutionGate;
    private readonly IDocumentIndexingProgressNotifier _progressNotifier;
    private readonly IMemoryCache _memoryCache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPythonAiConnectorService _pythonAi;
    private readonly ILogger<DocumentIndexingProcessor> _logger;
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;

    public DocumentIndexingProcessor(
        IUnitOfWork unitOfWork,
        IPdfProcessingService pdfProcessing,
        ISupabaseStorageService storageService,
        IIndexingExecutionGate indexingExecutionGate,
        IDocumentIndexingProgressNotifier progressNotifier,
        IMemoryCache memoryCache,
        IServiceScopeFactory scopeFactory,
        IPythonAiConnectorService pythonAi,
        IConfiguration configuration,
        ILogger<DocumentIndexingProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _pdfProcessing = pdfProcessing;
        _storageService = storageService;
        _indexingExecutionGate = indexingExecutionGate;
        _progressNotifier = progressNotifier;
        _memoryCache = memoryCache;
        _scopeFactory = scopeFactory;
        _pythonAi = pythonAi;
        _logger = logger;
        _chunkSize = Math.Clamp(configuration.GetValue("DocumentIndexing:ChunkSize", DefaultChunkSize), 800, 4000);
        _chunkOverlap = Math.Clamp(configuration.GetValue("DocumentIndexing:ChunkOverlap", DefaultChunkOverlap), 50, 800);
        if (_chunkOverlap >= _chunkSize)
            _chunkOverlap = Math.Max(50, _chunkSize / 5);
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

            // Remove existing pgvector rows for this document immediately so failed runs never leave stale embeddings in RAG.
            // Citations referencing old chunk ids cascade-delete (see citations_chunk_id_fkey). Final swap still replaces rows atomically on success.
            await _unitOfWork.Context.Database.ExecuteSqlRawAsync(
                "DELETE FROM document_chunks WHERE doc_id = {0}",
                new object[] { documentId },
                cancellationToken);

            _logger.LogInformation("[DocumentIndexing] Cleared document_chunks for doc {DocumentId} before rebuild.", documentId);

            SetProgress(
                documentId,
                DocumentIndexingPhases.OverallProgress(DocumentIndexingPhases.DownloadPdf, 0),
                "Downloading PDF (stream to disk)...",
                document.TotalPages,
                document.TotalChunks,
                document.CurrentPageIndexing,
                DocumentIndexingPhases.DownloadPdf);
            tempPdfPath = await _pdfProcessing.DownloadPdfToTempFileAsync(sourceFilePath, cancellationToken);

            await ReportProgressAsync(
                documentId,
                DocumentIndexingPhases.DownloadPdf,
                1,
                "PDF downloaded.",
                document.TotalPages,
                document.TotalChunks,
                document.CurrentPageIndexing,
                cancellationToken);

            var pageSegments = new List<PageTextSegment>();
            var pagesSinceSave = 0;

            var docTracked = await _unitOfWork.Context.Documents
                .FirstAsync(d => d.Id == documentId, cancellationToken);
            await EnsureDocumentDefaultsAsync(docTracked, cancellationToken);

            using (var pdfDocument = PdfDocument.Open(tempPdfPath))
            {
                var totalPages = pdfDocument.NumberOfPages;
                docTracked.TotalPages = totalPages;
                docTracked.TotalChunks = 0;
                docTracked.CurrentPageIndexing = 0;
                docTracked.IndexingProgress = 0;
                await _unitOfWork.SaveAsync();
                await ReportProgressAsync(
                    documentId,
                    DocumentIndexingPhases.ExtractPages,
                    0,
                    "PDF parsed. Extracting text...",
                    docTracked.TotalPages,
                    docTracked.TotalChunks,
                    docTracked.CurrentPageIndexing,
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
                    docTracked.IndexingProgress = DocumentIndexingPhases.OverallProgress(
                        DocumentIndexingPhases.ExtractPages,
                        totalPages > 0 ? pageIndex / (double)totalPages : 0);

                    pagesSinceSave++;
                    if (pagesSinceSave >= SaveProgressEveryPages || pageIndex == totalPages)
                    {
                        await _unitOfWork.SaveAsync();
                        pagesSinceSave = 0;
                        await ReportProgressAsync(
                            documentId,
                            DocumentIndexingPhases.ExtractPages,
                            totalPages > 0 ? pageIndex / (double)totalPages : 1,
                            $"Extracting text: page {pageIndex}/{totalPages}...",
                            docTracked.TotalPages,
                            docTracked.TotalChunks,
                            docTracked.CurrentPageIndexing,
                            cancellationToken);
                    }
                }
            }

            var totalExtractedCharacters = pageSegments.Sum(x => x.Text.Length);
            if (totalExtractedCharacters <= 0)
            {
                _logger.LogError(NoExtractableTextLog);
                await MarkFailedAsync(documentId, NoExtractableTextLog, cancellationToken);
                failoverMarked = true;
                return;
            }

            computedContentHash = await ComputeSha256HashForFileAsync(tempPdfPath, cancellationToken);

            var chunkPayload = SplitTextSlidingWindowWithPageRanges(pageSegments, _chunkSize, _chunkOverlap, MaxExtractedCharacters);
            if (chunkPayload.Count == 0 || chunkPayload.Sum(c => c.Content.Length) == 0)
            {
                _logger.LogError(NoExtractableTextLog);
                await MarkFailedAsync(documentId, NoExtractableTextLog, cancellationToken);
                failoverMarked = true;
                return;
            }

            _logger.LogInformation(
                "[DocumentIndexing] Extracted {ChunkCount} chunks for document {DocumentId}. Persisting chunks (vectors deferred / Python)...",
                chunkPayload.Count,
                documentId);
            docTracked.TotalChunks = chunkPayload.Count;
            await _unitOfWork.SaveAsync();
            await ReportProgressAsync(
                documentId,
                DocumentIndexingPhases.PersistChunks,
                0,
                "Chunking completed. Persisting chunks...",
                docTracked.TotalPages,
                docTracked.TotalChunks,
                docTracked.CurrentPageIndexing,
                cancellationToken);

            var processedChunks = 0;
            for (var batchStart = 0; batchStart < chunkPayload.Count; batchStart += PendingChunkInsertBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var take = Math.Min(PendingChunkInsertBatchSize, chunkPayload.Count - batchStart);
                _logger.LogInformation(
                    "[BatchPersist] Saving {Count} pending chunks without C# embeddings.",
                    take);

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
                        Embedding = null,
                    });
                }
                await _unitOfWork.Context.PendingDocumentChunks.AddRangeAsync(pendingBatch, cancellationToken);
                await _unitOfWork.SaveAsync();

                processedChunks += take;
                var phaseFraction = chunkPayload.Count > 0 ? processedChunks / (double)chunkPayload.Count : 1;
                var progress = DocumentIndexingPhases.OverallProgress(DocumentIndexingPhases.PersistChunks, phaseFraction);
                var currentIndexedPage = docTracked.TotalPages > 0
                    ? Math.Clamp((int)Math.Ceiling(processedChunks * docTracked.TotalPages / (double)chunkPayload.Count), 0, docTracked.TotalPages)
                    : docTracked.CurrentPageIndexing;
                docTracked.IndexingProgress = progress;
                docTracked.CurrentPageIndexing = currentIndexedPage;

                await _unitOfWork.SaveAsync();
                await ReportProgressAsync(
                    documentId,
                    DocumentIndexingPhases.PersistChunks,
                    phaseFraction,
                    $"Saving chunks: {processedChunks}/{chunkPayload.Count}...",
                    docTracked.TotalPages,
                    docTracked.TotalChunks,
                    docTracked.CurrentPageIndexing,
                    cancellationToken,
                    processedChunks);
            }
            await using var swapTransaction = await _unitOfWork.Context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Atomic swap: old chunks remain active until this exact point.
                var finalDoc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId);
                if (finalDoc == null)
                    throw new InvalidOperationException($"Document {documentId} not found during atomic swap.");

                var previousActiveFilePath = finalDoc.FilePath;
                var versionBeforeSwap = SemanticDocumentVersion.Normalize(finalDoc.Version);
                if (isAtomicReindex && !string.IsNullOrWhiteSpace(previousActiveFilePath))
                {
                    if (!TryExtractSupabaseFilePointer(previousActiveFilePath, out var oldBucket, out var oldObjectPath))
                        throw new InvalidOperationException("Could not parse old active file path for archive move.");

                    var ext = Path.GetExtension(oldObjectPath);
                    if (string.IsNullOrWhiteSpace(ext))
                        ext = ".pdf";
                    var archivePath = $"archive/{documentId}_v{SemanticDocumentVersion.SanitizeForStoragePath(versionBeforeSwap)}{ext}";
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
                }
                else if (!string.IsNullOrWhiteSpace(computedContentHash) &&
                         !string.Equals(finalDoc.ContentHash, computedContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    finalDoc.ContentHash = computedContentHash;
                }

                if (!string.IsNullOrWhiteSpace(finalDoc.PendingTargetVersion))
                {
                    finalDoc.Version = SemanticDocumentVersion.Normalize(finalDoc.PendingTargetVersion);
                    finalDoc.PendingTargetVersion = null;
                }

                finalDoc.IndexingStatus = DocumentIndexingStatuses.Processing;
                finalDoc.IndexingProgress = DocumentIndexingPhases.OverallProgress(DocumentIndexingPhases.EnrichMetadata, 0);
                finalDoc.IndexingErrorMessage = null;
                finalDoc.IsOutdated = false;
                finalDoc.UpdatedAt = DateTime.UtcNow;
                if (finalDoc.TotalPages > 0)
                    finalDoc.CurrentPageIndexing = finalDoc.TotalPages;
                await EnsureDocumentDefaultsAsync(finalDoc, cancellationToken);
                await _unitOfWork.DocumentRepository.UpdateAsync(finalDoc);
                await _unitOfWork.SaveAsync();
                await swapTransaction.CommitAsync(cancellationToken);

                var enrichTotalChunks = finalDoc.TotalChunks;

                await ReportProgressAsync(
                    documentId,
                    DocumentIndexingPhases.EnrichMetadata,
                    0,
                    "Enriching chunk metadata...",
                    finalDoc.TotalPages,
                    enrichTotalChunks,
                    finalDoc.CurrentPageIndexing,
                    cancellationToken);

                var metadataEnrich = await _pythonAi.EnrichDocumentChunksAsync(
                    documentId,
                    DocumentEnrichPhase.Metadata,
                    cancellationToken: cancellationToken,
                    onBatchProgressAsync: async (processed, _, ct) =>
                    {
                        var fraction = enrichTotalChunks > 0 ? processed / (double)enrichTotalChunks : 1;
                        await ReportProgressAsync(
                            documentId,
                            DocumentIndexingPhases.EnrichMetadata,
                            fraction,
                            $"Enriching metadata ({processed}/{enrichTotalChunks})...",
                            finalDoc.TotalPages,
                            enrichTotalChunks,
                            finalDoc.CurrentPageIndexing,
                            ct,
                            processed);
                    });

                if (!metadataEnrich.Success)
                {
                    var detail = metadataEnrich.ErrorMessage ?? "metadata enrichment failed";
                    _logger.LogError(
                        "[DocumentIndexing] Metadata enrichment failed for document {DocumentId}. Detail: {Detail}",
                        documentId,
                        detail);
                    await MarkFailedAsync(documentId, $"Chunk metadata enrichment failed: {detail}", cancellationToken);
                    completed = true;
                    failoverMarked = true;
                    return;
                }

                await ReportProgressAsync(
                    documentId,
                    DocumentIndexingPhases.GenerateEmbeddings,
                    0,
                    "Generating embeddings...",
                    finalDoc.TotalPages,
                    enrichTotalChunks,
                    finalDoc.CurrentPageIndexing,
                    cancellationToken);

                var embeddingEnrich = await _pythonAi.EnrichDocumentChunksAsync(
                    documentId,
                    DocumentEnrichPhase.Embeddings,
                    onlyMissingEmbedding: true,
                    cancellationToken: cancellationToken,
                    onBatchProgressAsync: async (processed, nullRemaining, ct) =>
                    {
                        var fraction = enrichTotalChunks > 0 ? processed / (double)enrichTotalChunks : 1;
                        await ReportProgressAsync(
                            documentId,
                            DocumentIndexingPhases.GenerateEmbeddings,
                            fraction,
                            $"Generating embeddings ({processed}/{enrichTotalChunks})...",
                            finalDoc.TotalPages,
                            enrichTotalChunks,
                            finalDoc.CurrentPageIndexing,
                            ct,
                            processed);
                    });

                if (!embeddingEnrich.Success || embeddingEnrich.NullEmbeddingRemaining > 0)
                {
                    var detail = embeddingEnrich.ErrorMessage ?? $"null_embeddings={embeddingEnrich.NullEmbeddingRemaining}";
                    _logger.LogError(
                        "[DocumentIndexing] Embedding enrichment failed for document {DocumentId}. Provider response: {Detail}",
                        documentId,
                        detail);
                    await MarkFailedAsync(documentId, $"Chunk embedding enrichment failed: {detail}", cancellationToken);
                    completed = true;
                    failoverMarked = true;
                    return;
                }

                _logger.LogInformation(
                    "[DocumentIndexing] Enriched {MetadataCount} metadata + {EmbeddingCount} embeddings for document {DocumentId}.",
                    metadataEnrich.ChunksProcessed,
                    embeddingEnrich.ChunksProcessed,
                    documentId);

                var completedDoc = await _unitOfWork.DocumentRepository.GetByIdAsync(documentId);
                if (completedDoc != null)
                {
                    completedDoc.IndexingStatus = DocumentIndexingStatuses.Completed;
                    completedDoc.IndexingProgress = 100;
                    completedDoc.IndexingErrorMessage = null;
                    completedDoc.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.DocumentRepository.UpdateAsync(completedDoc);
                    await _unitOfWork.SaveAsync();
                    finalDoc = completedDoc;
                }

                var completedAt = finalDoc.UpdatedAt ?? DateTime.UtcNow;
                await _progressNotifier.NotifyIndexingCompletedAsync(
                    documentId,
                    DocumentIndexingStatuses.Completed,
                    finalDoc.Version,
                    completedAt,
                    cancellationToken);

                SetProgress(
                    documentId,
                    100,
                    "Completed.",
                    finalDoc.TotalPages,
                    finalDoc.TotalChunks,
                    finalDoc.CurrentPageIndexing,
                    DocumentIndexingPhases.GenerateEmbeddings,
                    enrichTotalChunks);
                await _progressNotifier.NotifyProgressAsync(
                    documentId,
                    finalDoc.TotalPages,
                    finalDoc.TotalChunks,
                    finalDoc.CurrentPageIndexing,
                    100,
                    "Completed.",
                    cancellationToken,
                    DocumentIndexingPhases.GenerateEmbeddings,
                    enrichTotalChunks,
                    DocumentIndexingPhases.Label(DocumentIndexingPhases.GenerateEmbeddings));
            }
            catch (Exception swapEx)
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

                _logger.LogError(swapEx, "[DocumentIndexing] Atomic swap failed for document {DocumentId}.", documentId);
                throw;
            }

            _logger.LogInformation("[DocumentIndexing] Completed document {DocumentId}.", documentId);
            completed = true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[DocumentIndexing] Processing cancelled for {DocumentId}.", documentId);
            await MarkFailedAsync(documentId, "Processing cancelled.", CancellationToken.None);
            failoverMarked = true;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocumentIndexing] Fatal error for document {DocumentId}.", documentId);
            await MarkFailedAsync(documentId, ex.Message, cancellationToken);
            failoverMarked = true;
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempPdfPath))
                TryDeleteTempPdf(tempPdfPath);

            if (!completed && !failoverMarked)
                await MarkFailedAsync(documentId, "Indexing did not complete.", CancellationToken.None);
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

    private async Task MarkFailedAsync(Guid documentId, string? errorMessage, CancellationToken cancellationToken)
    {
        var safeError = string.IsNullOrWhiteSpace(errorMessage)
            ? "Document indexing failed."
            : errorMessage.Trim();
        if (safeError.Length > 2000)
            safeError = safeError[..2000];

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
            var notifier = scope.ServiceProvider.GetRequiredService<IDocumentIndexingProgressNotifier>();
            var doc = await uow.DocumentRepository.GetByIdAsync(documentId);
            var cacheStatus = DocumentIndexingStatuses.Failed;
            var cacheOperation = "Failed.";
            string? persistedError = safeError;
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
                    doc.PendingTargetVersion = null;
                    doc.IndexingStatus = DocumentIndexingStatuses.Completed;
                    doc.IndexingProgress = 100;
                    doc.IndexingErrorMessage = null;
                    cacheStatus = DocumentIndexingStatuses.Completed;
                    cacheOperation = null;
                    persistedError = "Reindexing failed; kept previous version active. " + safeError;

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
                    doc.PendingTargetVersion = null;
                    doc.IndexingStatus = DocumentIndexingStatuses.Failed;
                    doc.IndexingProgress = 100;
                    doc.IndexingErrorMessage = safeError;
                }

                await uow.DocumentRepository.UpdateAsync(doc);
                await uow.SaveAsync();

                if (string.Equals(cacheStatus, DocumentIndexingStatuses.Failed, StringComparison.OrdinalIgnoreCase))
                {
                    await notifier.NotifyIndexingFailedAsync(
                        documentId,
                        cacheStatus,
                        safeError,
                        doc.TotalPages,
                        doc.TotalChunks,
                        doc.CurrentPageIndexing,
                        CancellationToken.None);
                }
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
                    CurrentPageIndexing = doc?.CurrentPageIndexing ?? 0,
                    ErrorMessage = string.Equals(cacheStatus, DocumentIndexingStatuses.Failed, StringComparison.OrdinalIgnoreCase)
                        ? safeError
                        : persistedError
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

    private async Task EnsureDocumentDefaultsAsync(Document document, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(document.DefaultModality))
            return;

        document.DefaultModality = DocumentMetadataValidation.DefaultModality;
        document.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.DocumentRepository.UpdateAsync(document);
        await _unitOfWork.SaveAsync();
        _logger.LogInformation(
            "[DocumentIndexing] Backfilled default_modality={Modality} for legacy document {DocumentId}.",
            document.DefaultModality,
            document.Id);
    }

    private async Task ReportProgressAsync(
        Guid documentId,
        int indexingPhase,
        double phaseFraction,
        string operation,
        int totalPages,
        int totalChunks,
        int currentPageIndexing,
        CancellationToken cancellationToken,
        int chunksProcessed = 0)
    {
        var progress = indexingPhase >= DocumentIndexingPhases.MaxPhase && phaseFraction >= 1
            ? 100
            : DocumentIndexingPhases.OverallProgress(indexingPhase, phaseFraction);
        var phaseLabel = DocumentIndexingPhases.Label(indexingPhase);

        SetProgress(
            documentId,
            progress,
            operation,
            totalPages,
            totalChunks,
            currentPageIndexing,
            indexingPhase,
            chunksProcessed,
            phaseLabel);

        await _progressNotifier.NotifyProgressAsync(
            documentId,
            totalPages,
            totalChunks,
            currentPageIndexing,
            progress,
            operation,
            cancellationToken,
            indexingPhase,
            chunksProcessed,
            phaseLabel);
    }

    private void SetProgress(
        Guid documentId,
        int percentage,
        string operation,
        int totalPages = 0,
        int totalChunks = 0,
        int currentPageIndexing = 0,
        int indexingPhase = 0,
        int chunksProcessed = 0,
        string? phaseLabel = null)
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
            CurrentOperation = string.Equals(statusLabel, DocumentIndexingStatuses.Completed, StringComparison.OrdinalIgnoreCase)
                ? null
                : operation,
            TotalPages = totalPages,
            TotalChunks = totalChunks,
            CurrentPageIndexing = currentPageIndexing,
            ErrorMessage = string.Equals(statusLabel, DocumentIndexingStatuses.Failed, StringComparison.OrdinalIgnoreCase)
                ? operation
                : null,
            IndexingPhase = indexingPhase,
            PhaseLabel = string.IsNullOrWhiteSpace(phaseLabel) && indexingPhase > 0
                ? DocumentIndexingPhases.Label(indexingPhase)
                : phaseLabel,
            ChunksProcessed = chunksProcessed
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
