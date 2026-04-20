using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Interfaces.Expert;

public interface IExpertReviewService
{
    Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetCaseAnswersAsync(Guid expertId);
    Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetEscalatedAnswersAsync(Guid expertId);
    Task<ExpertEscalatedAnswerDto> ResolveEscalatedAnswerAsync(Guid expertId, Guid sessionId, ResolveEscalatedAnswerRequestDto request);
    Task<ExpertEscalatedAnswerDto> RespondToSessionAsync(Guid expertId, Guid sessionId, string content);
    Task ApproveSessionAsync(Guid expertId, Guid sessionId);
    Task<Guid> PromoteToLibraryAsync(Guid expertId, Guid sessionId, PromoteToLibraryRequestDto request);
    Task FlagChunkAsync(Guid expertId, Guid chunkId, FlagChunkRequestDto request);
    Task<ExpertVisualSessionDraftResponseDto> UpsertSessionReviewDraftAsync(Guid expertId, Guid sessionId, ExpertVisualSessionDraftRequestDto request);
    Task DeleteSessionReviewDraftAsync(Guid expertId, Guid sessionId);
}

public interface IExpertDashboardService
{
    Task<ExpertDashboardStatsDto> GetDashboardStatsAsync(Guid expertId);
    Task<IReadOnlyList<ExpertDashboardPendingReviewDto>> GetPendingReviewsAsync(Guid expertId);
    Task<IReadOnlyList<ExpertDashboardRecentCaseDto>> GetRecentCasesAsync(Guid expertId);
    Task<ExpertDashboardActivityDto> GetActivityAsync(Guid expertId);
}
