using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Interfaces;

public interface IVisualQaAiService
{
    Task<VisualQAResponseDto> RunPipelineAsync(VisualQARequestDto request, CancellationToken cancellationToken = default);
}
