using BoneVisQA.Services.Models.Student;

namespace BoneVisQA.Services.Interfaces;

public interface IFlashcardService
{
    // Deck operations
    Task<IReadOnlyList<FlashcardDeckDto>> GetDecksByStudentAsync(Guid studentId);
    Task<FlashcardDeckDto?> GetDeckByIdAsync(Guid deckId, Guid studentId);
    Task<FlashcardDeckDto> CreateDeckAsync(Guid studentId, CreateFlashcardDeckDto dto);
    Task<FlashcardDeckDto?> UpdateDeckAsync(Guid deckId, Guid studentId, UpdateFlashcardDeckDto dto);
    Task<bool> DeleteDeckAsync(Guid deckId, Guid studentId);

    // Flashcard operations
    Task<IReadOnlyList<FlashcardDto>> GetFlashcardsByDeckAsync(Guid deckId, Guid studentId);
    Task<FlashcardDto?> GetFlashcardByIdAsync(Guid flashcardId, Guid studentId);
    Task<FlashcardDto> CreateFlashcardAsync(Guid studentId, CreateFlashcardDto dto);
    Task<FlashcardDto?> UpdateFlashcardAsync(Guid flashcardId, Guid studentId, UpdateFlashcardDto dto);
    Task<bool> DeleteFlashcardAsync(Guid flashcardId, Guid studentId);

    // Study/Review operations
    Task<FlashcardStudySessionDto?> GetStudySessionAsync(Guid deckId, Guid studentId);
    Task<FlashcardReviewResultDto?> SubmitReviewAsync(Guid studentId, ReviewFlashcardDto dto);
    Task<FlashcardStatsDto> GetStatsAsync(Guid studentId);

    // Bookmark operations
    Task<FlashcardDto?> ToggleBookmarkAsync(Guid flashcardId, Guid studentId);
    Task<BookmarkedFlashcardsDto> GetBookmarkedCardsAsync(Guid studentId);
    Task<FlashcardStudySessionDto?> GetBookmarkedStudySessionAsync(Guid studentId);

    // Import operations
    Task<ImportFlashcardsResultDto> ImportFlashcardsAsync(Guid studentId, ImportFlashcardsDto dto);
}
