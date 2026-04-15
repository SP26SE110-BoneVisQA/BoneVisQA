using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace BoneVisQA.Services.Services;

public class DocumentService : IDocumentService
{
    private const string ProgressCacheKeyPrefix = "document-ingestion-progress:";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISupabaseStorageService _storageService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IUnitOfWork unitOfWork,
        ISupabaseStorageService storageService,
        IMemoryCache memoryCache,
        ILogger<DocumentService> logger)
    {
        _unitOfWork = unitOfWork;
        _storageService = storageService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Uploads the PDF to storage and persists metadata with <see cref="DocumentIndexingStatuses.Pending"/> for the background indexer.
    /// </summary>
    public async Task<DocumentDto> UploadDocumentAsync(
        IFormFile file,
        DocumentUploadDto metadata,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contentHash = await ComputeSha256HashAsync(file, cancellationToken);

        var documentId = Guid.NewGuid();
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".pdf";
        const int initialVersion = 1;
        var objectPath = $"documents/{documentId}_v{initialVersion}{ext}";
        var fileUrl = await _storageService.UploadFileToPathAsync(
            file,
            "knowledge_base",
            objectPath,
            cancellationToken);

        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var document = new Document
            {
                Id = documentId,
                Title = metadata.Title,
                FilePath = fileUrl,
                CategoryId = metadata.CategoryId,
                IndexingStatus = DocumentIndexingStatuses.Pending,
                IndexingProgress = 0,
                ContentHash = contentHash,
                Version = initialVersion,
                IsOutdated = false,
                CreatedAt = DateTime.UtcNow,
                TotalPages = 0,
                TotalChunks = 0,
                CurrentPageIndexing = 0
            };

            await _unitOfWork.DocumentRepository.AddAsync(document);
            await _unitOfWork.SaveAsync();

            if (metadata.TagIds is { Count: > 0 })
            {
                var tagIds = metadata.TagIds
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList();

                if (tagIds.Count > 0)
                {
                    var toAdd = tagIds.Select(tagId => new DocumentTag
                    {
                        DocumentId = document.Id,
                        TagId = tagId,
                        CreatedAt = DateTime.UtcNow
                    }).ToList();

                    await _unitOfWork.DocumentTagRepository.AddRangeAsync(toAdd);
                    await _unitOfWork.SaveAsync();
                }
            }

            await transaction.CommitAsync(cancellationToken);
            SetProgress(document.Id, 0, "Queued for indexing...");

            return MapToDto(document);
        }
        catch (DbUpdateException ex)
        {
            var msg = $"{ex.InnerException?.Message} {ex.Message}";
            var duplicateHash = msg.Contains("content_hash", StringComparison.OrdinalIgnoreCase)
                                && msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
            await transaction.RollbackAsync(cancellationToken);
            if (duplicateHash)
            {
                if (TryExtractSupabaseFilePointer(fileUrl, out var bucket, out var path))
                {
                    try
                    {
                        await _storageService.DeleteFileAsync(bucket, path, cancellationToken);
                    }
                    catch (Exception delEx)
                    {
                        _logger.LogWarning(delEx, "Could not delete uploaded file after duplicate-hash rejection.");
                    }
                }

                throw new InvalidOperationException("A document with identical content hash already exists.");
            }

            throw;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            if (TryExtractSupabaseFilePointer(fileUrl, out var bucket, out var path))
            {
                try
                {
                    await _storageService.DeleteFileAsync(bucket, path, cancellationToken);
                }
                catch (Exception delEx)
                {
                    _logger.LogWarning(delEx, "Compensating delete failed after document insert failure.");
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Replaces the file when the SHA-256 hash changes: uploads new blob, removes old vectors, and queues re-indexing.
    /// </summary>
    public async Task<DocumentDto> UpdateDocumentFileAsync(
        Guid id,
        IFormFile file,
        DocumentUploadDto metadata,
        CancellationToken cancellationToken = default)
    {
        return await UpdateDocumentVersionAsync(id, file, cancellationToken);
    }

    public async Task<DocumentDto> UpdateDocumentVersionAsync(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id)
                       ?? throw new KeyNotFoundException("Document not found.");

        if (string.Equals(document.IndexingStatus, DocumentIndexingStatuses.Processing, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(document.IndexingStatus, DocumentIndexingStatuses.Pending, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(document.IndexingStatus, DocumentIndexingStatuses.Reindexing, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(document.IndexingStatus, "Indexing", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The document is currently being processed. Please wait until the current operation finishes.");
        }

        var newHash = await ComputeSha256HashAsync(file, cancellationToken);

        if (string.Equals(document.ContentHash, newHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The uploaded document is identical to the current version. No changes detected.");
        }

        string? pendingUrl = null;
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".pdf";
        var objectPath = $"documents/{id}_pending_{Guid.NewGuid():N}{ext}";
        try
        {
            pendingUrl = await _storageService.UploadFileToPathAsync(file, "knowledge_base", objectPath, cancellationToken);
            await using var tx = await _unitOfWork.Context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                document.IndexingStatus = DocumentIndexingStatuses.Reindexing;
                document.IndexingProgress = 0;
                document.CurrentPageIndexing = 0;
                document.IsOutdated = false;
                document.PendingReindexPath = pendingUrl;
                document.PendingReindexHash = newHash;

                await _unitOfWork.DocumentRepository.UpdateAsync(document);
                await _unitOfWork.SaveAsync();
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }

            SetProgress(document.Id, 0, "Queued for atomic re-indexing...");

            return MapToDto(document);
        }
        catch
        {
            if (!string.IsNullOrEmpty(pendingUrl) && TryExtractSupabaseFilePointer(pendingUrl, out var nb, out var np))
            {
                try
                {
                    await _storageService.DeleteFileAsync(nb, np, cancellationToken);
                }
                catch (Exception delEx)
                {
                    _logger.LogWarning(delEx, "Compensating delete failed after document update failure.");
                }
            }

            throw;
        }
    }

    public async Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync()
    {
        var documents = await _unitOfWork.Context.Documents
            .AsNoTracking()
            .Include(d => d.Category)
            .ToListAsync();
        return documents
            .OrderByDescending(d => d.CreatedAt ?? DateTime.MinValue)
            .Select(MapToDto);
    }

    public async Task<DocumentDto?> GetDocumentByIdAsync(Guid id)
    {
        var document = await _unitOfWork.Context.Documents
            .AsNoTracking()
            .Include(d => d.Category)
            .FirstOrDefaultAsync(d => d.Id == id);
        return document == null ? null : MapToDto(document);
    }

    public async Task<bool> DeleteDocumentAsync(Guid id)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        if (document == null) return false;

        var chunks = await _unitOfWork.DocumentChunkRepository.FindAsync(c => c.DocId == id);
        if (chunks.Count > 0)
            await _unitOfWork.DocumentChunkRepository.RemoveRangeAsync(chunks);

        var extGuess = Path.GetExtension(document.FilePath ?? "");
        if (string.IsNullOrWhiteSpace(extGuess))
            extGuess = ".pdf";

        await _unitOfWork.DocumentRepository.DeleteAsync(id);
        await _unitOfWork.SaveAsync();

        const string bucket = "knowledge_base";
        try
        {
            var versioned = await _storageService.ListObjectPathsAsync(bucket, $"documents/{id}_", CancellationToken.None);
            foreach (var path in versioned)
            {
                try
                {
                    await _storageService.DeleteFileAsync(bucket, path, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Storage delete failed for document {DocumentId} path {Path}.", id, path);
                }
            }

            var legacyPath = $"documents/{id}{extGuess}";
            await _storageService.DeleteFileAsync(bucket, legacyPath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Storage delete failed for document {DocumentId}; DB row removed.", id);
        }

        return true;
    }

    public Task<bool> TriggerReindexAsync(Guid id)
    {
        throw new InvalidOperationException("Re-indexing requires a new file upload. Use the update-version endpoint.");
    }

    public async Task UpdateIndexingStatusAsync(Guid id, string status)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        if (document != null)
        {
            document.IndexingStatus = NormalizeManualStatus(status);
            await _unitOfWork.DocumentRepository.UpdateAsync(document);
            await _unitOfWork.SaveAsync();
        }
    }

    public string MapStatusForApi(string? rawStatus) => NormalizeApiStatus(rawStatus);

    public async Task<DocumentIngestionStatusDto?> GetIngestionStatusAsync(Guid id)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        if (document == null)
            return null;

        var normalizedStatus = NormalizeApiStatus(document.IndexingStatus);
        if (_memoryCache.TryGetValue(GetProgressCacheKey(id), out DocumentIngestionStatusDto? progress)
            && progress != null)
        {
            if (!string.Equals(normalizedStatus, DocumentIndexingStatuses.Processing, StringComparison.OrdinalIgnoreCase))
            {
                progress.Status = normalizedStatus;
                if (string.Equals(normalizedStatus, DocumentIndexingStatuses.Completed, StringComparison.OrdinalIgnoreCase))
                    progress.ProgressPercentage = 100;
                else if (string.Equals(normalizedStatus, DocumentIndexingStatuses.Failed, StringComparison.OrdinalIgnoreCase))
                    progress.ProgressPercentage = Math.Min(progress.ProgressPercentage, 99);
            }

            return progress;
        }

        return new DocumentIngestionStatusDto
        {
            Status = normalizedStatus,
            ProgressPercentage = string.Equals(normalizedStatus, DocumentIndexingStatuses.Completed, StringComparison.OrdinalIgnoreCase)
                ? 100
                : document.IndexingProgress,
            CurrentOperation = string.Equals(normalizedStatus, DocumentIndexingStatuses.Completed, StringComparison.OrdinalIgnoreCase)
                ? "Completed."
                : string.Equals(normalizedStatus, DocumentIndexingStatuses.Failed, StringComparison.OrdinalIgnoreCase)
                    ? "Failed."
                    : string.Equals(normalizedStatus, DocumentIndexingStatuses.Reindexing, StringComparison.OrdinalIgnoreCase)
                        ? "Queued for zero-downtime re-indexing..."
                    : string.Equals(normalizedStatus, DocumentIndexingStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                        ? "Queued for indexing..."
                        : "Indexing...",
            TotalPages = document.TotalPages,
            TotalChunks = document.TotalChunks,
            CurrentPageIndexing = document.CurrentPageIndexing
        };
    }

    public async Task<IReadOnlyList<DocumentChunkCitationFrequencyDto>> GetChunkCitationFrequencyAsync(
        Guid? documentId = null,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        var safeTop = Math.Clamp(top, 1, 500);
        var query = _unitOfWork.Context.Citations
            .AsNoTracking()
            .Join(
                _unitOfWork.Context.DocumentChunks.AsNoTracking(),
                c => c.ChunkId,
                ch => ch.Id,
                (c, ch) => new { ch.DocId, c.ChunkId });

        if (documentId.HasValue)
            query = query.Where(x => x.DocId == documentId.Value);

        return await query
            .GroupBy(x => new { x.DocId, x.ChunkId })
            .Select(g => new DocumentChunkCitationFrequencyDto
            {
                DocumentId = g.Key.DocId,
                ChunkId = g.Key.ChunkId,
                RetrievalCount = g.Count()
            })
            .OrderByDescending(x => x.RetrievalCount)
            .Take(safeTop)
            .ToListAsync(cancellationToken);
    }

    private static DocumentDto MapToDto(Document doc) => new()
    {
        Id = doc.Id,
        Title = doc.Title,
        FilePath = doc.FilePath,
        CategoryId = doc.CategoryId,
        Category = doc.Category?.Name,
        IndexingStatus = NormalizeApiStatus(doc.IndexingStatus),
        IndexingProgress = doc.IndexingProgress,
        ContentHash = doc.ContentHash,
        Version = doc.Version,
        IsOutdated = doc.IsOutdated,
        CreatedAt = doc.CreatedAt,
        TotalPages = doc.TotalPages,
        TotalChunks = doc.TotalChunks,
        CurrentPageIndexing = doc.CurrentPageIndexing
    };

    private static string NormalizeManualStatus(string? status)
    {
        if (string.Equals(status, DocumentIndexingStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            return DocumentIndexingStatuses.Completed;
        if (string.Equals(status, DocumentIndexingStatuses.Failed, StringComparison.OrdinalIgnoreCase))
            return DocumentIndexingStatuses.Failed;
        return DocumentIndexingStatuses.Processing;
    }

    private static string NormalizeApiStatus(string? status)
    {
        if (string.Equals(status, DocumentIndexingStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            return DocumentIndexingStatuses.Completed;
        if (string.Equals(status, DocumentIndexingStatuses.Failed, StringComparison.OrdinalIgnoreCase))
            return DocumentIndexingStatuses.Failed;
        if (string.Equals(status, DocumentIndexingStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return DocumentIndexingStatuses.Pending;
        if (string.Equals(status, DocumentIndexingStatuses.Reindexing, StringComparison.OrdinalIgnoreCase))
            return DocumentIndexingStatuses.Reindexing;
        if (string.Equals(status, DocumentIndexingStatuses.Processing, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "In Progress", StringComparison.OrdinalIgnoreCase))
            return DocumentIndexingStatuses.Processing;
        return DocumentIndexingStatuses.Failed;
    }

    private void SetProgress(Guid docId, int percentage, string operation)
    {
        string statusLabel;
        if (percentage >= 100 && string.Equals(operation, "Completed.", StringComparison.OrdinalIgnoreCase))
            statusLabel = DocumentIndexingStatuses.Completed;
        else if (percentage >= 100 && string.Equals(operation, "Failed.", StringComparison.OrdinalIgnoreCase))
            statusLabel = DocumentIndexingStatuses.Failed;
        else if (operation.Contains("Queued", StringComparison.OrdinalIgnoreCase))
            statusLabel = DocumentIndexingStatuses.Pending;
        else
            statusLabel = DocumentIndexingStatuses.Processing;

        var value = new DocumentIngestionStatusDto
        {
            Status = statusLabel,
            ProgressPercentage = Math.Clamp(percentage, 0, 100),
            CurrentOperation = operation
        };

        _memoryCache.Set(GetProgressCacheKey(docId), value, TimeSpan.FromHours(4));
    }

    private static string GetProgressCacheKey(Guid docId) => $"{ProgressCacheKeyPrefix}{docId}";

    private static async Task<string> ComputeSha256HashAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
}
