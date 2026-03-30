using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Exceptions;
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

    /// <summary>
    /// Uploads the PDF to storage, persists metadata with <see cref="Document.IndexingStatus"/> = Processing,
    /// then returns immediately. Text extraction, chunking, and embeddings run in a fire-and-forget background task
    /// so the HTTP response is not blocked by heavy RAG ingestion.
    /// </summary>
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
        var scopeFactory = _scopeFactory;
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var scopedService = scope.ServiceProvider.GetRequiredService<DocumentService>();
            try
            {
                await scopedService.IngestDocumentInBackgroundAsync(docId, url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<DocumentService>>();
                logger.LogError(ex, "Background ingestion failed for Document ID {DocumentId}.", docId);
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
        var scopeFactory = _scopeFactory;
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var scopedService = scope.ServiceProvider.GetRequiredService<DocumentService>();
            try
            {
                await scopedService.IngestDocumentInBackgroundAsync(docId, url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<DocumentService>>();
                logger.LogError(ex, "Background ingestion failed for Document ID {DocumentId}.", docId);
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

    public async Task IngestDocumentInBackgroundAsync(Guid docId, string fileUrl)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var pdf = scope.ServiceProvider.GetRequiredService<IPdfProcessingService>();
            var embedding = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

            _logger.LogInformation("Background ingestion started for Document ID {DocumentId}", docId);

            await SetStatusAsync(uow, docId, "Processing");

            var fullText = await pdf.DownloadAndExtractPdfTextAsync(fileUrl);
            var chunkTexts = SplitTextRecursively(fullText, maxSize: 800, overlap: 150);
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
        catch (EmbeddingFailedException ex)
        {
            _logger.LogError(ex, "Embedding failed; aborting ingestion for Document ID {DocumentId}", docId);
            await MarkIndexingFailedInNewScopeAsync(docId);
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

    /// <summary>
    /// Recursive character text splitter for better RAG:
    /// try paragraph (\n\n) then newline (\n) then sentence (. ! ?) then spaces, with character overlap.
    /// </summary>
    private static List<string> SplitTextRecursively(string text, int maxSize, int overlap)
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

            var chunk = remaining.Substring(0, cutIndex).Trim();
            if (chunk.Length > 0)
                chunks.Add(chunk);

            if (cutIndex >= remaining.Length)
                break;

            if (overlap <= 0)
            {
                remaining = remaining.Substring(cutIndex).TrimStart();
                continue;
            }

            var overlapLen = Math.Min(overlap, chunk.Length);
            var overlapTail = overlapLen > 0 ? chunk.Substring(chunk.Length - overlapLen) : string.Empty;
            var rest = remaining.Substring(cutIndex);

            remaining = (overlapTail + rest).TrimStart();

            // Safety: ensure forward progress.
            if (remaining.Length == 0)
                break;
        }

        return chunks;
    }

    private static int FindBestSplitIndex(string text, int maxSize)
    {
        var windowLen = Math.Min(maxSize, text.Length);
        var window = text.Substring(0, windowLen);

        // 1) Paragraph boundary.
        var paraIdx = window.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (paraIdx >= 0)
            return paraIdx + 2;

        // 2) Newline boundary.
        var newLineIdx = window.LastIndexOf('\n');
        if (newLineIdx >= 0)
            return newLineIdx + 1;

        // 3) Sentence boundary.
        var bestSentenceIdx = -1;
        var sentenceDelims = new[] { ". ", "? ", "! " };
        foreach (var delim in sentenceDelims)
        {
            var idx = window.LastIndexOf(delim, StringComparison.Ordinal);
            if (idx > bestSentenceIdx)
                bestSentenceIdx = idx;
        }
        if (bestSentenceIdx >= 0)
            return bestSentenceIdx + 2; // keep delimiter trailing space

        // 4) Space boundary.
        var spaceIdx = window.LastIndexOf(' ');
        if (spaceIdx > 0)
            return spaceIdx;

        // Absolute fallback: hard cut.
        return windowLen;
    }
}
