using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Interfaces;

public interface IVisualQaAiService
{
    Task<VisualQAResponseDto> RunPipelineAsync(VisualQARequestDto request, CancellationToken cancellationToken = default);

    Task<VisualQaStreamingPipelineResult> RunStreamingPipelineAsync(VisualQARequestDto request, CancellationToken cancellationToken = default);
}
