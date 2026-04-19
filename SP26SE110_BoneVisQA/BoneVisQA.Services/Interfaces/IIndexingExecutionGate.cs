namespace BoneVisQA.Services.Interfaces;

public interface IIndexingExecutionGate
{
    Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
