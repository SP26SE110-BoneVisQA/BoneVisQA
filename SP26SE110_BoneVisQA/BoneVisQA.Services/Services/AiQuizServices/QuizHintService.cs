using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.AiQuizServices;

/// <summary>
/// AI-powered hint system that generates contextual hints for quiz questions.
/// </summary>
public interface IQuizHintService
{
    /// <summary>
    /// Generate an AI hint for a specific quiz question.
    /// </summary>
    Task<QuizHintResultDto> GetHintAsync(
        Guid questionId,
        Guid? attemptId,
        int hintLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a hint from the pre-generated hint (database).
    /// </summary>
    Task<string?> GetPreGeneratedHintAsync(
        Guid questionId,
        int hintLevel,
        CancellationToken cancellationToken = default);
}

public class QuizHintResultDto
{
    public bool Success { get; set; }
    public string? Hint { get; set; }
    public int HintLevel { get; set; }
    public bool IsFromAi { get; set; }
    public string? ErrorMessage { get; set; }
}

public class QuizHintService : IQuizHintService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<QuizHintService> _logger;

    // Hint level determines how specific the hint is
    private const int MaxHintLevels = 3;

    public QuizHintService(
        IUnitOfWork unitOfWork,
        IGeminiService geminiService,
        ILogger<QuizHintService> logger)
    {
        _unitOfWork = unitOfWork;
        _geminiService = geminiService;
        _logger = logger;
    }

    public async Task<QuizHintResultDto> GetHintAsync(
        Guid questionId,
        Guid? attemptId,
        int hintLevel,
        CancellationToken cancellationToken = default)
    {
        hintLevel = Math.Clamp(hintLevel, 1, MaxHintLevels);

        try
        {
            // First try to get pre-generated hint
            var preGeneratedHint = await GetPreGeneratedHintAsync(questionId, hintLevel, cancellationToken);
            if (!string.IsNullOrEmpty(preGeneratedHint))
            {
                return new QuizHintResultDto
                {
                    Success = true,
                    Hint = preGeneratedHint,
                    HintLevel = hintLevel,
                    IsFromAi = false
                };
            }

            // Generate AI hint if no pre-generated hint exists
            return await GenerateAiHintAsync(questionId, attemptId, hintLevel, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hint for question {QuestionId}", questionId);
            return new QuizHintResultDto
            {
                Success = false,
                ErrorMessage = "Failed to generate hint.",
                HintLevel = hintLevel
            };
        }
    }

    public async Task<string?> GetPreGeneratedHintAsync(
        Guid questionId,
        int hintLevel,
        CancellationToken cancellationToken = default)
    {
        // Try to get the hint field from quiz question
        var question = await _unitOfWork.Context.QuizQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);

        if (question == null)
            return null;

        // Return hint based on level (using the same hint field for level 1)
        // In a real scenario, you'd have HintLevel1, HintLevel2, HintLevel3 fields
        return hintLevel switch
        {
            1 => question.Hint,
            _ => question.Hint // For higher levels, return the same hint with more context (or null)
        };
    }

    private async Task<QuizHintResultDto> GenerateAiHintAsync(
        Guid questionId,
        Guid? attemptId,
        int hintLevel,
        CancellationToken cancellationToken)
    {
        // Get question details
        var question = await _unitOfWork.Context.QuizQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);

        if (question == null)
        {
            return new QuizHintResultDto
            {
                Success = false,
                ErrorMessage = "Question not found.",
                HintLevel = hintLevel
            };
        }

        // Build context for hint generation
        var context = BuildHintContext(question, attemptId);

        // Generate hint based on level
        var hint = hintLevel switch
        {
            1 => await GenerateLevel1HintAsync(question, context, cancellationToken),
            2 => await GenerateLevel2HintAsync(question, context, cancellationToken),
            3 => await GenerateLevel3HintAsync(question, context, cancellationToken),
            _ => await GenerateLevel1HintAsync(question, context, cancellationToken)
        };

