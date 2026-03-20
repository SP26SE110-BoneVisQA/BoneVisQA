using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace BoneVisQA.Services.Services;

public class DocumentService : IDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISupabaseStorageService _storageService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IUnitOfWork unitOfWork,
        ISupabaseStorageService storageService,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentService> logger)
    {
        _unitOfWork = unitOfWork;
        _storageService = storageService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<DocumentDto> UploadDocumentAsync(IFormFile file, DocumentUploadDto metadata)
    {
        var fileUrl = await _storageService.UploadFileAsync(file, "knowledge_base", "documents");

        var document = new Document
        {
            Id = Guid.NewGuid(),
            Title = metadata.Title,
            FilePath = fileUrl,
            CategoryId = metadata.CategoryId,
            IndexingStatus = "Processing",
            Version = 1,
            IsOutdated = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.DocumentRepository.AddAsync(document);
        await _unitOfWork.SaveAsync();

        var docId = document.Id;
        var url = fileUrl;
        _ = Task.Run(async () =>
        {
            try
            {
                await IngestDocumentInBackgroundAsync(docId, url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in background ingest task wrapper for Document ID {DocumentId}", docId);
                await MarkIndexingFailedInNewScopeAsync(docId);
            }
        });

        return MapToDto(document);
    }

    public async Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync()
    {
        var documents = await _unitOfWork.DocumentRepository.GetAllAsync();
        return documents.Select(MapToDto);
    }

    public async Task<DocumentDto?> GetDocumentByIdAsync(Guid id)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        return document == null ? null : MapToDto(document);
    }

    public async Task<bool> DeleteDocumentAsync(Guid id)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        if (document == null) return false;

        var chunks = await _unitOfWork.DocumentChunkRepository.FindAsync(c => c.DocId == id);
        if (chunks.Count > 0)
        {
            await _unitOfWork.DocumentChunkRepository.RemoveRangeAsync(chunks);
        }

        await _unitOfWork.DocumentRepository.DeleteAsync(id);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task<bool> TriggerReindexAsync(Guid id)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        if (document == null || string.IsNullOrEmpty(document.FilePath)) return false;

        document.IndexingStatus = "Processing";
        document.Version += 1;
        await _unitOfWork.DocumentRepository.UpdateAsync(document);
        await _unitOfWork.SaveAsync();

        var docId = document.Id;
        var url = document.FilePath;
        _ = Task.Run(async () =>
        {
            try
            {
                await IngestDocumentInBackgroundAsync(docId, url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in background ingest task wrapper for Document ID {DocumentId}", docId);
                await MarkIndexingFailedInNewScopeAsync(docId);
            }
        });

        return true;
    }

    public async Task UpdateIndexingStatusAsync(Guid id, string status)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        if (document != null)
        {
            document.IndexingStatus = status;
            await _unitOfWork.DocumentRepository.UpdateAsync(document);
            await _unitOfWork.SaveAsync();
        }
    }

    private async Task IngestDocumentInBackgroundAsync(Guid docId, string fileUrl)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var pdf = scope.ServiceProvider.GetRequiredService<IPdfProcessingService>();
            var embedding = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

            _logger.LogInformation("Background ingestion started for Document ID {DocumentId}", docId);

            await SetStatusAsync(uow, docId, "Processing");

            var chunkTexts = await pdf.DownloadAndChunkPdfAsync(fileUrl);
            _logger.LogInformation("Extracted {ChunkCount} chunks. Starting embedding generation...", chunkTexts.Count);

            var existing = await uow.DocumentChunkRepository.FindAsync(c => c.DocId == docId);
            if (existing.Count > 0)
            {
                await uow.DocumentChunkRepository.RemoveRangeAsync(existing);
                await uow.SaveAsync();
            }

            var entities = new List<DocumentChunk>();
            for (var i = 0; i < chunkTexts.Count; i++)
            {
                var vec = await embedding.EmbedTextAsync(chunkTexts[i]);
                entities.Add(new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocId = docId,
                    Content = chunkTexts[i],
                    ChunkOrder = i,
                    Embedding = new Vector(vec),
                    IsFlagged = false
                });
            }

            _logger.LogInformation("Embeddings generated. Saving to database...");

            if (entities.Count > 0)
                await uow.DocumentChunkRepository.AddRangeAsync(entities);

            await uow.SaveAsync();
            await SetStatusAsync(uow, docId, "Completed");
            _logger.LogInformation("Background ingestion completed successfully for Document ID {DocumentId}", docId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FATAL ERROR in background ingestion for Document ID {DocumentId}", docId);
            await MarkIndexingFailedInNewScopeAsync(docId);
        }
    }

    /// <summary>
    /// Uses a fresh scope so Failed status is persisted even if the ingestion scope/DbContext is faulted.
    /// </summary>
    private async Task MarkIndexingFailedInNewScopeAsync(Guid docId)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await SetStatusAsync(uow, docId, "Failed");
            _logger.LogWarning("Document {DocumentId} IndexingStatus set to Failed (new DbContext scope).", docId);
        }
        catch (Exception inner)
        {
            _logger.LogError(inner, "Could not update IndexingStatus to Failed for Document ID {DocumentId}", docId);
        }
    }

    private static async Task SetStatusAsync(IUnitOfWork uow, Guid docId, string status)
    {
        var doc = await uow.DocumentRepository.GetByIdAsync(docId);
        if (doc != null)
        {
            doc.IndexingStatus = status;
            await uow.DocumentRepository.UpdateAsync(doc);
            await uow.SaveAsync();
        }
    }

    private static DocumentDto MapToDto(Document doc) => new()
    {
        Id = doc.Id,
        Title = doc.Title,
        FilePath = doc.FilePath,
        CategoryId = doc.CategoryId,
        IndexingStatus = doc.IndexingStatus,
        Version = doc.Version,
        IsOutdated = doc.IsOutdated,
        CreatedAt = doc.CreatedAt
    };
}
