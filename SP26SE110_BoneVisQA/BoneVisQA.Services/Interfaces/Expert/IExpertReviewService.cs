using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Interfaces.Expert;

public interface IExpertReviewService
{
    Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetCaseAnswersAsync(Guid expertId, Guid? specialtyId = null, string? status = null);
    Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetEscalatedAnswersAsync(Guid expertId, Guid? specialtyId = null, string? status = null);

    /// <summary>Single-session payload (aligned with queue items; citations merged across assistant turns).</summary>
    Task<ExpertEscalatedAnswerDto> GetEscalatedSessionDetailAsync(Guid expertId, Guid sessionId);
    Task<ExpertEscalatedAnswerDto> ResolveEscalatedAnswerAsync(Guid expertId, Guid sessionId, ResolveEscalatedAnswerRequestDto request);
    Task<ExpertEscalatedAnswerDto> RespondToSessionAsync(Guid expertId, Guid sessionId, string content);
    Task ApproveSessionAsync(Guid expertId, Guid sessionId);
    Task<PromoteToLibraryResponseDto> PromoteToLibraryAsync(Guid expertId, Guid sessionId, PromoteToLibraryRequestDto request);
    Task<PromoteToLibraryResponseDto> ApproveAndPromoteToLibraryAsync(
        Guid expertId,
        Guid sessionId,
        ApproveAndPromoteToLibraryRequestDto request);
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
