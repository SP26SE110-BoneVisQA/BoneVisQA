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

    /// <summary>
    /// Get available cases that can be used for AI quiz generation.
    /// Used for student to select cases for practice quiz.
    /// </summary>
    Task<List<AIQuizCaseInputDto>> GetAvailableCasesAsync(
        string? topic = null,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate quiz from specific cases selected by student.
    /// Used for AI Practice Quiz (Student) with Case selection.
    /// </summary>
    Task<AIQuizGenerationResultDto> GenerateQuizFromCasesAsync(
        List<AIQuizCaseInputDto> cases,
        int questionCount = 5,
        string? difficulty = null,
        CancellationToken cancellationToken = default);
}
