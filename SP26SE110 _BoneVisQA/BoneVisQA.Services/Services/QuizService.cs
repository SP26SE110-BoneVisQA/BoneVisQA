using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Expert;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services
{
    public class QuizService : IQuizService
    {
        private readonly IUnitOfWork _unitOfWork;

        public QuizService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<QuizDTO> CreateQuizAsync(QuizDTO request)
        {
            var quiz = new Quiz
            {
                Id = Guid.NewGuid(),
                ClassId = request.ClassId,
                Title = request.Title,
                OpenTime = request.OpenTime,
                CloseTime = request.CloseTime,
                TimeLimit = request.TimeLimit,
                PassingScore = request.PassingScore,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.QuizRepository.AddAsync(quiz);

            await _unitOfWork.SaveAsync();

            request.Id = quiz.Id;
            request.CreatedAt = quiz.CreatedAt;

            return request;
        }

        public async Task<QuizQuestionDTO> CreateQuestionAsync(Guid quizId, QuizQuestionDTO request)
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

        public async Task<List<QuizDTO>> GetQuizzesByClassAsync(Guid classId)
        {
            var quizzes = await _unitOfWork.QuizRepository.FindAsync(q => q.ClassId == classId);

            return quizzes.Select(q => new QuizDTO
            {
                Id = q.Id,
                ClassId = q.ClassId,
                Title = q.Title,
                OpenTime = q.OpenTime,
                CloseTime = q.CloseTime,
                TimeLimit = q.TimeLimit,
                PassingScore = q.PassingScore,
                CreatedAt = q.CreatedAt
            }).ToList();
        }

        public async Task<List<QuizDTO>> RecommendQuizAsync(string topic)
        {
            var quizzes = await _unitOfWork.QuizRepository.FindAsync(q => q.Title.Contains(topic));

            return quizzes
                .Take(10)
                .Select(q => new QuizDTO
                {
                    Id = q.Id,
                    ClassId = q.ClassId,
                    Title = q.Title,
                    OpenTime = q.OpenTime,
                    CloseTime = q.CloseTime,
                    TimeLimit = q.TimeLimit,
                    PassingScore = q.PassingScore,
                    CreatedAt = q.CreatedAt
                })
                .ToList();
        }
    }
}
