using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

/// <summary>
/// AI-powered flashcard recommendation service that analyzes student performance
/// and suggests which cards to review, prioritize, or create new cards about.
/// </summary>
public interface IFlashcardRecommendationService
{
    /// <summary>
    /// Get AI-powered recommendations for a student's flashcard study plan.
    /// </summary>
    Task<FlashcardRecommendationResultDto> GetRecommendationsAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate study tips for a specific flashcard based on the student's history.
    /// </summary>
    Task<string?> GetCardStudyTipAsync(
        Guid studentId,
        Guid cardId,
        CancellationToken cancellationToken = default);
}

public class FlashcardRecommendationResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Cards that need immediate review (overdue or low retention).
    /// </summary>
    public List<CardRecommendationDto> UrgentReviewCards { get; set; } = new();

    /// <summary>
    /// Cards recommended for today's study session.
    /// </summary>
    public List<CardRecommendationDto> RecommendedStudyCards { get; set; } = new();

    /// <summary>
    /// Weak areas identified from student's performance.
    /// </summary>
    public List<string> WeakAreas { get; set; } = new();

    /// <summary>
    /// AI-generated study tips for the student.
    /// </summary>
    public string? StudyTips { get; set; }

    /// <summary>
    /// Suggested new topics to create flashcards about.
    /// </summary>
    public List<string> SuggestedTopics { get; set; } = new();

    /// <summary>
    /// Overall mastery score (0-100).
    /// </summary>
    public int MasteryScore { get; set; }
}

public class CardRecommendationDto
{
    public Guid CardId { get; set; }
    public Guid DeckId { get; set; }
    public string DeckName { get; set; } = string.Empty;
    public string FrontContent { get; set; } = string.Empty;
    public string BackContent { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Priority level: high, medium, low
    /// </summary>
    public string Priority { get; set; } = "medium";

    /// <summary>
    /// Reason for recommendation.
    /// </summary>
    public string RecommendationReason { get; set; } = string.Empty;

    /// <summary>
    /// How many times the card has been reviewed.
    /// </summary>
    public int ReviewCount { get; set; }

    /// <summary>
    /// Ease factor from SM2 algorithm.
    /// </summary>
    public double EaseFactor { get; set; }

    /// <summary>
    /// Last review date.
    /// </summary>
    public DateTime? LastReviewDate { get; set; }

    /// <summary>
    /// Next scheduled review date.
    /// </summary>
    public DateOnly? NextReviewDate { get; set; }
}

public class FlashcardRecommendationService : IFlashcardRecommendationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<FlashcardRecommendationService> _logger;

    public FlashcardRecommendationService(
        IUnitOfWork unitOfWork,
        IGeminiService geminiService,
        ILogger<FlashcardRecommendationService> logger)
    {
        _unitOfWork = unitOfWork;
        _geminiService = geminiService;
        _logger = logger;
    }

    public async Task<FlashcardRecommendationResultDto> GetRecommendationsAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Get all decks for this student
            var deckIds = await _unitOfWork.Context.FlashcardDecks
                .Where(d => d.StudentId == studentId)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            if (deckIds.Count == 0)
            {
                return new FlashcardRecommendationResultDto
                {
                    Success = false,
                    ErrorMessage = "No flashcard decks found."
                };
            }

            // Get all flashcards with their study history
            var allCards = await _unitOfWork.Context.Flashcards
                .Include(f => f.Deck)
                .Where(f => deckIds.Contains(f.DeckId))
                .ToListAsync(cancellationToken);

            if (allCards.Count == 0)
            {
                return new FlashcardRecommendationResultDto
                {
                    Success = false,
                    ErrorMessage = "No flashcards found."
                };
            }

            // Categorize cards
            var urgentReviewCards = new List<CardRecommendationDto>();
            var recommendedStudyCards = new List<CardRecommendationDto>();
            var weakAreas = new List<string>();
            var cardAnalysis = new List<(Repositories.Models.Flashcard card, string category, string reason, string priority)>();

            foreach (var card in allCards)
            {
                var recommendation = AnalyzeCardPriority(card, today);
                cardAnalysis.Add((card, recommendation.category, recommendation.reason, recommendation.priority));

                var dto = MapToDto(card);

                if (recommendation.category == "urgent")
                {
                    urgentReviewCards.Add(dto);
                }
                else if (recommendation.category == "recommended")
                {
                    recommendedStudyCards.Add(dto);
                }

                // Identify weak areas based on low ease factor
                if (card.EaseFactor < 1.5m && card.RepetitionCount > 0)
                {
                    var area = ExtractTopicFromCard(card);
                    if (!string.IsNullOrEmpty(area) && !weakAreas.Contains(area))
                    {
                        weakAreas.Add(area);
                    }
                }
            }

