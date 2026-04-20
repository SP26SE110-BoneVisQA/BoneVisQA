using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Services;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Services;

/// <summary>
/// Runs once at startup to recover and immediately process indexing work left in
/// Pending/Processing state after an unclean shutdown.
/// </summary>
public sealed class StartupReindexingHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StartupReindexingHostedService> _logger;
    private CancellationTokenSource? _startupCts;
    private Task? _startupTask;

    public StartupReindexingHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<StartupReindexingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Do not block host startup; run recovery in the background.
        _startupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _startupTask = Task.Run(() => RunRecoveryAsync(_startupCts.Token), _startupCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_startupCts == null || _startupTask == null)
            return;

        try
        {
            _startupCts.Cancel();
            await _startupTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation during shutdown.
        }
    }

    private async Task RunRecoveryAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var targetStatuses = new[] { DocumentIndexingStatuses.Pending, DocumentIndexingStatuses.Reindexing, DocumentIndexingStatuses.Processing };

        var documentIds = await uow.Context.Documents
            .Where(d => targetStatuses.Contains(d.IndexingStatus))
            .OrderBy(d => d.CreatedAt)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var medicalCaseIds = await uow.Context.MedicalCases
            .Where(mc => targetStatuses.Contains(mc.IndexingStatus))
            .OrderBy(mc => mc.UpdatedAt)
            .Select(mc => mc.Id)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "[StartupReindex] Found {DocumentCount} documents and {CaseCount} medical cases to recover.",
            documentIds.Count,
            medicalCaseIds.Count);

        foreach (var documentId in documentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessDocumentAsync(documentId, cancellationToken);
        }

        foreach (var medicalCaseId in medicalCaseIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessMedicalCaseAsync(medicalCaseId, cancellationToken);
        }
    }

    private async Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var processor = scope.ServiceProvider.GetRequiredService<IDocumentIndexingProcessor>();

        try
        {
            var document = await uow.Context.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (document == null)
                return;

            document.IndexingStatus = DocumentIndexingStatuses.Processing;
            await uow.Context.SaveChangesAsync(cancellationToken);
            await processor.ProcessDocumentAsync(documentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StartupReindex] Failed to recover document {DocumentId}.", documentId);
        }
    }

    private async Task ProcessMedicalCaseAsync(Guid medicalCaseId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var processor = scope.ServiceProvider.GetRequiredService<IMedicalCaseIndexingProcessor>();

        try
        {
            var medicalCase = await uow.Context.MedicalCases.FirstOrDefaultAsync(mc => mc.Id == medicalCaseId, cancellationToken);
            if (medicalCase == null)
                return;

            medicalCase.IndexingStatus = DocumentIndexingStatuses.Processing;
            await uow.Context.SaveChangesAsync(cancellationToken);
            await processor.ProcessMedicalCaseAsync(medicalCaseId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StartupReindex] Failed to recover medical case {MedicalCaseId}.", medicalCaseId);
        }
    }
}
