using BoneVisQA.Services.Models.Student;

namespace BoneVisQA.Services.Interfaces;

public interface IFlashcardGeneratorService
{
    Task<FlashcardGenerationResultDto> GenerateFromDocumentAsync(
        Guid documentId,
        Guid studentId,
        int cardCount = 10,
        CancellationToken cancellationToken = default);

    Task<FlashcardGenerationResultDto> GenerateFromChunksAsync(
        Guid studentId,
        IEnumerable<Guid> chunkIds,
        int cardCount = 10,
        CancellationToken cancellationToken = default);

    Task<FlashcardGenerationResultDto> GenerateFromTextAsync(
        Guid studentId,
        string sourceText,
        string? deckName = null,
        int cardCount = 10,
        CancellationToken cancellationToken = default);

    Task<FlashcardGenerationResultDto> GenerateFromCaseAsync(
        Guid caseId,
        Guid studentId,
        int cardCount = 10,
        CancellationToken cancellationToken = default);
}

public class FlashcardGenerationResultDto
{
    public bool Success { get; set; }
    public string? DeckName { get; set; }
    public Guid? DeckId { get; set; }
    public int GeneratedCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<FlashcardDto> GeneratedCards { get; set; } = Array.Empty<FlashcardDto>();
    public string? ErrorMessage { get; set; }
}
