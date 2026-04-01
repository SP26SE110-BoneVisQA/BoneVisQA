using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Interfaces.Expert;

public interface IExpertReviewService
{
    Task<IReadOnlyList<ExpertEscalatedAnswerDto>> GetEscalatedAnswersAsync(Guid expertId);
    Task<ExpertEscalatedAnswerDto> ResolveEscalatedAnswerAsync(Guid expertId, Guid answerId, ResolveEscalatedAnswerRequestDto request);
}
