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

    public async Task<QuizDto> CreateQuizAsync(QuizDto request)
    {
        var now = DateTime.UtcNow;
        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            OpenTime = request.OpenTime,
            CloseTime = request.CloseTime,
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
                OpenTime = request.OpenTime,
                CloseTime = request.CloseTime,
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
            OpenTime = q.OpenTime,
            CloseTime = q.CloseTime,
            TimeLimit = q.TimeLimit,
            PassingScore = q.PassingScore,
            CreatedAt = q.CreatedAt
        }).ToList();
    }
}
