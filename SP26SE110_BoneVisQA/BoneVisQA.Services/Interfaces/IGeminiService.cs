using BoneVisQA.Services.Models.VisualQA;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces;

/// <summary>Gemini REST client: non-streaming <c>generateContent</c> and SSE streaming <c>streamGenerateContent?alt=sse</c>.</summary>
public interface IGeminiService
{
    /// <summary>
    /// Generates a structured medical answer (language controlled by caller prompt) based on the given prompt and an optional image.
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

    /// <summary>Same preconditions as <see cref="GenerateMedicalAnswerAsync"/> when the API cannot be called (missing keys, model, base URL).</summary>
    VisualQAResponseDto? TryGetUnavailableFallbackResponse();

    VisualQAResponseDto ParseMedicalAnswerFromRawResponse(string rawJson);

    /// <summary>Yields incremental text fragments from Gemini <c>streamGenerateContent</c> (SSE); concatenation matches a single <c>generateContent</c> body text for JSON parsing.</summary>
    IAsyncEnumerable<string> StreamMedicalAnswerRawAsync(
        string prompt,
        string imageUrl,
        string? conversationHistory = null,
        bool ragContextAdequate = true,
        CancellationToken cancellationToken = default);
}
