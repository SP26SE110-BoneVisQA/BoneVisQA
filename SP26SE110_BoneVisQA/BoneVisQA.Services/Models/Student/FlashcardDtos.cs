using System;

namespace BoneVisQA.Services.Models.Student;

public class FlashcardDeckDto
{
    public Guid Id { get; set; }
    public string DeckName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CardCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateFlashcardDeckDto
{
    public string DeckName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateFlashcardDeckDto
{
    public string? DeckName { get; set; }
    public string? Description { get; set; }
}

public class FlashcardDto
{
    public Guid Id { get; set; }
    public Guid DeckId { get; set; }
    public string FrontContent { get; set; } = string.Empty;
    public string BackContent { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal EaseFactor { get; set; }
    public int IntervalDays { get; set; }
    public int RepetitionCount { get; set; }
    public DateOnly? NextReviewDate { get; set; }
    public DateTime? LastReviewDate { get; set; }
    public bool IsBookmarked { get; set; }
    public DateTime? BookmarkedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFlashcardDto
{
    public Guid DeckId { get; set; }
    public string FrontContent { get; set; } = string.Empty;
    public string BackContent { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public class UpdateFlashcardDto
{
    public string? FrontContent { get; set; }
    public string? BackContent { get; set; }
    public string? ImageUrl { get; set; }
}

public class ReviewFlashcardDto
{
    public Guid FlashcardId { get; set; }
    public int Quality { get; set; }
}

public class FlashcardReviewResultDto
{
    public Guid FlashcardId { get; set; }
    public DateOnly NextReviewDate { get; set; }
    public int IntervalDays { get; set; }
    public decimal EaseFactor { get; set; }
    public int RepetitionCount { get; set; }
}

public class FlashcardStudySessionDto
{
    public Guid DeckId { get; set; }
    public string DeckName { get; set; } = string.Empty;
    public int TotalCards { get; set; }
    public int CardsDueToday { get; set; }
    public int NewCards { get; set; }
    public int ReviewedCards { get; set; }
    public IReadOnlyList<FlashcardDto> CardsToReview { get; set; } = Array.Empty<FlashcardDto>();
}

public class FlashcardStatsDto
{
    public int TotalDecks { get; set; }
    public int TotalCards { get; set; }
    public int CardsDueToday { get; set; }
    public int CardsStudiedToday { get; set; }
    public int NewCards { get; set; }
    public double AverageEaseFactor { get; set; }
}

// ===== Bookmark DTOs =====

public class BookmarkFlashcardDto
{
    public Guid FlashcardId { get; set; }
}

public class BookmarkedFlashcardsDto
{
    public IReadOnlyList<FlashcardDto> BookmarkedCards { get; set; } = Array.Empty<FlashcardDto>();
    public int TotalBookmarked { get; set; }
}

// ===== Import DTOs =====

public class ImportFlashcardsDto
{
    public Guid DeckId { get; set; }
    public List<ImportFlashcardItemDto> Cards { get; set; } = new();
}

public class ImportFlashcardItemDto
{
    public string FrontContent { get; set; } = string.Empty;
    public string BackContent { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public class ImportFlashcardsResultDto
{
    public bool Success { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<FlashcardDto> ImportedCards { get; set; } = new();
}
