using BoneVisQA.Services.Models.Quiz;
using System.Threading;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces;

public interface IAIQuizService
{
    /// <summary>
    /// Generate quiz questions automatically from topic and case library.
    /// Used for Practice Quiz (Student) and AI Auto-Generate (Lecturer).
    /// </summary>
    Task<AIQuizGenerationResultDto> GenerateQuizQuestionsAsync(
        string topic,
        int questionCount = 5,
        string? difficulty = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggest questions from specific cases.
    /// Used for AI Suggest mode (Lecturer).
    /// </summary>
    Task<AIQuizGenerationResultDto> SuggestQuestionsFromCasesAsync(
        List<AIQuizCaseInputDto> cases,
        int questionsPerCase = 2,
        CancellationToken cancellationToken = default);
}
