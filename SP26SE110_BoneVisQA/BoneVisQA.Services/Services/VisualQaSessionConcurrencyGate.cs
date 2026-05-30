using System.Collections.Concurrent;

namespace BoneVisQA.Services.Services;

/// <summary>Per-session mutex so concurrent <c>ask-json</c> calls cannot bypass the turn limit (multi-instance safe when backed by Postgres advisory locks).</summary>
public interface IVisualQaSessionConcurrencyGate
{
    Task<IDisposable> AcquireAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

/// <summary>In-process fallback for local dev/tests when <c>VisualQa:UseInMemorySessionGate</c> is true.</summary>
public sealed class InMemoryVisualQaSessionConcurrencyGate : IVisualQaSessionConcurrencyGate
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var sem = _locks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new ReleaseHandle(sem);
    }

    private sealed class ReleaseHandle(SemaphoreSlim sem) : IDisposable
    {
        public void Dispose() => sem.Release();
    }
}
