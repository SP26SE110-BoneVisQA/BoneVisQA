using BoneVisQA.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.Rag;

public sealed class NoOpRagExpertAnswerIndexingSignal : IRagExpertAnswerIndexingSignal
{
    private readonly ILogger<NoOpRagExpertAnswerIndexingSignal> _logger;

    public NoOpRagExpertAnswerIndexingSignal(ILogger<NoOpRagExpertAnswerIndexingSignal> logger)
    {
        _logger = logger;
    }

    public Task NotifyExpertApprovedForFutureIndexingAsync(Guid answerId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "RAG flywheel: expert-approved answer {AnswerId} marked as candidate for future vector indexing (not implemented).",
            answerId);
        return Task.CompletedTask;
    }
}
