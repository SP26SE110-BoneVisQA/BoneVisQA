using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Interfaces.Expert;

public interface IExpertReviewService
{
    Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetCaseAnswersAsync(Guid expertId);
    Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetEscalatedAnswersAsync(Guid expertId);
    Task<ExpertEscalatedAnswerDto> ResolveEscalatedAnswerAsync(Guid expertId, Guid answerId, ResolveEscalatedAnswerRequestDto request);
    Task FlagChunkAsync(Guid expertId, Guid chunkId, FlagChunkRequestDto request);
}

public interface IExpertDashboardService
{
    Task<ExpertDashboardStatsDto> GetDashboardStatsAsync(Guid expertId);
    Task<IReadOnlyList<ExpertDashboardPendingReviewDto>> GetPendingReviewsAsync(Guid expertId);
    Task<IReadOnlyList<ExpertDashboardRecentCaseDto>> GetRecentCasesAsync(Guid expertId);
    Task<ExpertDashboardActivityDto> GetActivityAsync(Guid expertId);
}
