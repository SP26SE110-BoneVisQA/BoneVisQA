using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

public class FlashcardGeneratorService : IFlashcardGeneratorService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<FlashcardGeneratorService> _logger;

    public FlashcardGeneratorService(
        IUnitOfWork unitOfWork,
        IGeminiService geminiService,
        ILogger<FlashcardGeneratorService> logger)
    {
        _unitOfWork = unitOfWork;
        _geminiService = geminiService;
        _logger = logger;
    }

    public async Task<FlashcardGenerationResultDto> GenerateFromDocumentAsync(
        Guid documentId,
        Guid studentId,
        int cardCount = 10,
        CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            return new FlashcardGenerationResultDto
            {
                Success = false,
                ErrorMessage = "Document not found."
            };
        }

        var chunks = await _unitOfWork.Context.DocumentChunks
            .Where(c => c.DocId == documentId && !c.IsFlagged)
            .OrderBy(c => c.ChunkOrder)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            return new FlashcardGenerationResultDto
            {
                Success = false,
                ErrorMessage = "No valid chunks found in document."
            };
        }

        var combinedContent = string.Join("\n\n", chunks.Select(c => c.Content));
        var deckName = $"Flashcards from {document.Title}";

        return await GenerateFromTextAsync(studentId, combinedContent, deckName, cardCount, cancellationToken);
    }

    public async Task<FlashcardGenerationResultDto> GenerateFromChunksAsync(
        Guid studentId,
        IEnumerable<Guid> chunkIds,
        int cardCount = 10,
        CancellationToken cancellationToken = default)
    {
        var chunks = await _unitOfWork.Context.DocumentChunks
            .Where(c => chunkIds.Contains(c.Id) && !c.IsFlagged)
            .OrderBy(c => c.ChunkOrder)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            return new FlashcardGenerationResultDto
            {
                Success = false,
                ErrorMessage = "No valid chunks found."
            };
        }

        var combinedContent = string.Join("\n\n", chunks.Select(c => c.Content));
        var deckName = $"Flashcards from {chunks.Count} chunk(s)";

        return await GenerateFromTextAsync(studentId, combinedContent, deckName, cardCount, cancellationToken);
    }

    public async Task<FlashcardGenerationResultDto> GenerateFromCaseAsync(
        Guid caseId,
        Guid studentId,
        int cardCount = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var medicalCase = await _unitOfWork.Context.MedicalCases
                .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

            if (medicalCase == null)
            {
                return new FlashcardGenerationResultDto
                {
                    Success = false,
                    ErrorMessage = "Case not found."
                };
            }

            var studentQuestions = await _unitOfWork.Context.StudentQuestions
                .Where(q => q.CaseId == caseId)
                .Include(q => q.CaseAnswers)
                .ToListAsync(cancellationToken);

            var contentBuilder = new System.Text.StringBuilder();

            contentBuilder.AppendLine("=== MEDICAL CASE INFORMATION ===");
            contentBuilder.AppendLine($"Title: {medicalCase.Title}");
            contentBuilder.AppendLine($"Description: {medicalCase.Description}");
            if (!string.IsNullOrWhiteSpace(medicalCase.SuggestedDiagnosis))
                contentBuilder.AppendLine($"Suggested Diagnosis: {medicalCase.SuggestedDiagnosis}");
            if (!string.IsNullOrWhiteSpace(medicalCase.Difficulty))
                contentBuilder.AppendLine($"Difficulty: {medicalCase.Difficulty}");

            if (studentQuestions.Count > 0)
            {
                contentBuilder.AppendLine("\n=== Q&A DISCUSSION (EXPERT ANSWERS) ===");

                foreach (var question in studentQuestions)
                {
                    contentBuilder.AppendLine($"\nQ: {question.QuestionText}");

                    var expertAnswers = question.CaseAnswers
                        .Where(a => !string.IsNullOrWhiteSpace(a.AnswerText) && a.Status == "Completed")
                        .ToList();

                    foreach (var answer in expertAnswers)
                    {
                        if (!string.IsNullOrWhiteSpace(answer.AnswerText))
                        {
                            contentBuilder.AppendLine($"Expert Answer: {answer.AnswerText}");
                        }

                        if (!string.IsNullOrWhiteSpace(answer.KeyImagingFindings))
                        {
                            contentBuilder.AppendLine($"Key Imaging Findings: {answer.KeyImagingFindings}");
                        }

                        if (!string.IsNullOrWhiteSpace(answer.StructuredDiagnosis))
                        {
                            contentBuilder.AppendLine($"Structured Diagnosis: {answer.StructuredDiagnosis}");
                        }

                        if (!string.IsNullOrWhiteSpace(answer.DifferentialDiagnoses))
                        {
                            contentBuilder.AppendLine($"Differential Diagnoses: {answer.DifferentialDiagnoses}");
                        }
                    }
                }
            }
            else
            {
                contentBuilder.AppendLine("\n(No Q&A discussions available for this case)");
            }

            var sourceContent = contentBuilder.ToString();
            var deckName = $"Case Study: {medicalCase.Title}";

            return await GenerateFromTextAsync(studentId, sourceContent, deckName, cardCount, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating flashcards from case {CaseId}", caseId);
            return new FlashcardGenerationResultDto
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<FlashcardGenerationResultDto> GenerateFromTextAsync(
        Guid studentId,
        string sourceText,
        string? deckName = null,
        int cardCount = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var finalDeckName = deckName ?? $"Flashcards - {DateTime.UtcNow:yyyy-MM-dd}";
            var isFromCase = deckName?.StartsWith("Case Study:") == true;
            var description = isFromCase
                ? $"Auto-generated from Medical Case. Created at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC"
                : $"Auto-generated from document. Created at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";

            var deck = new FlashcardDeck
            {
                Id = Guid.NewGuid(),
                DeckName = finalDeckName,
                Description = description,
                StudentId = studentId,
                CardCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _unitOfWork.Context.FlashcardDecks.Add(deck);

            var prompt = BuildFlashcardPrompt(sourceText, cardCount);

            var response = await _geminiService.GenerateMedicalAnswerAsync(
                prompt,
                "",
                null,
                false,
                cancellationToken: cancellationToken);

            var generatedCards = ParseGeneratedFlashcards(response?.AnswerText ?? "", deck.Id, studentId);

            if (generatedCards.Count == 0)
            {
                return new FlashcardGenerationResultDto
                {
                    Success = false,
                    DeckName = finalDeckName,
                    ErrorMessage = "Failed to generate flashcards. Please try again."
                };
            }

            var initialValues = SM2Algorithm.GetInitialValues();

            foreach (var card in generatedCards)
            {
                _unitOfWork.Context.Flashcards.Add(new Flashcard
                {
                    Id = Guid.NewGuid(),
                    DeckId = deck.Id,
                    FrontContent = card.FrontContent,
                    BackContent = card.BackContent,
                    ImageUrl = card.ImageUrl,
                    EaseFactor = initialValues.EaseFactor,
                    IntervalDays = initialValues.IntervalDays,
                    RepetitionCount = initialValues.RepetitionCount,
                    NextReviewDate = initialValues.NextReviewDate,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            deck.CardCount = generatedCards.Count;

            await _unitOfWork.SaveAsync();

            var resultCards = generatedCards.Select(c => new FlashcardDto
            {
                Id = Guid.NewGuid(),
                DeckId = deck.Id,
                FrontContent = c.FrontContent,
                BackContent = c.BackContent,
                ImageUrl = c.ImageUrl,
                EaseFactor = initialValues.EaseFactor,
                IntervalDays = initialValues.IntervalDays,
                RepetitionCount = initialValues.RepetitionCount,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            return new FlashcardGenerationResultDto
            {
                Success = true,
                DeckName = finalDeckName,
                DeckId = deck.Id,
                GeneratedCount = generatedCards.Count,
                GeneratedCards = resultCards
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating flashcards from text");
            return new FlashcardGenerationResultDto
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }

    private string BuildFlashcardPrompt(string sourceText, int cardCount)
    {
        return $@"You are a medical education assistant. Based on the following content, generate {cardCount} flashcards (Q&A cards) for bone anatomy and pathology learning.

Each flashcard should follow this format (JSON array):
[
  {{""front"": ""Question in Vietnamese"", ""back"": ""Answer in Vietnamese""}}
]

Rules:
- Questions should be clear, specific, and test understanding (not just recall)
- Answers should be concise but complete
- Cover different aspects: anatomy, pathology, clinical features, diagnosis, treatment
- Maximum {cardCount} cards
- Only return valid JSON array, no other text

Content to generate flashcards from:
{sourceText}";
    }

    private List<(string FrontContent, string BackContent, string? ImageUrl)> ParseGeneratedFlashcards(
        string aiResponse,
        Guid deckId,
        Guid studentId)
    {
        var cards = new List<(string FrontContent, string BackContent, string? ImageUrl)>();

        try
        {
            aiResponse = aiResponse.Trim();

            int jsonStart = aiResponse.IndexOf('[');
            int jsonEnd = aiResponse.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonArray = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<List<FlashcardJsonItem>>(jsonArray);

                if (parsed != null)
                {
                    foreach (var item in parsed)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Front) && !string.IsNullOrWhiteSpace(item.Back))
                        {
                            cards.Add((item.Front, item.Back, null));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI flashcard response, attempting line-by-line parsing");
            cards.AddRange(ParseFlashcardsLineByLine(aiResponse));
        }

        return cards;
    }

    private List<(string FrontContent, string BackContent, string? ImageUrl)> ParseFlashcardsLineByLine(string text)
    {
        var cards = new List<(string FrontContent, string BackContent, string? ImageUrl)>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string? currentFront = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("```"))
                continue;

            if (trimmed.StartsWith("\"front\"") || trimmed.StartsWith("front:") || trimmed.StartsWith("Q:"))
            {
                currentFront = ExtractValue(trimmed);
            }
            else if ((trimmed.StartsWith("\"back\"") || trimmed.StartsWith("back:") || trimmed.StartsWith("A:")) && currentFront != null)
            {
                var back = ExtractValue(trimmed);
                if (!string.IsNullOrWhiteSpace(back))
                {
                    cards.Add((currentFront, back, null));
                    currentFront = null;
                }
            }
        }

        return cards;
    }

    private string ExtractValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0) return line;

        var value = line.Substring(colonIndex + 1).Trim();
        value = value.Trim('"', ' ', ',', '-', '\r', '\n');

        return value;
    }

    private class FlashcardJsonItem
    {
        public string Front { get; set; } = "";
        public string Back { get; set; } = "";
    }
}
