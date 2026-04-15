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
        var objectPath = $"documents/{documentId}{ext}";
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
                Version = 1,
                IsOutdated = false,
                CreatedAt = DateTime.UtcNow,
                TotalPages = 0,
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

                throw new InvalidOperationException("Tài liệu có cùng nội dung (hash) đã tồn tại trong hệ thống.");
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
    /// Replaces the file when the SHA-256 hash changes: uploads new blob, removes old vectors, bumps version, queues re-indexing.
    /// </summary>
    public async Task<DocumentDto> UpdateDocumentFileAsync(
        Guid id,
        IFormFile file,
        DocumentUploadDto metadata,
        CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id)
                       ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

        var newHash = await ComputeSha256HashAsync(file, cancellationToken);
        var oldPath = document.FilePath;

        if (string.Equals(document.ContentHash, newHash, StringComparison.OrdinalIgnoreCase))
        {
            await using var tx = await _unitOfWork.Context.Database.BeginTransactionAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(metadata.Title))
                document.Title = metadata.Title.Trim();
            if (metadata.CategoryId.HasValue)
                document.CategoryId = metadata.CategoryId;

            await SyncDocumentTagsAsync(document.Id, metadata.TagIds, cancellationToken);
            await _unitOfWork.DocumentRepository.UpdateAsync(document);
            await _unitOfWork.SaveAsync();
            await tx.CommitAsync(cancellationToken);
            return MapToDto(document);
        }

        await using (var clearTx = await _unitOfWork.Context.Database.BeginTransactionAsync(cancellationToken))
        {
            var chunks = await _unitOfWork.DocumentChunkRepository.FindAsync(c => c.DocId == id);
            if (chunks.Count > 0)
                await _unitOfWork.DocumentChunkRepository.RemoveRangeAsync(chunks);

            document.Version += 1;
            document.IndexingStatus = DocumentIndexingStatuses.Pending;
            document.IndexingProgress = 0;
            document.TotalPages = 0;
            document.CurrentPageIndexing = 0;
            document.IsOutdated = false;
            if (!string.IsNullOrWhiteSpace(metadata.Title))
                document.Title = metadata.Title.Trim();
            if (metadata.CategoryId.HasValue)
                document.CategoryId = metadata.CategoryId;

            await SyncDocumentTagsAsync(document.Id, metadata.TagIds, cancellationToken);
            await _unitOfWork.DocumentRepository.UpdateAsync(document);
            await _unitOfWork.SaveAsync();
            await clearTx.CommitAsync(cancellationToken);
        }

        string? newUrl = null;
        try
        {
            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".pdf";
            var objectPath = $"documents/{id}{ext}";
            newUrl = await _storageService.UploadFileToPathAsync(file, "knowledge_base", objectPath, cancellationToken);

            await using var pathTx = await _unitOfWork.Context.Database.BeginTransactionAsync(cancellationToken);
            document.FilePath = newUrl;
            document.ContentHash = newHash;
            await _unitOfWork.DocumentRepository.UpdateAsync(document);
            await _unitOfWork.SaveAsync();
            await pathTx.CommitAsync(cancellationToken);

            SetProgress(document.Id, 0, "Queued for re-indexing...");

            if (!string.IsNullOrEmpty(oldPath)
                && !string.Equals(NormalizeStorageUrl(oldPath), NormalizeStorageUrl(newUrl), StringComparison.OrdinalIgnoreCase)
                && TryExtractSupabaseFilePointer(oldPath, out var ob, out var op))
            {
                try
                {
                    await _storageService.DeleteFileAsync(ob, op, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete previous document file from storage.");
                }
            }

            return MapToDto(document);
        }
        catch
        {
            if (!string.IsNullOrEmpty(newUrl) && TryExtractSupabaseFilePointer(newUrl, out var nb, out var np))
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

    private static string? NormalizeStorageUrl(string url) => url?.Trim().TrimEnd('/');

    private async Task SyncDocumentTagsAsync(Guid documentId, List<Guid>? tagIds, CancellationToken cancellationToken)
    {
        if (tagIds == null)
            return;

        var existing = await _unitOfWork.DocumentTagRepository.FindAsync(dt => dt.DocumentId == documentId);
        var wanted = tagIds.Where(x => x != Guid.Empty).Distinct().ToHashSet();

        var toRemove = existing.Where(dt => !wanted.Contains(dt.TagId)).ToList();
        if (toRemove.Count > 0)
            await _unitOfWork.DocumentTagRepository.RemoveRangeAsync(toRemove);

        var have = existing.Select(e => e.TagId).ToHashSet();
        var toAdd = wanted
            .Where(id => !have.Contains(id))
            .Select(id => new DocumentTag
            {
                DocumentId = documentId,
                TagId = id,
                CreatedAt = DateTime.UtcNow
            }).ToList();
        if (toAdd.Count > 0)
            await _unitOfWork.DocumentTagRepository.AddRangeAsync(toAdd);

        await _unitOfWork.SaveAsync();
    }

    public async Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync()
    {
        var documents = await _unitOfWork.DocumentRepository.GetAllAsync();
        return documents
            .OrderByDescending(d => d.CreatedAt ?? DateTime.MinValue)
            .Select(MapToDto);
    }

    public async Task<DocumentDto?> GetDocumentByIdAsync(Guid id)
    {
        var document = await _unitOfWork.Context.Documents
            .AsNoTracking()
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

        var filePath = document.FilePath;
        await _unitOfWork.DocumentRepository.DeleteAsync(id);
        await _unitOfWork.SaveAsync();

        if (!string.IsNullOrEmpty(filePath) && TryExtractSupabaseFilePointer(filePath, out var bucket, out var filePathRel))
        {
            try
            {
                await _storageService.DeleteFileAsync(bucket, filePathRel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Storage delete failed for document {DocumentId}; DB row removed.", id);
            }
        }

        return true;
    }

    public async Task<bool> TriggerReindexAsync(Guid id)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        if (document == null || string.IsNullOrEmpty(document.FilePath)) return false;

        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync();
        try
        {
            var chunks = await _unitOfWork.DocumentChunkRepository.FindAsync(c => c.DocId == id);
            if (chunks.Count > 0)
                await _unitOfWork.DocumentChunkRepository.RemoveRangeAsync(chunks);

            document.IndexingStatus = DocumentIndexingStatuses.Pending;
            document.IndexingProgress = 0;
            document.TotalPages = 0;
            document.CurrentPageIndexing = 0;
            document.Version += 1;
            document.IsOutdated = false;
            await _unitOfWork.DocumentRepository.UpdateAsync(document);
            await _unitOfWork.SaveAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        SetProgress(document.Id, 0, "Queued for re-indexing...");
        return true;
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
                    : string.Equals(normalizedStatus, DocumentIndexingStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                        ? "Queued for indexing..."
                        : "Indexing..."
        };
    }

    private static DocumentDto MapToDto(Document doc) => new()
    {
        Id = doc.Id,
        Title = doc.Title,
        FilePath = doc.FilePath,
        CategoryId = doc.CategoryId,
        IndexingStatus = NormalizeApiStatus(doc.IndexingStatus),
        IndexingProgress = doc.IndexingProgress,
        ContentHash = doc.ContentHash,
        Version = doc.Version,
        IsOutdated = doc.IsOutdated,
        CreatedAt = doc.CreatedAt,
        TotalPages = doc.TotalPages,
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
