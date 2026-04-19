using BoneVisQA.Services.Models.VisualQA;
using System.Threading;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces;

public interface IGeminiService
{
    /// <summary>
    /// Generates a structured medical answer in Vietnamese based on the given prompt and an optional image.
    /// The image payload can be either:
    /// - a Base64 JPEG string (no data prefix), or
    /// - an HTTP(S) URL to an image.
    /// </summary>
    Task<VisualQAResponseDto> GenerateMedicalAnswerAsync(
        string prompt,
        string imageUrl,
        string? conversationHistory = null,
        bool ragContextAdequate = true,
        CancellationToken cancellationToken = default);
}
