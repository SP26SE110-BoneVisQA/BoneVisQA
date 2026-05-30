using System.Collections.Concurrent;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Infrastructure;

public sealed class BoneSpecialtyCacheInvalidationInterceptor : SaveChangesInterceptor
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<BoneSpecialtyCacheInvalidationInterceptor> _logger;
    private readonly ConcurrentDictionary<Guid, byte> _pendingInvalidations = new();

    public BoneSpecialtyCacheInvalidationInterceptor(
        IMemoryCache cache,
        ILogger<BoneSpecialtyCacheInvalidationInterceptor> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        TrackIfBoneSpecialtyChanged(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        TrackIfBoneSpecialtyChanged(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        InvalidateIfPending(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        InvalidateIfPending(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ClearPending(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ClearPending(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void TrackIfBoneSpecialtyChanged(DbContext? context)
    {
        if (context == null)
            return;

        var shouldInvalidate = context.ChangeTracker
            .Entries<BoneSpecialty>()
            .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        if (!shouldInvalidate)
            return;

        _pendingInvalidations[context.ContextId.InstanceId] = 1;
    }

    private void InvalidateIfPending(DbContext? context)
    {
        if (context == null)
            return;

        if (!_pendingInvalidations.TryRemove(context.ContextId.InstanceId, out _))
            return;

        _cache.Remove(SpecialtyCacheKeys.AllSpecialties);
        _logger.LogInformation("Invalidated cached specialty catalog after specialty write.");
    }

    private void ClearPending(DbContext? context)
    {
        if (context == null)
            return;

        _pendingInvalidations.TryRemove(context.ContextId.InstanceId, out _);
    }
}
