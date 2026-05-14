using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BoneVisQA.Services.Services.QuizExtensions;

public class QuizReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<QuizReviewService> _logger;

    public QuizReviewService(IUnitOfWork unitOfWork, ILogger<QuizReviewService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public class DetailedReviewDto
    {
        public Guid AttemptId { get; set; }
        public string QuizTitle { get; set; } = string.Empty;
        public double? Score { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public List<QuestionReviewDto> Questions { get; set; } = new();
    }

    public class QuestionReviewDto
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public bool? IsCorrect { get; set; }
        public string? AiExplanation { get; set; }
        public List<string> TopicTags { get; set; } = new();
        public List<RelatedCaseDto> RelatedCases { get; set; } = new();
    }

    public class RelatedCaseDto
    {
        public Guid CaseId { get; set; }
        public string CaseTitle { get; set; } = string.Empty;
        public string? BoneSpecialty { get; set; }
    }

    public async Task<DetailedReviewDto?> GetDetailedReviewAsync(Guid attemptId)
    {
        var attempt = await _unitOfWork.QuizAttemptRepository
            .GetQueryable()
            .Include(a => a.Quiz)
            .Include(a => a.StudentQuizAnswers)
                .ThenInclude(sa => sa.Question)
                    .ThenInclude(q => q!.Case)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt == null) return null;

        var reviewItems = await _unitOfWork.QuizReviewItemRepository
            .FindAsync(r => r.AttemptId == attemptId);

        var questionReviews = new List<QuestionReviewDto>();

        foreach (var answer in attempt.StudentQuizAnswers)
        {
            var question = answer.Question;
            if (question == null) continue;

            var reviewItem = reviewItems.FirstOrDefault(r => r.QuestionId == question.Id);

            var relatedCases = new List<RelatedCaseDto>();
            if (reviewItem != null && !string.IsNullOrEmpty(reviewItem.RelatedCases) && reviewItem.RelatedCases != "[]")
            {
                var caseIds = JsonConvert.DeserializeObject<List<Guid>>(reviewItem.RelatedCases) ?? new List<Guid>();
                var cases = await _unitOfWork.MedicalCaseRepository
                    .GetQueryable()
                    .Where(c => caseIds.Contains(c.Id))
                    .Include(c => c.BoneSpecialty)
                    .ToListAsync();

                relatedCases = cases.Select(c => new RelatedCaseDto
                {
                    CaseId = c.Id,
                    CaseTitle = c.Title ?? "Unknown Case",
                    BoneSpecialty = c.BoneSpecialty?.Name
                }).ToList();
            }

            var topicTags = reviewItem?.TopicTagList ?? new List<string>();

            questionReviews.Add(new QuestionReviewDto
            {
                QuestionId = question.Id,
                QuestionText = question.QuestionText,
                StudentAnswer = answer.StudentAnswer ?? answer.EssayAnswer,
                CorrectAnswer = question.CorrectAnswer,
                IsCorrect = answer.IsCorrect,
                AiExplanation = reviewItem?.AiExplanation,
                TopicTags = topicTags,
                RelatedCases = relatedCases
            });
        }

        return new DetailedReviewDto
        {
            AttemptId = attemptId,
            QuizTitle = attempt.Quiz?.Title ?? "Unknown Quiz",
            Score = attempt.Score,
            CompletedAt = attempt.CompletedAt,
            TotalQuestions = attempt.StudentQuizAnswers.Count,
            CorrectAnswers = attempt.StudentQuizAnswers.Count(a => a.IsCorrect == true),
            Questions = questionReviews
        };
    }

    public async Task GenerateReviewItemsAsync(Guid attemptId, string? aiExplanations = null)
    {
        var attempt = await _unitOfWork.QuizAttemptRepository
            .GetQueryable()
            .Include(a => a.StudentQuizAnswers)
                .ThenInclude(sa => sa.Question)
                    .ThenInclude(q => q!.Case)
                        .ThenInclude(c => c!.BoneSpecialty)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt == null) return;

        foreach (var answer in attempt.StudentQuizAnswers)
        {
            var question = answer.Question;
            if (question == null) continue;

            var existingReview = await _unitOfWork.QuizReviewItemRepository
                .FirstOrDefaultAsync(r => r.AttemptId == attemptId && r.QuestionId == question.Id);

            if (existingReview != null) continue;

            var relatedCaseIds = new List<Guid>();
            var topicTags = new List<string>();

            if (question.Case != null)
            {
                if (question.Case.BoneSpecialty != null)
                {
                    topicTags.Add(question.Case.BoneSpecialty.Name ?? "");
                }

                var relatedCases = await _unitOfWork.MedicalCaseRepository
                    .GetQueryable()
                    .Where(c => c.BoneSpecialtyId == question.Case.BoneSpecialtyId &&
                                c.Id != question.CaseId &&
                                c.IsApproved == true)
                    .Take(3)
                    .ToListAsync();

                relatedCaseIds = relatedCases.Select(c => c.Id).ToList();
            }

            var reviewItem = new QuizReviewItem
            {
                Id = Guid.NewGuid(),
                AttemptId = attemptId,
                QuestionId = question.Id,
                QuestionText = question.QuestionText,
                StudentAnswer = answer.StudentAnswer ?? answer.EssayAnswer,
                CorrectAnswer = question.CorrectAnswer,
                IsCorrect = answer.IsCorrect,
                AiExplanation = aiExplanations,
                RelatedCases = JsonConvert.SerializeObject(relatedCaseIds),
                TopicTags = JsonConvert.SerializeObject(topicTags),
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.QuizReviewItemRepository.Add(reviewItem);
        }

        await _unitOfWork.SaveAsync();
    }

    public async Task UpdateAiExplanationAsync(Guid reviewItemId, string explanation)
    {
        var reviewItem = await _unitOfWork.QuizReviewItemRepository.GetByIdAsync(reviewItemId);
        if (reviewItem == null) return;

        reviewItem.AiExplanation = explanation;
        _unitOfWork.QuizReviewItemRepository.Update(reviewItem);
        await _unitOfWork.SaveAsync();
    }

    public async Task<List<QuizReviewItem>> GetReviewItemsAsync(Guid attemptId)
    {
        return await _unitOfWork.QuizReviewItemRepository
            .GetQueryable()
            .Where(r => r.AttemptId == attemptId)
            .Include(r => r.Question)
            .ToListAsync();
    }
}
