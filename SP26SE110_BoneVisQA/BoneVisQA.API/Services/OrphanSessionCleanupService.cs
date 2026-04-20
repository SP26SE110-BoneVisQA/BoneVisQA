using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.API.Services;

public sealed class OrphanSessionCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrphanSessionCleanupService> _logger;

    public OrphanSessionCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrphanSessionCleanupService> logger)
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
                await CleanupOrphanSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrphanSessionCleanup] Unexpected cleanup error.");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CleanupOrphanSessionsAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var storageService = scope.ServiceProvider.GetRequiredService<ISupabaseStorageService>();

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(stoppingToken);
        var orphanSessions = await unitOfWork.Context.VisualQaSessions
            .FromSqlRaw(@"
                SELECT * FROM visual_qa_sessions
                WHERE status = 'Active'
                  AND custom_image_url IS NOT NULL
                  AND created_at < NOW() - INTERVAL '24 HOURS'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM qa_messages
                      WHERE session_id = visual_qa_sessions.id
                  )
                FOR UPDATE SKIP LOCKED
                LIMIT 50")
            .ToListAsync(stoppingToken);

        if (orphanSessions.Count == 0)
        {
            await transaction.CommitAsync(stoppingToken);
            return;
        }

        var sessionsToDelete = new List<BoneVisQA.Repositories.Models.VisualQASession>();
        foreach (var session in orphanSessions)
        {
            var imageUrl = session.CustomImageUrl;
            if (string.IsNullOrWhiteSpace(imageUrl))
                continue;

            if (TryExtractSupabaseFilePointer(imageUrl, out var bucket, out var filePath))
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(10));

                    var deleted = await storageService.DeleteFileAsync(bucket, filePath, cts.Token);
                    if (!deleted)
                    {
                        _logger.LogWarning(
                            "[OrphanSessionCleanup] Storage delete returned false for session {SessionId}. Url: {ImageUrl}",
                            session.Id,
                            imageUrl);
                        continue;
                    }

                    sessionsToDelete.Add(session);
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[OrphanSessionCleanup] Storage delete timed out for session {SessionId}. Url: {ImageUrl}",
                        session.Id,
                        imageUrl);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[OrphanSessionCleanup] Storage delete threw for session {SessionId}. Url: {ImageUrl}",
                        session.Id,
                        imageUrl);
                    continue;
                }
            }
            else
            {
                _logger.LogWarning(
                    "[OrphanSessionCleanup] Could not parse storage path for session {SessionId}. Keeping record for retry. Url: {ImageUrl}",
                    session.Id,
                    imageUrl);
                continue;
            }
        }

        if (sessionsToDelete.Count == 0)
        {
            await transaction.CommitAsync(stoppingToken);
            return;
        }

        unitOfWork.Context.VisualQaSessions.RemoveRange(sessionsToDelete);
        await unitOfWork.SaveAsync();
        await transaction.CommitAsync(stoppingToken);

        _logger.LogInformation(
            "[OrphanSessionCleanup] Removed {Count} orphan Visual QA sessions older than 24h.",
            sessionsToDelete.Count);
    }

    private static bool TryExtractSupabaseFilePointer(string imageUrl, out string bucket, out string filePath)
    {
        bucket = string.Empty;
        filePath = string.Empty;

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            return false;

        const string marker = "/storage/v1/object/public/";
        var path = uri.AbsolutePath;
        var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return false;

        var relative = path[(markerIndex + marker.Length)..].Trim('/');
        var slash = relative.IndexOf('/');
        if (slash <= 0 || slash == relative.Length - 1)
            return false;

        bucket = relative[..slash];
        filePath = relative[(slash + 1)..];
        return !string.IsNullOrWhiteSpace(bucket) && !string.IsNullOrWhiteSpace(filePath);
    }
}
