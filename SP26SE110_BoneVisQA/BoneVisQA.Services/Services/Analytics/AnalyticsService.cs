using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.Analytics;

public class AnalyticsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(IUnitOfWork unitOfWork, ILogger<AnalyticsService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<List<StudentCompetency>> GetStudentCompetenciesAsync(Guid studentId)
    {
        return await _unitOfWork.StudentCompetencyRepository
            .GetQueryable()
            .Where(c => c.StudentId == studentId)
            .Include(c => c.BoneSpecialty)
            .Include(c => c.PathologyCategory)
            .ToListAsync();
    }

    public async Task<StudentCompetency?> GetStudentCompetencyAsync(Guid studentId, Guid boneSpecialtyId, Guid? pathologyCategoryId = null)
    {
        var query = _unitOfWork.StudentCompetencyRepository
            .GetQueryable()
            .Where(c => c.StudentId == studentId && c.BoneSpecialtyId == boneSpecialtyId);

        if (pathologyCategoryId.HasValue)
        {
            query = query.Where(c => c.PathologyCategoryId == pathologyCategoryId.Value);
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task UpdateStudentCompetencyAsync(Guid studentId, Guid boneSpecialtyId, Guid? pathologyCategoryId, decimal newScore)
    {
        var competency = await GetStudentCompetencyAsync(studentId, boneSpecialtyId, pathologyCategoryId);

        if (competency == null)
        {
            competency = new StudentCompetency
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                BoneSpecialtyId = boneSpecialtyId,
                PathologyCategoryId = pathologyCategoryId,
                Score = newScore,
                TotalAttempts = 1,
                CorrectAttempts = newScore >= 60 ? 1 : 0,
                MasteryLevel = CalculateMasteryLevel(newScore),
                LastAttemptAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _unitOfWork.StudentCompetencyRepository.Add(competency);
        }
        else
        {
            var isCorrect = newScore >= 60;
            var prevScore = competency.Score;
            
            competency.TotalAttempts += 1;
            if (isCorrect)
            {
                competency.CorrectAttempts += 1;
            }
            
            competency.Score = (prevScore * (competency.TotalAttempts - 1) + newScore) / competency.TotalAttempts;
            competency.LastAttemptAt = DateTime.UtcNow;
            competency.MasteryLevel = CalculateMasteryLevel(competency.Score);
            competency.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.StudentCompetencyRepository.Update(competency);
        }

        await _unitOfWork.SaveAsync();
    }

    private string CalculateMasteryLevel(decimal score)
    {
        if (score >= 80) return "Expert";
        if (score >= 60) return "Proficient";
        if (score >= 40) return "Intermediate";
        return "Beginner";
    }

    public async Task<List<ErrorPattern>> GetStudentErrorPatternsAsync(Guid studentId)
    {
        return await _unitOfWork.ErrorPatternRepository
            .GetQueryable()
            .Where(e => e.StudentId == studentId && !e.IsResolved)
            .OrderByDescending(e => e.ErrorCount)
            .ThenByDescending(e => e.LastOccurredAt)
            .ToListAsync();
    }

    public async Task<ErrorPattern?> DetectOrUpdateErrorPatternAsync(Guid studentId, string errorTopic, string questionPattern)
    {
        var existingPattern = await _unitOfWork.ErrorPatternRepository
            .GetQueryable()
            .Where(e => e.StudentId == studentId && 
                        (e.ErrorTopic == errorTopic || e.QuestionPattern == questionPattern))
            .FirstOrDefaultAsync();

        if (existingPattern != null)
        {
            existingPattern.ErrorCount += 1;
            existingPattern.LastOccurredAt = DateTime.UtcNow;
            _unitOfWork.ErrorPatternRepository.Update(existingPattern);
            await _unitOfWork.SaveAsync();
            return existingPattern;
        }

        var newPattern = new ErrorPattern
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ErrorTopic = errorTopic,
            QuestionPattern = questionPattern,
            ErrorCount = 1,
            FirstOccurredAt = DateTime.UtcNow,
            LastOccurredAt = DateTime.UtcNow,
            IsResolved = false,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.ErrorPatternRepository.Add(newPattern);
        await _unitOfWork.SaveAsync();
        return newPattern;
    }

    public async Task<bool> ResolveErrorPatternAsync(Guid errorPatternId)
    {
        var pattern = await _unitOfWork.ErrorPatternRepository.GetByIdAsync(errorPatternId);
        if (pattern == null) return false;

        pattern.IsResolved = true;
        pattern.ResolvedAt = DateTime.UtcNow;
        _unitOfWork.ErrorPatternRepository.Update(pattern);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task<List<LearningInsight>> GetStudentInsightsAsync(Guid studentId)
    {
        return await _unitOfWork.LearningInsightRepository
            .GetQueryable()
            .Where(i => i.StudentId == studentId)
            .Include(i => i.RelatedBoneSpecialty)
            .Include(i => i.RelatedPathology)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<LearningInsight> CreateInsightAsync(Guid studentId, string insightType, string title, string description, 
        decimal? confidence = 0.5m, Guid? boneSpecialtyId = null, Guid? pathologyId = null, string? recommendedAction = null)
    {
        var insight = new LearningInsight
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            InsightType = insightType,
            Title = title,
            Description = description,
            Confidence = confidence ?? 0.5m,
            RelatedBoneSpecialtyId = boneSpecialtyId,
            RelatedPathologyId = pathologyId,
            RecommendedAction = recommendedAction,
            IsRead = false,
            IsActionTaken = false,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.LearningInsightRepository.Add(insight);
        await _unitOfWork.SaveAsync();
        return insight;
    }

    public async Task<bool> MarkInsightAsReadAsync(Guid insightId)
    {
        var insight = await _unitOfWork.LearningInsightRepository.GetByIdAsync(insightId);
        if (insight == null) return false;

        insight.IsRead = true;
        _unitOfWork.LearningInsightRepository.Update(insight);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task<bool> MarkInsightAsActionTakenAsync(Guid insightId)
    {
        var insight = await _unitOfWork.LearningInsightRepository.GetByIdAsync(insightId);
        if (insight == null) return false;

        insight.IsActionTaken = true;
        _unitOfWork.LearningInsightRepository.Update(insight);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task AnalyzeQuizAttemptAndUpdateAnalyticsAsync(Guid attemptId)
    {
        var attempt = await _unitOfWork.QuizAttemptRepository
            .GetQueryable()
            .Include(a => a.Quiz)
            .Include(a => a.StudentQuizAnswers)
                .ThenInclude(sa => sa.Question)
                    .ThenInclude(q => q.Case)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt == null) return;

        foreach (var answer in attempt.StudentQuizAnswers)
        {
            if (answer.Question?.Case == null) continue;

            var boneSpecialtyId = answer.Question.Case.BoneSpecialtyId;
            var pathologyId = answer.Question.Case.PathologyCategoryId;

            if (!boneSpecialtyId.HasValue) continue;

            var scorePercent = answer.ScoreAwarded.HasValue ? (decimal)(answer.ScoreAwarded / 10 * 100) : (answer.IsCorrect == true ? 100 : 0);
            await UpdateStudentCompetencyAsync(attempt.StudentId, boneSpecialtyId.Value, pathologyId, scorePercent);

            if (!answer.IsCorrect.GetValueOrDefault())
            {
                var errorTopic = attempt.Quiz?.Topic ?? "General";
                var questionPattern = answer.Question.Case.Title ?? answer.Question.QuestionText;
                await DetectOrUpdateErrorPatternAsync(attempt.StudentId, errorTopic, questionPattern);
            }
        }

        await GenerateInsightsFromAnalyticsAsync(attempt.StudentId);
    }

    public async Task GenerateInsightsFromAnalyticsAsync(Guid studentId)
    {
        var competencies = await GetStudentCompetenciesAsync(studentId);
        var errorPatterns = await GetStudentErrorPatternsAsync(studentId);

        foreach (var competency in competencies.Where(c => c.Score < 50))
        {
            var existingInsight = await _unitOfWork.LearningInsightRepository
                .GetQueryable()
                .Where(i => i.StudentId == studentId && 
                            i.InsightType == "WeakTopic" &&
                            i.RelatedBoneSpecialtyId == competency.BoneSpecialtyId)
                .FirstOrDefaultAsync();

            if (existingInsight == null)
            {
                var topicName = competency.BoneSpecialty?.Name ?? "Unknown Topic";
                await CreateInsightAsync(
                    studentId,
                    "WeakTopic",
                    $"Weak in {topicName}",
                    $"Your score in {topicName} is {competency.Score:F1}%. Consider reviewing this topic more carefully.",
                    0.8m,
                    competency.BoneSpecialtyId,
                    competency.PathologyCategoryId,
                    $"Practice more questions in {topicName}"
                );
            }
        }

        foreach (var pattern in errorPatterns.Where(p => p.ErrorCount >= 3))
        {
            var existingInsight = await _unitOfWork.LearningInsightRepository
                .GetQueryable()
                .Where(i => i.StudentId == studentId &&
                            i.InsightType == "ErrorPattern" &&
                            i.Description!.Contains(pattern.ErrorTopic ?? ""))
                .FirstOrDefaultAsync();

            if (existingInsight == null)
            {
                await CreateInsightAsync(
                    studentId,
                    "ErrorPattern",
                    $"Repeated error in {pattern.ErrorTopic}",
                    $"You've made the same type of error {pattern.ErrorCount} times in {pattern.ErrorTopic}. Focus on this pattern to improve.",
                    0.7m,
                    null, null,
                    "Review similar cases and practice targeted questions"
                );
            }
        }
    }
}
