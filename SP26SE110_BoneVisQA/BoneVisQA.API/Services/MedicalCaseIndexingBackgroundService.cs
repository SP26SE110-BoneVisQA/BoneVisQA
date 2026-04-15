using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Services;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Services;

/// <summary>
/// Polls <c>medical_cases</c> with <c>indexing_status = Pending</c>, claims a row with <c>FOR UPDATE SKIP LOCKED</c>, and writes <c>embedding</c>.
/// </summary>
public sealed class MedicalCaseIndexingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly SemaphoreSlim WorkerGate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MedicalCaseIndexingBackgroundService> _logger;

    public MedicalCaseIndexingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<MedicalCaseIndexingBackgroundService> logger)
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
                await TryProcessOnePendingCaseAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MedicalCaseIndexing] Unexpected error in indexing loop.");
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

    private async Task TryProcessOnePendingCaseAsync(CancellationToken stoppingToken)
    {
        await WorkerGate.WaitAsync(stoppingToken);
        try
        {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(stoppingToken);
        var locked = await unitOfWork.Context.MedicalCases
            .FromSqlRaw(
                """
                SELECT * FROM medical_cases
                WHERE indexing_status = 'Pending'
                  AND is_approved IS TRUE
                  AND is_active IS TRUE
                ORDER BY updated_at ASC NULLS LAST
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(stoppingToken);

        if (locked.Count == 0)
        {
            await transaction.CommitAsync(stoppingToken);
            return;
        }

        var mc = locked[0];
        var caseId = mc.Id;
        mc.IndexingStatus = DocumentIndexingStatuses.Processing;
        await unitOfWork.Context.SaveChangesAsync(stoppingToken);
        await transaction.CommitAsync(stoppingToken);

        await using var workScope = _scopeFactory.CreateAsyncScope();
        var processor = workScope.ServiceProvider.GetRequiredService<IMedicalCaseIndexingProcessor>();
        try
        {
            await processor.ProcessMedicalCaseAsync(caseId, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MedicalCaseIndexing] Processor failed for case {CaseId}.", caseId);
            await MarkFailedAsync(caseId, workScope.ServiceProvider, stoppingToken);
        }
        }
        finally
        {
            WorkerGate.Release();
        }
    }

    private async Task MarkFailedAsync(Guid medicalCaseId, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        try
        {
            var uow = serviceProvider.GetRequiredService<IUnitOfWork>();
            var mc = await uow.Context.MedicalCases.FirstOrDefaultAsync(x => x.Id == medicalCaseId, cancellationToken);
            if (mc != null)
            {
                mc.IndexingStatus = DocumentIndexingStatuses.Failed;
                await uow.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MedicalCaseIndexing] Could not set Failed for case {CaseId}.", medicalCaseId);
        }
    }
}
