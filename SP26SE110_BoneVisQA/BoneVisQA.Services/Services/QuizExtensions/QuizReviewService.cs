using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Services.AiQuizServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BoneVisQA.Services.Services.QuizExtensions;

public class QuizReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<QuizReviewService> _logger;
    private readonly IQuizGeminiService _geminiService;

    public QuizReviewService(
        IUnitOfWork unitOfWork,
        ILogger<QuizReviewService> logger,
        IQuizGeminiService geminiService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _geminiService = geminiService;
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
        public string? LecturerFeedback { get; set; }
        public string? ReferenceAnswer { get; set; }
        public string? ImageUrl { get; set; }
        public string? CaseTitle { get; set; }
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
                RelatedCases = relatedCases,
                LecturerFeedback = answer.LecturerFeedback,
                ReferenceAnswer = question.ReferenceAnswer,
                ImageUrl = question.ImageUrl,
                CaseTitle = question.Case?.Title
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

            // Generate AI explanation using Gemini
            string? generatedExplanation = null;
            if (!string.IsNullOrWhiteSpace(aiExplanations))
            {
                // Use provided explanation
                generatedExplanation = aiExplanations;
            }
            else
            {
                // Generate explanation using Gemini AI
                generatedExplanation = await GenerateAiExplanationAsync(question, answer, attempt);
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
                AiExplanation = generatedExplanation,
                RelatedCases = JsonConvert.SerializeObject(relatedCaseIds),
                TopicTags = JsonConvert.SerializeObject(topicTags),
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.QuizReviewItemRepository.Add(reviewItem);
        }

        await _unitOfWork.SaveAsync();
    }

    private async Task<string?> GenerateAiExplanationAsync(
        QuizQuestion question,
        StudentQuizAnswer answer,
        QuizAttempt attempt)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a medical education expert specializing in bone pathology X-ray analysis.");
            sb.AppendLine();
            sb.AppendLine("Please provide a clear, educational explanation for the following quiz question:");
            sb.AppendLine();

            // Question details
            if (!string.IsNullOrWhiteSpace(question.QuestionText))
            {
                sb.AppendLine($"Question: {question.QuestionText}");
                sb.AppendLine();
            }

            // Case context
            if (question.Case != null)
            {
                if (!string.IsNullOrWhiteSpace(question.Case.Title))
                    sb.AppendLine($"Case: {question.Case.Title}");
                if (!string.IsNullOrWhiteSpace(question.Case.BoneSpecialty?.Name))
                    sb.AppendLine($"Bone Specialty: {question.Case.BoneSpecialty.Name}");
                if (!string.IsNullOrWhiteSpace(question.Case.Description))
                    sb.AppendLine($"Case Description: {question.Case.Description}");
                sb.AppendLine();
            }

            // Student's answer
            var studentAnswer = answer.StudentAnswer ?? answer.EssayAnswer ?? "No answer";
            sb.AppendLine($"Student's Answer: {studentAnswer}");
            sb.AppendLine();

            // Correct answer
            if (!string.IsNullOrWhiteSpace(question.CorrectAnswer))
            {
                sb.AppendLine($"Correct Answer: {question.CorrectAnswer}");
                sb.AppendLine();
            }

            // Result
            var isCorrect = answer.IsCorrect == true;
            sb.AppendLine($"Result: {(isCorrect ? "CORRECT" : "INCORRECT")}");
            sb.AppendLine();

            // Instructions
            sb.AppendLine("Please explain:");
            sb.AppendLine("1. Why the correct answer is correct");
            if (!isCorrect)
            {
                sb.AppendLine("2. Why the student's answer was incorrect (if applicable)");
            }
            sb.AppendLine("3. Key learning points from this question");
            sb.AppendLine("4. Any tips for identifying similar cases in the future");
            sb.AppendLine();
            sb.AppendLine("Format your response as a clear, educational explanation suitable for medical students.");

            var prompt = sb.ToString();
            var imageUrl = question.ImageUrl;
            var explanation = await _geminiService.GenerateQuizAsync(prompt, imageUrl);

            if (!string.IsNullOrWhiteSpace(explanation))
            {
                _logger.LogInformation("Generated AI explanation for question {QuestionId} in attempt {AttemptId}",
                    question.Id, attempt.Id);
            }

            return explanation;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate AI explanation for question {QuestionId}", question.Id);
            return null;
        }
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
