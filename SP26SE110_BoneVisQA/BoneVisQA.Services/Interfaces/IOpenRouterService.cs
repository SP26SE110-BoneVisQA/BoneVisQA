using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Interfaces;

public interface IOpenRouterService
{
    Task<VisualQAResponseDto> GenerateDiagnosticAnswerAsync(
        string questionText,
        string? annotatedImageBase64,
        IReadOnlyList<DocumentChunk> contextChunks,
        string? coordinates,
        CancellationToken cancellationToken = default);
}
