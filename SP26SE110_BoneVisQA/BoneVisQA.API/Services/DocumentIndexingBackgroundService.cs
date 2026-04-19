using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Services;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Services;

/// <summary>
/// Polls for documents with <c>indexing_status = Pending</c>, claims one row with <c>FOR UPDATE SKIP LOCKED</c>, and runs the RAG indexing pipeline.
/// </summary>
public sealed class DocumentIndexingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly SemaphoreSlim WorkerGate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentIndexingBackgroundService> _logger;

    public DocumentIndexingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentIndexingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TryProcessOnePendingDocumentAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DocumentIndexing] Unexpected error in indexing loop.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task TryProcessOnePendingDocumentAsync(CancellationToken stoppingToken)
    {
        await WorkerGate.WaitAsync(stoppingToken);
        try
        {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(stoppingToken);
        var locked = await unitOfWork.Context.Documents
            .FromSqlRaw(
                """
                SELECT * FROM documents
                WHERE indexing_status IN ('Pending', 'Reindexing')
                ORDER BY created_at ASC NULLS LAST
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(stoppingToken);

        if (locked.Count == 0)
        {
            await transaction.CommitAsync(stoppingToken);
            return;
        }

        var doc = locked[0];
        var docId = doc.Id;
        doc.IndexingStatus = DocumentIndexingStatuses.Processing;
        await unitOfWork.Context.SaveChangesAsync(stoppingToken);
        await transaction.CommitAsync(stoppingToken);

        await using var workScope = _scopeFactory.CreateAsyncScope();
        var processor = workScope.ServiceProvider.GetRequiredService<IDocumentIndexingProcessor>();
        try
        {
            await processor.ProcessDocumentAsync(docId, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocumentIndexing] Processor failed for document {DocumentId}.", docId);
            await MarkFailedAsync(docId, workScope.ServiceProvider, stoppingToken);
        }
        }
        finally
        {
            WorkerGate.Release();
        }
    }

    private async Task MarkFailedAsync(Guid documentId, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        try
        {
            var uow = serviceProvider.GetRequiredService<IUnitOfWork>();
            var doc = await uow.DocumentRepository.GetByIdAsync(documentId);
            if (doc != null)
            {
                doc.IndexingStatus = DocumentIndexingStatuses.Failed;
                doc.IndexingProgress = 100;
                await uow.DocumentRepository.UpdateAsync(doc);
                await uow.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocumentIndexing] Could not set Failed for document {DocumentId}.", documentId);
        }
    }
}