            // Calculate mastery score
            var masteryScore = CalculateMasteryScore(allCards);

            // Generate AI study tips and topic suggestions
            var (studyTips, suggestedTopics) = await GenerateStudyInsightsAsync(
                allCards, cardAnalysis, cancellationToken);

            // Sort cards by priority
            urgentReviewCards = urgentReviewCards
                .OrderBy(c => c.Priority == "high" ? 0 : 1)
                .ThenBy(c => c.LastReviewDate)
                .Take(10)
                .ToList();

            recommendedStudyCards = recommendedStudyCards
                .OrderByDescending(c => c.Priority == "high" ? 2 : c.Priority == "medium" ? 1 : 0)
                .ThenBy(c => c.LastReviewDate)
                .Take(15)
                .ToList();

            return new FlashcardRecommendationResultDto
            {
                Success = true,
                UrgentReviewCards = urgentReviewCards,
                RecommendedStudyCards = recommendedStudyCards,
                WeakAreas = weakAreas.Take(5).ToList(),
                StudyTips = studyTips,
                SuggestedTopics = suggestedTopics,
                MasteryScore = masteryScore
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flashcard recommendations for student {StudentId}", studentId);
            return new FlashcardRecommendationResultDto
            {
                Success = false,
                ErrorMessage = "An error occurred while generating recommendations."
            };
        }
    }

    public async Task<string?> GetCardStudyTipAsync(
        Guid studentId,
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var card = await _unitOfWork.Context.Flashcards
                .Include(f => f.Deck)
                .FirstOrDefaultAsync(
                    f => f.Id == cardId && f.Deck!.StudentId == studentId,
                    cancellationToken);

            if (card == null)
                return null;

            // Get recent review history for this card
            var recentPerformance = card.RepetitionCount > 0
                ? $"Reviewed {card.RepetitionCount} times. Last review: {card.LastReviewDate?.ToString("yyyy-MM-dd") ?? "Never"}. Ease factor: {card.EaseFactor:F2}."
                : "This is a new card.";

            var prompt = $@"You are a medical education tutor helping a student study bone anatomy and pathology flashcards.

Card information:
- Deck: {card.Deck?.DeckName ?? "Unknown"}
- Question: {card.FrontContent}
- Answer: {card.BackContent}
- Performance: {recentPerformance}

Based on the card content and student's performance, provide a concise study tip (2-3 sentences max) to help the student remember this concept better. Focus on:
- Memory techniques
- Key points to focus on
- Common mistakes to avoid

Format: Return ONLY the study tip in Vietnamese.";

            var response = await _geminiService.GenerateMedicalAnswerAsync(
                prompt,
                string.Empty,
                null,
                false,
                cancellationToken: cancellationToken);

            // Extract the diagnosis field which contains our text
            return response?.SuggestedDiagnosis ?? response?.AnswerText;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting study tip for card {CardId}", cardId);
            return null;
        }
    }

    private (string category, string reason, string priority) AnalyzeCardPriority(
        Repositories.Models.Flashcard card,
        DateOnly today)
    {
        // New card - needs first review
        if (card.LastReviewDate == null)
        {
            return ("urgent", "New card - hasn't been reviewed yet", "high");
        }

        // Overdue card
        if (card.NextReviewDate.HasValue && card.NextReviewDate.Value < today)
        {
            var daysOverdue = today.DayNumber - card.NextReviewDate.Value.DayNumber;
            if (daysOverdue > 7)
                return ("urgent", $"Overdue by {daysOverdue} days", "high");
            return ("urgent", $"Overdue by {daysOverdue} days", "medium");
        }

        // Low retention cards (low ease factor)
        if (card.EaseFactor < 1.5m && card.RepetitionCount > 0)
        {
            return ("recommended", "Low retention - needs more practice", "high");
        }

        // Medium difficulty retention
        if (card.EaseFactor < 2.0m && card.RepetitionCount > 0)
        {
            return ("recommended", "Medium retention - review recommended", "medium");
        }

        // Due today
        if (card.NextReviewDate.HasValue && card.NextReviewDate.Value == today)
        {
            return ("recommended", "Due for review today", "medium");
        }

        // Not yet due but reviewed before
        if (card.RepetitionCount > 0 && card.EaseFactor >= 2.0m)
        {
            return ("recommended", "Good retention - occasional review helps", "low");
        }

        return ("recommended", "Regular review", "low");
    }

    private string ExtractTopicFromCard(Repositories.Models.Flashcard card)
    {
        // Extract potential topic from card content
        var content = $"{card.FrontContent} {card.BackContent}".ToLower();

        // Simple keyword extraction
        var keywords = new[] {
            "fracture", "bone", "joint", "spine", "skull", "pelvis",
            "tumor", "infection", "arthritis", "osteoporosis", "tendon",
            "ligament", "muscle", "cartilage", "disc", "vertebra"
        };

        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword))
            {
                return char.ToUpper(keyword[0]) + keyword.Substring(1);
            }
        }

        // Return first few words as topic
        var words = card.FrontContent.Split(' ').Take(3);
        return string.Join(" ", words);
    }

    private int CalculateMasteryScore(List<Repositories.Models.Flashcard> cards)
    {
        if (cards.Count == 0) return 0;

        var totalScore = 0.0;
        var reviewedCards = 0;

        foreach (var card in cards)
        {
            if (card.RepetitionCount > 0)
            {
                reviewedCards++;
                // Normalize ease factor to 0-100 scale (2.5m ease = 100%, 1.3m ease = 0%)
                var normalizedScore = ((double)card.EaseFactor - 1.3) / (2.5 - 1.3) * 100;
                normalizedScore = Math.Max(0, Math.Min(100, normalizedScore));
                totalScore += normalizedScore;
            }
        }

        if (reviewedCards == 0) return 0;

        return (int)Math.Round(totalScore / reviewedCards);
    }

    private async Task<(string? studyTips, List<string> suggestedTopics)> GenerateStudyInsightsAsync(
        List<Repositories.Models.Flashcard> cards,
        List<(Repositories.Models.Flashcard card, string category, string reason, string priority)> analysis,
        CancellationToken cancellationToken)
    {
        try
        {
            var cardsNeedingWork = analysis
                .Where(a => a.category == "urgent" || (a.priority == "high" && a.category == "recommended"))
                .Take(5)
                .ToList();

            if (cardsNeedingWork.Count == 0)
            {
                return ("Great job! Your flashcard mastery is excellent. Keep up the regular reviews to maintain your knowledge.",
                    new List<string>());
            }

            var cardsSummary = string.Join("\n", cardsNeedingWork.Select(c =>
                $"- {c.card.FrontContent} ({c.reason})"));

            var allTopics = cards
                .Select(c => c.Deck?.DeckName ?? "General")
                .Distinct()
                .Take(5)
                .ToList();

            var prompt = $@"You are a medical education AI tutor analyzing a student's flashcard study data.

Current study data:
- Total cards: {cards.Count}
- Cards needing urgent review: {analysis.Count(a => a.category == "urgent")}
- High priority cards: {analysis.Count(a => a.priority == "high")}

Cards that need attention:
{cardsSummary}

Topics being studied:
{string.Join(", ", allTopics)}

Provide two things:
1. Study tips (in Vietnamese, 2-3 sentences) - motivational and practical advice
2. Suggested new topics to create flashcards about (in English, comma-separated, max 5 topics)

Return response as JSON with fields: studyTips, suggestedTopics.";

            var response = await _geminiService.GenerateMedicalAnswerAsync(
                prompt,
                string.Empty,
                null,
                false,
                cancellationToken: cancellationToken);

            // Parse response
            var text = response?.SuggestedDiagnosis ?? response?.AnswerText ?? string.Empty;

            // Extract JSON from response
            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = text.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var studyTips = root.TryGetProperty("studyTips", out var tips)
                    ? tips.GetString()
                    : null;

                var suggestedTopics = new List<string>();
                if (root.TryGetProperty("suggestedTopics", out var topics) &&
                    topics.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var topic in topics.EnumerateArray())
                    {
                        var topicStr = topic.GetString();
                        if (!string.IsNullOrEmpty(topicStr))
                            suggestedTopics.Add(topicStr);
                    }
                }

                return (studyTips, suggestedTopics);
            }

            return (null, new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generating study insights");
            return (null, new List<string>());
        }
    }

    private CardRecommendationDto MapToDto(Repositories.Models.Flashcard card)
    {
        return new CardRecommendationDto
        {
            CardId = card.Id,
            DeckId = card.DeckId,
            DeckName = card.Deck?.DeckName ?? string.Empty,
            FrontContent = card.FrontContent,
            BackContent = card.BackContent,
            ImageUrl = card.ImageUrl,
            ReviewCount = card.RepetitionCount,
            EaseFactor = (double)card.EaseFactor,
            LastReviewDate = card.LastReviewDate,
            NextReviewDate = card.NextReviewDate
        };
    }
}
