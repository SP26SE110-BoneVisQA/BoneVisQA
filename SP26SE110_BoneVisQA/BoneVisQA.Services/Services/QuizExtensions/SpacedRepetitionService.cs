using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.QuizExtensions;

public class SpacedRepetitionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SpacedRepetitionService> _logger;

    public SpacedRepetitionService(IUnitOfWork unitOfWork, ILogger<SpacedRepetitionService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public class ReviewItemDto
    {
        public Guid ScheduleId { get; set; }
        public Guid? CaseId { get; set; }
        public Guid? QuizId { get; set; }
        public Guid? QuestionId { get; set; }
        public string CaseTitle { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string? CorrectAnswer { get; set; }
        public DateOnly NextReviewDate { get; set; }
        public int RepetitionCount { get; set; }
        public int DaysOverdue { get; set; }
    }

    public class SpacedRepetitionStats
    {
        public int TotalReviews { get; set; }
        public int DueToday { get; set; }
        public int DueTomorrow { get; set; }
        public int DueThisWeek { get; set; }
        public int Overdue { get; set; }
        public int Mastered { get; set; }
    }

    public async Task<List<ReviewItemDto>> GetDueReviewsAsync(Guid studentId, int limit = 20)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);
        var endOfWeek = today.AddDays(7);

        var reviews = await _unitOfWork.ReviewScheduleRepository
            .GetQueryable()
            .Where(r => r.StudentId == studentId && r.NextReviewDate <= endOfWeek)
            .Include(r => r.Case)
            .Include(r => r.Question)
            .OrderBy(r => r.NextReviewDate)
            .Take(limit)
            .ToListAsync();

        return reviews.Select(r => new ReviewItemDto
        {
            ScheduleId = r.Id,
            CaseId = r.CaseId,
            QuizId = r.QuizId,
            QuestionId = r.QuestionId,
            CaseTitle = r.Case?.Title ?? "Unknown Case",
            QuestionText = r.Question?.QuestionText ?? "",
            CorrectAnswer = r.Question?.CorrectAnswer,
            NextReviewDate = r.NextReviewDate,
            RepetitionCount = r.RepetitionCount,
            DaysOverdue = Math.Max(0, today.DayNumber - r.NextReviewDate.DayNumber)
        }).ToList();
    }

    public async Task<SpacedRepetitionStats> GetStatsAsync(Guid studentId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);
        var endOfWeek = today.AddDays(7);

        var reviews = await _unitOfWork.ReviewScheduleRepository
            .FindAsync(r => r.StudentId == studentId);

        return new SpacedRepetitionStats
        {
            TotalReviews = reviews.Count,
            DueToday = reviews.Count(r => r.NextReviewDate == today),
            DueTomorrow = reviews.Count(r => r.NextReviewDate == tomorrow),
            DueThisWeek = reviews.Count(r => r.NextReviewDate > today && r.NextReviewDate <= endOfWeek),
            Overdue = reviews.Count(r => r.NextReviewDate < today),
            Mastered = reviews.Count(r => r.RepetitionCount >= 5 && r.EaseFactor >= 2.3m)
        };
    }

    public async Task ScheduleReviewAsync(Guid studentId, Guid? caseId, Guid? quizId, Guid? questionId, bool wasCorrect)
    {
        var quality = wasCorrect ? 4 : 1;

        var existing = await _unitOfWork.ReviewScheduleRepository
            .FirstOrDefaultAsync(r => r.StudentId == studentId && 
                        r.CaseId == caseId && 
                        r.QuestionId == questionId);

        if (existing != null)
        {
            await UpdateReviewAsync(existing.Id, quality);
            return;
        }

        var initialValues = SM2Algorithm.GetInitialValues();

        var schedule = new ReviewSchedule
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CaseId = caseId,
            QuizId = quizId,
            QuestionId = questionId,
            NextReviewDate = initialValues.NextReviewDate,
            EaseFactor = initialValues.EaseFactor,
            IntervalDays = initialValues.IntervalDays,
            RepetitionCount = initialValues.RepetitionCount,
            LastQuality = quality,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _unitOfWork.ReviewScheduleRepository.Add(schedule);
        await _unitOfWork.SaveAsync();

        await UpdateReviewAsync(schedule.Id, quality);
    }

    public async Task UpdateReviewAsync(Guid scheduleId, int quality)
    {
        quality = Math.Clamp(quality, 0, 5);

        var schedule = await _unitOfWork.ReviewScheduleRepository.GetByIdAsync(scheduleId);
        if (schedule == null) return;

        schedule.LastQuality = quality;
        schedule.LastReviewDate = DateTime.UtcNow;

        var result = SM2Algorithm.Calculate(
            schedule.EaseFactor,
            schedule.IntervalDays,
            schedule.RepetitionCount,
            quality);

        schedule.EaseFactor = result.EaseFactor;
        schedule.IntervalDays = result.IntervalDays;
        schedule.RepetitionCount = result.RepetitionCount;
        schedule.NextReviewDate = result.NextReviewDate;
        schedule.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.ReviewScheduleRepository.Update(schedule);
        await _unitOfWork.SaveAsync();
    }

    public async Task DeleteReviewAsync(Guid scheduleId)
    {
        var schedule = await _unitOfWork.ReviewScheduleRepository.GetByIdAsync(scheduleId);
        if (schedule == null) return;

        _unitOfWork.ReviewScheduleRepository.Remove(schedule);
        await _unitOfWork.SaveAsync();
    }

    public async Task DeleteAllReviewsForStudentAsync(Guid studentId)
    {
        var reviews = await _unitOfWork.ReviewScheduleRepository
            .FindAsync(r => r.StudentId == studentId);

        foreach (var review in reviews)
        {
            _unitOfWork.ReviewScheduleRepository.Remove(review);
        }

        await _unitOfWork.SaveAsync();
    }
}