        return new QuizHintResultDto
        {
            Success = true,
            Hint = hint,
            HintLevel = hintLevel,
            IsFromAi = true
        };
    }

    private string BuildHintContext(QuizQuestion question, Guid? attemptId)
    {
        var sb = new System.Text.StringBuilder();

        if (attemptId.HasValue)
        {
            sb.AppendLine("Context: Student is taking a quiz attempt.");
        }

        sb.AppendLine("Topic: General Medical Knowledge");
        return sb.ToString();
    }

    private async Task<string> GenerateLevel1HintAsync(
        QuizQuestion question,
        string context,
        CancellationToken cancellationToken)
    {
        // Level 1: General hint - points to the topic area
        var prompt = $@"You are a medical education tutor. Generate a gentle hint for a quiz question.

Question: {question.QuestionText}
Topic: Medical knowledge

{hintLevel1SystemPrompt}

Return ONLY the hint text (max 50 words) in Vietnamese.";

        return await CallGeminiForHintAsync(prompt, cancellationToken);
    }

    private async Task<string> GenerateLevel2HintAsync(
        QuizQuestion question,
        string context,
        CancellationToken cancellationToken)
    {
        // Level 2: More specific hint - mentions key concept
        var prompt = $@"You are a medical education tutor. Generate a more specific hint for a quiz question.

Question: {question.QuestionText}
Topic: Medical knowledge
Options:
A: {question.OptionA}
B: {question.OptionB}
C: {question.OptionC}
D: {question.OptionD}

{hintLevel2SystemPrompt}

Return ONLY the hint text (max 60 words) in Vietnamese.";

        return await CallGeminiForHintAsync(prompt, cancellationToken);
    }

    private async Task<string> GenerateLevel3HintAsync(
        QuizQuestion question,
        string context,
        CancellationToken cancellationToken)
    {
        // Level 3: Strong hint - eliminates some wrong answers
        var prompt = $@"You are a medical education tutor. Generate a strong hint that helps narrow down the answer.

Question: {question.QuestionText}
Topic: Medical knowledge
Correct Answer: {question.CorrectAnswer}
Options:
A: {question.OptionA}
B: {question.OptionB}
C: {question.OptionC}
D: {question.OptionD}

{hintLevel3SystemPrompt}

Return ONLY the hint text (max 70 words) in Vietnamese.";

        return await CallGeminiForHintAsync(prompt, cancellationToken);
    }

    private async Task<string> CallGeminiForHintAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _geminiService.GenerateMedicalAnswerAsync(
                prompt,
                string.Empty,
                null,
                false,
                cancellationToken);

            var hint = response?.SuggestedDiagnosis ?? response?.AnswerText ?? string.Empty;

            // Clean up the response - remove JSON if any
            var jsonStart = hint.IndexOf('{');
            if (jsonStart >= 0)
            {
                hint = hint.Substring(0, jsonStart).Trim();
            }

            return string.IsNullOrEmpty(hint)
                ? "Hint không khả dụng. Hãy thử xem lại tài liệu về chủ đề này."
                : hint;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calling Gemini for hint");
            return "Hint không khả dụng lúc này. Hãy thử xem lại tài liệu.";
        }
    }

    private const string hintLevel1SystemPrompt = @"Rules:
- Give a GENERAL hint about the topic area
- Do NOT reveal the answer
- Help student recall relevant knowledge without giving away the solution
- Focus on the general concept or category
- Be encouraging and supportive in tone";

    private const string hintLevel2SystemPrompt = @"Rules:
- Give a MORE SPECIFIC hint about key concepts
- Point toward important terms or relationships
- Do NOT reveal the answer directly
- Help student connect concepts
- Can reference the topic's key features";

    private const string hintLevel3SystemPrompt = @"Rules:
- Give a STRONG hint that narrows down possibilities
- CAN mention that certain options are incorrect (without saying which)
- Guide student toward the correct approach
- Still DO NOT say the exact answer
- Help eliminate obviously wrong choices";
}
