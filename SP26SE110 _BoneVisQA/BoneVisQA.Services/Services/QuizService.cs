using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Expert;
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

        public async Task<QuizDTO> CreateQuizAsync(QuizDTO dto)
        {
            var quiz = new Quiz
            {
                Id = Guid.NewGuid(),
                ClassId = dto.ClassId,
                Title = dto.Title,
                OpenTime = dto.OpenTime,
                CloseTime = dto.CloseTime,
                TimeLimit = dto.TimeLimit,
                PassingScore = dto.PassingScore,
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.QuizRepository.PrepareCreate(quiz);

            await _unitOfWork.SaveAsync();

            dto.Id = quiz.Id;
            dto.CreatedAt = quiz.CreatedAt;

            return dto;
        }

        public async Task<QuizQuestionDTO> CreateQuizQuestionAsync(QuizQuestionDTO dto)
        {
            var question = new QuizQuestion
            {
                Id = Guid.NewGuid(),
                QuizId = dto.QuizId,
                CaseId = dto.CaseId,
                QuestionText = dto.QuestionText,
                Type = dto.Type,
                CorrectAnswer = dto.CorrectAnswer
            };

            _unitOfWork.QuizQuestionRepository.PrepareCreate(question);

            await _unitOfWork.SaveAsync();

            dto.Id = question.Id;

            return dto;
        }

        public async Task<List<QuizDTO>> GetQuizForClassAsync(Guid classId)
        {
            var quizzes = await _unitOfWork.QuizRepository.GetAllAsync();

            return quizzes
                .Where(q => q.ClassId == classId)
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

        public async Task<StudentQuizAnswerDTO> SubmitAnswerAsync(StudentQuizAnswerDTO dto)
        {
            var question = await _unitOfWork.QuizQuestionRepository.GetByIdAsync(dto.QuestionId);

            bool isCorrect = false;

            if (question?.CorrectAnswer != null)
            {
                isCorrect = string.Equals(
                    question.CorrectAnswer.Trim(),
                    dto.StudentAnswer.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }

            var answer = new StudentQuizAnswer
            {
                Id = Guid.NewGuid(),
                AttemptId = dto.AttemptId,
                QuestionId = dto.QuestionId,
                StudentAnswer = dto.StudentAnswer,
                IsCorrect = isCorrect
            };

            _unitOfWork.StudentQuizAnswerRepository.PrepareCreate(answer);

            await _unitOfWork.SaveAsync();

            dto.Id = answer.Id;
            dto.IsCorrect = isCorrect;

            return dto;
        }

        public async Task<float> GradeQuizAttemptAsync(Guid attemptId)
        {
            var answers = await _unitOfWork.StudentQuizAnswerRepository.GetAllAsync();

            var attemptAnswers = answers.Where(a => a.AttemptId == attemptId).ToList();

            if (!attemptAnswers.Any())
                return 0;

            int total = attemptAnswers.Count;
            int correct = attemptAnswers.Count(a => a.IsCorrect == true);

            float score = (float)correct / total * 100;

            var attempt = await _unitOfWork.QuizAttemptRepository.GetByIdAsync(attemptId);

            if (attempt != null)
            {
                attempt.Score = score;

                _unitOfWork.QuizAttemptRepository.PrepareUpdate(attempt);
                await _unitOfWork.SaveAsync();
            }

            return score;
        }
    }
}
