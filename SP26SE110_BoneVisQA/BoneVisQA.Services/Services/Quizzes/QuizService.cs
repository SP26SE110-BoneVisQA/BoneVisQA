using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Quizzes;

public class QuizService : IQuizService
{
    private readonly IUnitOfWork _unitOfWork;

    public QuizService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private static DateTime? ToUtc(DateTime? dt)
    {
        if (!dt.HasValue) return null;
        return dt.Value.Kind switch
        {
            DateTimeKind.Utc => dt.Value,
            DateTimeKind.Local => dt.Value.ToUniversalTime(),
            // Unspecified: assume it's a local datetime string (e.g., from datetime-local input)
            // Parse as Vietnam time (UTC+7) and convert to UTC
            _ => ParseLocalToUtc(dt.Value, 7) // Treat as UTC+7 (Vietnam)
        };
    }

    /// <summary>
    /// Parse a DateTime with Kind=Unspecified as local time in the given timezone offset hours,
    /// then convert to UTC.
    /// </summary>
    private static DateTime ParseLocalToUtc(DateTime dt, int utcOffsetHours)
    {
        var localDateTime = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        var offset = TimeSpan.FromHours(utcOffsetHours);
        return DateTime.SpecifyKind(localDateTime.Subtract(offset), DateTimeKind.Utc);
    }

    public async Task<QuizDto> CreateQuizAsync(QuizDto request)
    {
        var now = DateTime.UtcNow;
        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Topic = request.Topic,
            IsAiGenerated = request.IsAiGenerated,
            Difficulty = request.Difficulty,
            Classification = request.Classification,
            OpenTime = ToUtc(request.OpenTime),
            CloseTime = ToUtc(request.CloseTime),
            TimeLimit = request.TimeLimit,
            PassingScore = request.PassingScore,
            CreatedAt = now
        };

        await _unitOfWork.QuizRepository.AddAsync(quiz);
        await _unitOfWork.SaveAsync();

        if (request.ClassId != Guid.Empty)
        {
            var classQuiz = new ClassQuizSession
            {
                Id = Guid.NewGuid(),
                ClassId = request.ClassId,
                QuizId = quiz.Id,
                OpenTime = ToUtc(request.OpenTime),
                CloseTime = ToUtc(request.CloseTime),
                PassingScore = request.PassingScore,
                TimeLimitMinutes = request.TimeLimit,
                CreatedAt = now
            };
            await _unitOfWork.ClassQuizSessionRepository.AddAsync(classQuiz);
            await _unitOfWork.SaveAsync();
        }

        request.Id = quiz.Id;
        request.CreatedAt = quiz.CreatedAt;

        return request;
    }

    public async Task<QuizQuestionDto> CreateQuestionAsync(Guid quizId, QuizQuestionDto request)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId);

        if (quiz == null)
            throw new Exception("Quiz not found");

        var question = new QuizQuestion
        {
            Id = Guid.NewGuid(),
            QuizId = quizId,
            CaseId = request.CaseId,
            QuestionText = request.QuestionText,
            Type = request.Type,
            CorrectAnswer = request.CorrectAnswer
        };

        await _unitOfWork.QuizQuestionRepository.AddAsync(question);
        await _unitOfWork.SaveAsync();

        request.Id = question.Id;
        request.QuizId = quizId;

        return request;
    }

    public async Task<List<QuizDto>> GetQuizzesByClassAsync(Guid classId)
    {
        var quizIds = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => cqs.ClassId == classId)
            .Select(cqs => cqs.QuizId)
            .Distinct()
            .ToListAsync();

        if (quizIds.Count == 0)
        {
            return new List<QuizDto>();
        }

        var quizzes = await _unitOfWork.QuizRepository
            .FindByCondition(q => quizIds.Contains(q.Id))
            .ToListAsync();

        return quizzes.Select(q => new QuizDto
        {
            Id = q.Id,
            ClassId = classId,
            Title = q.Title,
            Topic = q.Topic,
            IsAiGenerated = q.IsAiGenerated,
            Difficulty = q.Difficulty,
            Classification = q.Classification,
            OpenTime = q.OpenTime,
            CloseTime = q.CloseTime,
            TimeLimit = q.TimeLimit,
            PassingScore = q.PassingScore,
            CreatedAt = q.CreatedAt
        }).ToList();
    }

    public async Task<List<QuizDto>> RecommendQuizAsync(string topic)
    {
        var quizzes = await _unitOfWork.QuizRepository
            .FindByCondition(q => q.Title.Contains(topic))
            .Take(10)
            .ToListAsync();

        return quizzes.Select(q => new QuizDto
        {
            Id = q.Id,
            ClassId = Guid.Empty,
            Title = q.Title,
            Topic = q.Topic,
            IsAiGenerated = q.IsAiGenerated,
            Difficulty = q.Difficulty,
            Classification = q.Classification,
            OpenTime = q.OpenTime,
            CloseTime = q.CloseTime,
            TimeLimit = q.TimeLimit,
            PassingScore = q.PassingScore,
            CreatedAt = q.CreatedAt
        }).ToList();
    }
}
