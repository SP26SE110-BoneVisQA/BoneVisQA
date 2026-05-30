using System.Data;
using System.Data.Common;
using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

/// <summary>
/// Cross-instance mutex via PostgreSQL session-level advisory locks (<c>pg_advisory_lock</c>).
/// Works with Supabase/Postgres without Redis; lock is held on a dedicated connection until disposed.
/// </summary>
public sealed class PostgresVisualQaSessionConcurrencyGate : IVisualQaSessionConcurrencyGate
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PostgresVisualQaSessionConcurrencyGate> _logger;
    private readonly TimeSpan _acquireTimeout;

    public PostgresVisualQaSessionConcurrencyGate(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PostgresVisualQaSessionConcurrencyGate> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = configuration.GetValue("VisualQa:SessionLockAcquireTimeoutSeconds", 45);
        _acquireTimeout = TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 120));
    }

    public async Task<IDisposable> AcquireAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BoneVisQADbContext>();
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        var lockKey = SessionAdvisoryLockKey.ToInt64(sessionId);
        var acquired = await TryAcquireAdvisoryLockAsync(context, lockKey, cancellationToken);
        if (!acquired)
        {
            scope.Dispose();
            throw new InvalidOperationException(
                "Another question is being processed for this Visual QA session. Please wait and try again.");
        }

        _logger.LogDebug("Acquired pg advisory lock {LockKey} for session {SessionId}", lockKey, sessionId);
        return new AdvisoryLockRelease(context, scope, lockKey, sessionId, _logger);
    }

    private async Task<bool> TryAcquireAdvisoryLockAsync(
        BoneVisQADbContext context,
        long lockKey,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + _acquireTimeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await TryPgTryAdvisoryLockAsync(context, lockKey, cancellationToken))
                return true;

            if (DateTime.UtcNow >= deadline)
                return false;

            await Task.Delay(80, cancellationToken);
        }
    }

    private static async Task<bool> TryPgTryAdvisoryLockAsync(
        BoneVisQADbContext context,
        long lockKey,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@p0);";
        AddLockKeyParameter(command, lockKey);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool granted && granted;
    }

    private static async Task ReleaseAdvisoryLockAsync(
        BoneVisQADbContext context,
        long lockKey,
        CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(@p0);";
        AddLockKeyParameter(command, lockKey);

        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static void AddLockKeyParameter(DbCommand command, long lockKey)
    {
        var param = command.CreateParameter();
        param.ParameterName = "@p0";
        param.Value = lockKey;
        command.Parameters.Add(param);
    }

    private sealed class AdvisoryLockRelease : IDisposable
    {
        private readonly BoneVisQADbContext _context;
        private readonly IServiceScope _scope;
        private readonly long _lockKey;
        private readonly Guid _sessionId;
        private readonly ILogger _logger;
        private bool _disposed;

        public AdvisoryLockRelease(
            BoneVisQADbContext context,
            IServiceScope scope,
            long lockKey,
            Guid sessionId,
            ILogger logger)
        {
            _context = context;
            _scope = scope;
            _lockKey = lockKey;
            _sessionId = sessionId;
            _logger = logger;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                ReleaseAdvisoryLockAsync(_context, _lockKey).GetAwaiter().GetResult();
                _logger.LogDebug("Released pg advisory lock {LockKey} for session {SessionId}", _lockKey, _sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release pg advisory lock {LockKey} for session {SessionId}", _lockKey, _sessionId);
            }
            finally
            {
                _scope.Dispose();
            }
        }
    }
}
