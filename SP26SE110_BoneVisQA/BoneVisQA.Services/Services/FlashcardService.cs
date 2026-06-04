using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

public class FlashcardService : IFlashcardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FlashcardService> _logger;

    public FlashcardService(IUnitOfWork unitOfWork, ILogger<FlashcardService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    #region Deck Operations

    public async Task<IReadOnlyList<FlashcardDeckDto>> GetDecksByStudentAsync(Guid studentId)
    {
        var decks = await _unitOfWork.Context.FlashcardDecks
            .Where(d => d.StudentId == studentId)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new FlashcardDeckDto
            {
                Id = d.Id,
                DeckName = d.DeckName,
                Description = d.Description,
                CardCount = d.CardCount,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();

        return decks;
    }

    public async Task<FlashcardDeckDto?> GetDeckByIdAsync(Guid deckId, Guid studentId)
    {
        var deck = await _unitOfWork.Context.FlashcardDecks
            .Where(d => d.Id == deckId && d.StudentId == studentId)
            .Select(d => new FlashcardDeckDto
            {
                Id = d.Id,
                DeckName = d.DeckName,
                Description = d.Description,
                CardCount = d.CardCount,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .FirstOrDefaultAsync();

        return deck;
    }

    public async Task<FlashcardDeckDto> CreateDeckAsync(Guid studentId, CreateFlashcardDeckDto dto)
    {
        var deck = new FlashcardDeck
        {
            Id = Guid.NewGuid(),
            DeckName = dto.DeckName,
            Description = dto.Description,
            StudentId = studentId,
            CardCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _unitOfWork.Context.FlashcardDecks.Add(deck);
        await _unitOfWork.SaveAsync();

        return new FlashcardDeckDto
        {
            Id = deck.Id,
            DeckName = deck.DeckName,
            Description = deck.Description,
            CardCount = deck.CardCount,
            CreatedAt = deck.CreatedAt,
            UpdatedAt = deck.UpdatedAt
        };
    }

    public async Task<FlashcardDeckDto?> UpdateDeckAsync(Guid deckId, Guid studentId, UpdateFlashcardDeckDto dto)
    {
        var deck = await _unitOfWork.Context.FlashcardDecks
            .FirstOrDefaultAsync(d => d.Id == deckId && d.StudentId == studentId);

        if (deck == null) return null;

        if (!string.IsNullOrEmpty(dto.DeckName))
            deck.DeckName = dto.DeckName;

        if (dto.Description != null)
            deck.Description = dto.Description;

        deck.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveAsync();

        return new FlashcardDeckDto
        {
            Id = deck.Id,
            DeckName = deck.DeckName,
            Description = deck.Description,
            CardCount = deck.CardCount,
            CreatedAt = deck.CreatedAt,
            UpdatedAt = deck.UpdatedAt
        };
    }

    public async Task<bool> DeleteDeckAsync(Guid deckId, Guid studentId)
    {
        var deck = await _unitOfWork.Context.FlashcardDecks
            .FirstOrDefaultAsync(d => d.Id == deckId && d.StudentId == studentId);

        if (deck == null) return false;

        _unitOfWork.Context.FlashcardDecks.Remove(deck);
        await _unitOfWork.SaveAsync();

        return true;
    }

    #endregion

    #region Flashcard Operations

    public async Task<IReadOnlyList<FlashcardDto>> GetFlashcardsByDeckAsync(Guid deckId, Guid studentId)
    {
        var deck = await _unitOfWork.Context.FlashcardDecks
            .Where(d => d.Id == deckId && d.StudentId == studentId)
            .Select(d => new { d.Id })
            .FirstOrDefaultAsync();

        if (deck == null)
            return new List<FlashcardDto>();

        var flashcards = await _unitOfWork.Context.Flashcards
            .Where(f => f.DeckId == deckId)
            .OrderBy(f => f.CreatedAt)
            .Select(f => new FlashcardDto
            {
                Id = f.Id,
                DeckId = f.DeckId,
                FrontContent = f.FrontContent,
                BackContent = f.BackContent,
                ImageUrl = f.ImageUrl,
                EaseFactor = f.EaseFactor,
                IntervalDays = f.IntervalDays,
                RepetitionCount = f.RepetitionCount,
                NextReviewDate = f.NextReviewDate,
                LastReviewDate = f.LastReviewDate,
                IsBookmarked = f.IsBookmarked,
                BookmarkedAt = f.BookmarkedAt,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync();

        return flashcards;
    }

    public async Task<FlashcardDto?> GetFlashcardByIdAsync(Guid flashcardId, Guid studentId)
    {
        var flashcard = await _unitOfWork.Context.Flashcards
            .Where(f => f.Id == flashcardId)
            .Where(f => _unitOfWork.Context.FlashcardDecks
                .Any(d => d.Id == f.DeckId && d.StudentId == studentId))
            .Select(f => new FlashcardDto
            {
                Id = f.Id,
                DeckId = f.DeckId,
                FrontContent = f.FrontContent,
                BackContent = f.BackContent,
                ImageUrl = f.ImageUrl,
                EaseFactor = f.EaseFactor,
                IntervalDays = f.IntervalDays,
                RepetitionCount = f.RepetitionCount,
                NextReviewDate = f.NextReviewDate,
                LastReviewDate = f.LastReviewDate,
                IsBookmarked = f.IsBookmarked,
                BookmarkedAt = f.BookmarkedAt,
                CreatedAt = f.CreatedAt
            })
            .FirstOrDefaultAsync();

        return flashcard;
    }

    public async Task<FlashcardDto> CreateFlashcardAsync(Guid studentId, CreateFlashcardDto dto)
    {
        var deck = await _unitOfWork.Context.FlashcardDecks
            .FirstOrDefaultAsync(d => d.Id == dto.DeckId && d.StudentId == studentId);

        if (deck == null)
            throw new InvalidOperationException("Deck not found or access denied.");

        var initialValues = SM2Algorithm.GetInitialValues();

        var flashcard = new Flashcard
        {
            Id = Guid.NewGuid(),
            DeckId = dto.DeckId,
            FrontContent = dto.FrontContent,
            BackContent = dto.BackContent,
            ImageUrl = dto.ImageUrl,
            EaseFactor = initialValues.EaseFactor,
            IntervalDays = initialValues.IntervalDays,
            RepetitionCount = initialValues.RepetitionCount,
            NextReviewDate = initialValues.NextReviewDate,
            IsBookmarked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _unitOfWork.Context.Flashcards.Add(flashcard);

        deck.CardCount++;
        deck.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveAsync();

        return new FlashcardDto
        {
            Id = flashcard.Id,
            DeckId = flashcard.DeckId,
            FrontContent = flashcard.FrontContent,
            BackContent = flashcard.BackContent,
            ImageUrl = flashcard.ImageUrl,
            EaseFactor = flashcard.EaseFactor,
            IntervalDays = flashcard.IntervalDays,
            RepetitionCount = flashcard.RepetitionCount,
            NextReviewDate = flashcard.NextReviewDate,
            LastReviewDate = flashcard.LastReviewDate,
            IsBookmarked = flashcard.IsBookmarked,
            BookmarkedAt = flashcard.BookmarkedAt,
            CreatedAt = flashcard.CreatedAt
        };
    }

    public async Task<FlashcardDto?> UpdateFlashcardAsync(Guid flashcardId, Guid studentId, UpdateFlashcardDto dto)
    {
        var flashcard = await _unitOfWork.Context.Flashcards
            .Include(f => f.Deck)
            .FirstOrDefaultAsync(f => f.Id == flashcardId && f.Deck!.StudentId == studentId);

        if (flashcard == null) return null;

        if (!string.IsNullOrEmpty(dto.FrontContent))
            flashcard.FrontContent = dto.FrontContent;

        if (!string.IsNullOrEmpty(dto.BackContent))
            flashcard.BackContent = dto.BackContent;

        if (dto.ImageUrl != null)
            flashcard.ImageUrl = dto.ImageUrl;

        flashcard.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveAsync();

        return new FlashcardDto
        {
            Id = flashcard.Id,
            DeckId = flashcard.DeckId,
            FrontContent = flashcard.FrontContent,
            BackContent = flashcard.BackContent,
            ImageUrl = flashcard.ImageUrl,
            EaseFactor = flashcard.EaseFactor,
            IntervalDays = flashcard.IntervalDays,
            RepetitionCount = flashcard.RepetitionCount,
            NextReviewDate = flashcard.NextReviewDate,
            LastReviewDate = flashcard.LastReviewDate,
            IsBookmarked = flashcard.IsBookmarked,
            BookmarkedAt = flashcard.BookmarkedAt,
            CreatedAt = flashcard.CreatedAt
        };
    }

    public async Task<bool> DeleteFlashcardAsync(Guid flashcardId, Guid studentId)
    {
        var flashcard = await _unitOfWork.Context.Flashcards
            .Include(f => f.Deck)
            .FirstOrDefaultAsync(f => f.Id == flashcardId && f.Deck!.StudentId == studentId);

        if (flashcard == null) return false;

        var deck = flashcard.Deck;
        _unitOfWork.Context.Flashcards.Remove(flashcard);

        if (deck != null)
        {
            deck.CardCount = Math.Max(0, deck.CardCount - 1);
            deck.UpdatedAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveAsync();

        return true;
    }

    #endregion

    #region Study/Review Operations

    public async Task<FlashcardStudySessionDto?> GetStudySessionAsync(Guid deckId, Guid studentId)
    {
        var deck = await _unitOfWork.Context.FlashcardDecks
            .FirstOrDefaultAsync(d => d.Id == deckId && d.StudentId == studentId);

        if (deck == null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var cardsToReview = await _unitOfWork.Context.Flashcards
            .Include(f => f.Deck)
            .Where(f => f.DeckId == deckId && f.Deck!.StudentId == studentId)
            .Where(f => f.NextReviewDate == null || f.NextReviewDate <= today)
            .OrderBy(f => f.NextReviewDate)
            .ThenBy(f => f.CreatedAt)
            .Select(f => new FlashcardDto
            {
                Id = f.Id,
                DeckId = f.DeckId,
                FrontContent = f.FrontContent,
                BackContent = f.BackContent,
                ImageUrl = f.ImageUrl,
                EaseFactor = f.EaseFactor,
                IntervalDays = f.IntervalDays,
                RepetitionCount = f.RepetitionCount,
                NextReviewDate = f.NextReviewDate,
                LastReviewDate = f.LastReviewDate,
                IsBookmarked = f.IsBookmarked,
                BookmarkedAt = f.BookmarkedAt,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync();

        var newCards = cardsToReview.Count(f => f.LastReviewDate == null);
        var cardsDueToday = cardsToReview.Count;

        return new FlashcardStudySessionDto
        {
            DeckId = deckId,
            DeckName = deck.DeckName,
            TotalCards = deck.CardCount,
            CardsDueToday = cardsDueToday,
            NewCards = newCards,
            ReviewedCards = deck.CardCount - newCards - cardsDueToday,
            CardsToReview = cardsToReview
        };
    }

    public async Task<FlashcardReviewResultDto?> SubmitReviewAsync(Guid studentId, ReviewFlashcardDto dto)
    {
        var flashcard = await _unitOfWork.Context.Flashcards
            .Include(f => f.Deck)
            .FirstOrDefaultAsync(f => f.Id == dto.FlashcardId && f.Deck!.StudentId == studentId);

        if (flashcard == null) return null;

        var result = SM2Algorithm.Calculate(
            flashcard.EaseFactor,
            flashcard.IntervalDays,
            flashcard.RepetitionCount,
            dto.Quality);

        flashcard.EaseFactor = result.EaseFactor;
        flashcard.IntervalDays = result.IntervalDays;
        flashcard.RepetitionCount = result.RepetitionCount;
        flashcard.NextReviewDate = result.NextReviewDate;
        flashcard.LastReviewDate = DateTime.UtcNow;
        flashcard.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveAsync();

        return new FlashcardReviewResultDto
        {
            FlashcardId = flashcard.Id,
            NextReviewDate = result.NextReviewDate,
            IntervalDays = result.IntervalDays,
            EaseFactor = result.EaseFactor,
            RepetitionCount = result.RepetitionCount
        };
    }

    public async Task<FlashcardStatsDto> GetStatsAsync(Guid studentId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var deckIds = await _unitOfWork.Context.FlashcardDecks
            .Where(d => d.StudentId == studentId)
            .Select(d => d.Id)
            .ToListAsync();

        var deckCount = deckIds.Count;

        var stats = await _unitOfWork.Context.Flashcards
            .Where(f => deckIds.Contains(f.DeckId))
            .GroupBy(f => 1)
            .Select(g => new
            {
                TotalCards = g.Count(),
                CardsDueToday = g.Count(f => f.NextReviewDate == null || f.NextReviewDate <= today),
                CardsStudiedToday = g.Count(f => f.LastReviewDate != null && f.LastReviewDate.Value.Date == DateTime.UtcNow.Date),
                NewCards = g.Count(f => f.LastReviewDate == null),
                AvgEaseFactor = g.Where(f => f.LastReviewDate != null).Average(f => (double?)f.EaseFactor) ?? (double)SM2Algorithm.Defaults.InitialEaseFactor
            })
            .FirstOrDefaultAsync();

        return new FlashcardStatsDto
        {
            TotalDecks = deckCount,
            TotalCards = stats?.TotalCards ?? 0,
            CardsDueToday = stats?.CardsDueToday ?? 0,
            CardsStudiedToday = stats?.CardsStudiedToday ?? 0,
            NewCards = stats?.NewCards ?? 0,
            AverageEaseFactor = stats?.AvgEaseFactor ?? (double)SM2Algorithm.Defaults.InitialEaseFactor
        };
    }

    #endregion

    #region Bookmark Operations

    public async Task<FlashcardDto?> ToggleBookmarkAsync(Guid flashcardId, Guid studentId)
    {
        var flashcard = await _unitOfWork.Context.Flashcards
            .Include(f => f.Deck)
            .FirstOrDefaultAsync(f => f.Id == flashcardId && f.Deck!.StudentId == studentId);

        if (flashcard == null) return null;

        flashcard.IsBookmarked = !flashcard.IsBookmarked;
        flashcard.BookmarkedAt = flashcard.IsBookmarked ? DateTime.UtcNow : null;
        flashcard.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveAsync();

        return new FlashcardDto
        {
            Id = flashcard.Id,
            DeckId = flashcard.DeckId,
            FrontContent = flashcard.FrontContent,
            BackContent = flashcard.BackContent,
            ImageUrl = flashcard.ImageUrl,
            EaseFactor = flashcard.EaseFactor,
            IntervalDays = flashcard.IntervalDays,
            RepetitionCount = flashcard.RepetitionCount,
            NextReviewDate = flashcard.NextReviewDate,
            LastReviewDate = flashcard.LastReviewDate,
            IsBookmarked = flashcard.IsBookmarked,
            BookmarkedAt = flashcard.BookmarkedAt,
            CreatedAt = flashcard.CreatedAt
        };
    }

    public async Task<BookmarkedFlashcardsDto> GetBookmarkedCardsAsync(Guid studentId)
    {
        var bookmarkedCards = await _unitOfWork.Context.Flashcards
            .Include(f => f.Deck)
            .Where(f => f.Deck!.StudentId == studentId && f.IsBookmarked)
            .OrderByDescending(f => f.BookmarkedAt)
            .Select(f => new FlashcardDto
            {
                Id = f.Id,
                DeckId = f.DeckId,
                FrontContent = f.FrontContent,
                BackContent = f.BackContent,
                ImageUrl = f.ImageUrl,
                EaseFactor = f.EaseFactor,
                IntervalDays = f.IntervalDays,
                RepetitionCount = f.RepetitionCount,
                NextReviewDate = f.NextReviewDate,
                LastReviewDate = f.LastReviewDate,
                IsBookmarked = f.IsBookmarked,
                BookmarkedAt = f.BookmarkedAt,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync();

        return new BookmarkedFlashcardsDto
        {
            BookmarkedCards = bookmarkedCards,
            TotalBookmarked = bookmarkedCards.Count
        };
    }

    public async Task<FlashcardStudySessionDto?> GetBookmarkedStudySessionAsync(Guid studentId)
    {
        var bookmarkedCards = await _unitOfWork.Context.Flashcards
            .Include(f => f.Deck)
            .Where(f => f.Deck!.StudentId == studentId && f.IsBookmarked)
            .OrderByDescending(f => f.BookmarkedAt)
            .Select(f => new FlashcardDto
            {
                Id = f.Id,
                DeckId = f.DeckId,
                FrontContent = f.FrontContent,
                BackContent = f.BackContent,
                ImageUrl = f.ImageUrl,
                EaseFactor = f.EaseFactor,
                IntervalDays = f.IntervalDays,
                RepetitionCount = f.RepetitionCount,
                NextReviewDate = f.NextReviewDate,
                LastReviewDate = f.LastReviewDate,
                IsBookmarked = f.IsBookmarked,
                BookmarkedAt = f.BookmarkedAt,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync();

        if (bookmarkedCards.Count == 0) return null;

        return new FlashcardStudySessionDto
        {
            DeckId = Guid.Empty,
            DeckName = "Bookmarked Cards",
            TotalCards = bookmarkedCards.Count,
            CardsDueToday = bookmarkedCards.Count,
            NewCards = 0,
            ReviewedCards = 0,
            CardsToReview = bookmarkedCards
        };
    }

    #endregion

    #region Import Operations

    public async Task<ImportFlashcardsResultDto> ImportFlashcardsAsync(Guid studentId, ImportFlashcardsDto dto)
    {
        var result = new ImportFlashcardsResultDto
        {
            Success = true,
            ImportedCount = 0,
            FailedCount = 0,
            Errors = new List<string>(),
            ImportedCards = new List<FlashcardDto>()
        };

        var deck = await _unitOfWork.Context.FlashcardDecks
            .FirstOrDefaultAsync(d => d.Id == dto.DeckId && d.StudentId == studentId);

        if (deck == null)
        {
            result.Success = false;
            result.Errors.Add("Deck not found or access denied.");
            return result;
        }

        var initialValues = SM2Algorithm.GetInitialValues();

        foreach (var item in dto.Cards)
        {
            if (string.IsNullOrWhiteSpace(item.FrontContent) || string.IsNullOrWhiteSpace(item.BackContent))
            {
                result.FailedCount++;
                result.Errors.Add($"Skipped card with empty front or back content");
                continue;
            }

            try
            {
                var flashcard = new Flashcard
                {
                    Id = Guid.NewGuid(),
                    DeckId = dto.DeckId,
                    FrontContent = item.FrontContent.Trim(),
                    BackContent = item.BackContent.Trim(),
                    ImageUrl = item.ImageUrl,
                    EaseFactor = initialValues.EaseFactor,
                    IntervalDays = initialValues.IntervalDays,
                    RepetitionCount = initialValues.RepetitionCount,
                    NextReviewDate = initialValues.NextReviewDate,
                    IsBookmarked = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _unitOfWork.Context.Flashcards.Add(flashcard);

                var flashcardDto = new FlashcardDto
                {
                    Id = flashcard.Id,
                    DeckId = flashcard.DeckId,
                    FrontContent = flashcard.FrontContent,
                    BackContent = flashcard.BackContent,
                    ImageUrl = flashcard.ImageUrl,
                    EaseFactor = flashcard.EaseFactor,
                    IntervalDays = flashcard.IntervalDays,
                    RepetitionCount = flashcard.RepetitionCount,
                    NextReviewDate = flashcard.NextReviewDate,
                    LastReviewDate = flashcard.LastReviewDate,
                    IsBookmarked = flashcard.IsBookmarked,
                    BookmarkedAt = flashcard.BookmarkedAt,
                    CreatedAt = flashcard.CreatedAt
                };

                result.ImportedCards.Add(flashcardDto);
                result.ImportedCount++;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Errors.Add($"Failed to import card: {ex.Message}");
            }
        }

        if (result.ImportedCount > 0)
        {
            deck.CardCount += result.ImportedCount;
            deck.UpdatedAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveAsync();

        result.Success = result.FailedCount == 0;
        return result;
    }

    #endregion
}
