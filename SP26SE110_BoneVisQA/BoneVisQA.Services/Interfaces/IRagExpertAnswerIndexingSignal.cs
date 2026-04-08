namespace BoneVisQA.Services.Interfaces;

/// <summary>
/// Hook for future RAG "data flywheel": expert-approved Q&amp;A pairs can be embedded into the knowledge base.
/// Default implementation is a no-op logger.
/// </summary>
public interface IRagExpertAnswerIndexingSignal
{
    Task NotifyExpertApprovedForFutureIndexingAsync(Guid answerId, CancellationToken cancellationToken = default);
}
