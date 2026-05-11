using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.QuizExtensions;

public class AdaptiveQuizService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AdaptiveQuizService> _logger;

    public AdaptiveQuizService(IUnitOfWork unitOfWork, ILogger<AdaptiveQuizService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public enum DifficultyLevel
    {
        Easy,
        Medium,
        Hard
    }

    public class AdaptiveQuizPreview
    {
        public Guid QuizId { get; set; }
        public string QuizTitle { get; set; } = string.Empty;
        public bool IsAdaptive { get; set; }
        public string Difficulty { get; set; } = "Medium";
        public int TotalQuestions { get; set; }
        public Dictionary<string, int> QuestionsByDifficulty { get; set; } = new();
        public string EstimatedDifficulty { get; set; } = "Medium";
    }

    public class AdaptiveQuestionDto
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string CurrentDifficulty { get; set; } = "Medium";
        public string? ImageUrl { get; set; }
        public int Index { get; set; }
    }

    public async Task<AdaptiveQuizPreview?> GetQuizPreviewAsync(Guid quizId, Guid studentId)
    {
        var quiz = await _unitOfWork.QuizRepository
            .GetQueryable()
            .Include(q => q.QuizQuestions)
            .FirstOrDefaultAsync(q => q.Id == quizId);

        if (quiz == null || !quiz.AdaptiveDifficulty) return null;

        var estimatedDifficulty = await EstimateStudentDifficultyAsync(studentId, quiz.BoneSpecialtyId, quiz.PathologyCategoryId);

        return new AdaptiveQuizPreview
        {
            QuizId = quizId,
            QuizTitle = quiz.Title ?? "Unknown Quiz",
            IsAdaptive = true,
            Difficulty = estimatedDifficulty,
            TotalQuestions = quiz.QuizQuestions.Count,
            QuestionsByDifficulty = new Dictionary<string, int> { { "All", quiz.QuizQuestions.Count } },
            EstimatedDifficulty = estimatedDifficulty
        };
    }

    public async Task<string> EstimateStudentDifficultyAsync(Guid studentId, Guid? boneSpecialtyId, Guid? pathologyCategoryId)
    {
        if (!boneSpecialtyId.HasValue) return "Medium";

        var competency = await _unitOfWork.StudentCompetencyRepository
            .FirstOrDefaultAsync(c => c.StudentId == studentId && 
                        c.BoneSpecialtyId == boneSpecialtyId.Value);

        if (competency == null) return "Medium";

        if (competency.Score >= 70) return "Hard";
        if (competency.Score >= 40) return "Medium";
        return "Easy";
    }

    public async Task UpdateAttemptDifficultyAsync(Guid attemptId, string newDifficulty)
    {
        var attempt = await _unitOfWork.QuizAttemptRepository.GetByIdAsync(attemptId);
        if (attempt == null) return;

        attempt.DifficultyLevel = newDifficulty;
        _unitOfWork.QuizAttemptRepository.Update(attempt);
        await _unitOfWork.SaveAsync();
    }

    public async Task<List<AdaptiveQuestionDto>> GetNextQuestionsForAdaptiveQuizAsync(Guid attemptId, int count = 1)
    {
        var attempt = await _unitOfWork.QuizAttemptRepository
            .GetQueryable()
            .Include(a => a.Quiz)
                .ThenInclude(q => q!.QuizQuestions)
            .Include(a => a.StudentQuizAnswers)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt?.Quiz == null) return new List<AdaptiveQuestionDto>();

        var answeredIds = attempt.StudentQuizAnswers.Select(sa => sa.QuestionId).ToHashSet();
        var remainingQuestions = attempt.Quiz.QuizQuestions
            .Where(q => !answeredIds.Contains(q.Id))
            .ToList();

        var targetDifficulty = attempt.DifficultyLevel ?? "Medium";

        var difficultyOrdered = remainingQuestions
            .OrderBy(q => {
                // Simplified ordering - just return questions in order
                // In a real adaptive system, you'd use question metadata or AI
                return 0;
            })
            .Take(count)
            .ToList();

        return difficultyOrdered.Select((q, index) => new AdaptiveQuestionDto
        {
            QuestionId = q.Id,
            QuestionText = q.QuestionText,
            OptionA = q.OptionA,
            OptionB = q.OptionB,
            OptionC = q.OptionC,
            OptionD = q.OptionD,
            CurrentDifficulty = targetDifficulty,
            ImageUrl = q.ImageUrl,
            Index = attempt.StudentQuizAnswers.Count + index
        }).ToList();
    }

    public async Task AdjustDifficultyAfterAnswerAsync(Guid attemptId, bool wasCorrect)
    {
        var attempt = await _unitOfWork.QuizAttemptRepository.GetByIdAsync(attemptId);
        if (attempt == null) return;

        var currentLevel = attempt.DifficultyLevel ?? "Medium";
        var newLevel = currentLevel;

        if (currentLevel == "Easy")
        {
            newLevel = wasCorrect ? "Medium" : "Easy";
        }
        else if (currentLevel == "Medium")
        {
            newLevel = wasCorrect ? "Hard" : "Easy";
        }
        else if (currentLevel == "Hard")
        {
            newLevel = wasCorrect ? "Hard" : "Medium";
        }

        if (newLevel != currentLevel)
        {
            attempt.DifficultyLevel = newLevel;
            _unitOfWork.QuizAttemptRepository.Update(attempt);
            await _unitOfWork.SaveAsync();
        }
    }

    public async Task<bool> EnableAdaptiveModeAsync(Guid quizId)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId);
        if (quiz == null) return false;

        quiz.AdaptiveDifficulty = true;
        _unitOfWork.QuizRepository.Update(quiz);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task<bool> DisableAdaptiveModeAsync(Guid quizId)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId);
        if (quiz == null) return false;

        quiz.AdaptiveDifficulty = false;
        _unitOfWork.QuizRepository.Update(quiz);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task EnableSpacedRepetitionAsync(Guid quizId)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId);
        if (quiz == null) return;

        quiz.SpacedRepetitionEnabled = true;
        _unitOfWork.QuizRepository.Update(quiz);
        await _unitOfWork.SaveAsync();
    }
}
