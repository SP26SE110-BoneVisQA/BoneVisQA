using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Expert
{
    public class QuizsService : IQuizsService
    {
        private readonly IUnitOfWork _unitOfWork;

        public QuizsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<QuizDto> CreateQuizAsync(QuizDto request)
        {
            var quiz = new Quiz
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                OpenTime = request.OpenTime.HasValue ? DateTime.SpecifyKind(request.OpenTime.Value, DateTimeKind.Utc) : null,
                CloseTime = request.CloseTime.HasValue ? DateTime.SpecifyKind(request.CloseTime.Value, DateTimeKind.Utc) : null,
                TimeLimit = request.TimeLimit,
                PassingScore = request.PassingScore,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.QuizRepository.AddAsync(quiz);
            await _unitOfWork.SaveAsync();

            return new QuizDto
            {
                Id = quiz.Id,
                Title = quiz.Title,
                OpenTime = quiz.OpenTime,
                CloseTime = quiz.CloseTime,
                TimeLimit = quiz.TimeLimit,
                PassingScore = quiz.PassingScore,
                CreatedAt = quiz.CreatedAt
            };
        }

        public async Task<QuizQuestionDto> CreateQuestionAsync(Guid quizId, CreateQuizQuestionDto request)
        {
            var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId)
                ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

            MedicalCase? medicalCase = null;
            if (request.CaseId.HasValue)
            {
                medicalCase = await _unitOfWork.MedicalCaseRepository
                    .GetByIdAsync(request.CaseId.Value)
                    ?? throw new KeyNotFoundException("Không tìm thấy medical case.");
            }

            var question = new QuizQuestion
            {
                QuizId = quizId,
                CaseId = request.CaseId,
                QuestionText = request.QuestionText,
                Type = request.Type,
                OptionA = request.OptionA,
                OptionB = request.OptionB,
                OptionC = request.OptionC,
                OptionD = request.OptionD,
                CorrectAnswer = request.CorrectAnswer
            };

            await _unitOfWork.QuizQuestionRepository.AddAsync(question);
            await _unitOfWork.SaveAsync();

            return new QuizQuestionDto
            {
                Id = question.Id,
                QuizId = question.QuizId,
                QuizTitle = quiz.Title,
                CaseId = question.CaseId,
                CaseTitle = medicalCase?.Title,
                QuestionText = question.QuestionText,
                Type = question.Type,
                OptionA = question.OptionA,
                OptionB = question.OptionB,
                OptionC = question.OptionC,
                OptionD = question.OptionD,
                CorrectAnswer = question.CorrectAnswer
            };
        }
        public async Task<ClassQuizSessionResponseDTO> AssignQuizToClassAsync(AssignQuizRequestDTO dto)
        {
            var academicClass = await _unitOfWork.AcademicClassRepository
                .GetByIdAsync(dto.ClassId)
                ?? throw new KeyNotFoundException("Không tìm thấy lớp học.");

            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(dto.QuizId)
                ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

            var existing = await _unitOfWork.ClassQuizSessionRepository
                .FirstOrDefaultAsync(cq => cq.ClassId == dto.ClassId && cq.QuizId == dto.QuizId);

            if (existing != null)
                throw new InvalidOperationException("Quiz đã được gán cho lớp này rồi.");

            var openTime = dto.OpenTime.HasValue ? DateTime.SpecifyKind(dto.OpenTime.Value, DateTimeKind.Utc) : quiz.OpenTime;
            var closeTime = dto.CloseTime.HasValue ? DateTime.SpecifyKind(dto.CloseTime.Value, DateTimeKind.Utc) : quiz.CloseTime;

            var classQuiz = new ClassQuizSession
            {
                Id = Guid.NewGuid(),

                ClassId = dto.ClassId,
                QuizId = dto.QuizId,

                OpenTime = openTime,
                CloseTime = closeTime,

                PassingScore = dto.PassingScore ?? quiz.PassingScore,
                TimeLimitMinutes = dto.TimeLimitMinutes ?? quiz.TimeLimit,

                CreatedAt = DateTime.UtcNow
            };


            await _unitOfWork.ClassQuizSessionRepository.AddAsync(classQuiz);
            await _unitOfWork.SaveAsync();

            return new ClassQuizSessionResponseDTO
            {
                ClassId = classQuiz.ClassId,
                ClassName = academicClass.ClassName,

                QuizId = classQuiz.QuizId,
                QuizName = quiz.Title,

                AssignedAt = classQuiz.CreatedAt,

                OpenTime = classQuiz.OpenTime,
                CloseTime = classQuiz.CloseTime,

                PassingScore = classQuiz.PassingScore,
                TimeLimitMinutes = classQuiz.TimeLimitMinutes
            };
        }
        public async Task<QuizScoreResultDto> CalculateScoreAsync(Guid attemptId)
        {
            var attempt = await _unitOfWork.QuizAttemptRepository
                .GetByIdAsync(attemptId)
                ?? throw new KeyNotFoundException("Không tìm thấy lần làm quiz.");

            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(attempt.QuizId)
                ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

            var questions = await _unitOfWork.QuizQuestionRepository
                .FindAsync(q => q.QuizId == attempt.QuizId);

            if (!questions.Any())
                throw new InvalidOperationException("Quiz chưa có câu hỏi.");

            var studentAnswers = await _unitOfWork.StudentQuizAnswerRepository
                .FindAsync(a => a.AttemptId == attemptId);

            int totalQuestions = questions.Count;
            int correctAnswers = studentAnswers.Count(a => a.IsCorrect == true);

            float score = (float)Math.Round((double)correctAnswers / totalQuestions * 10, 1);

            bool isPassed = score >= quiz.PassingScore;

            attempt.Score = score;
            attempt.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.QuizAttemptRepository.UpdateAsync(attempt);
            await _unitOfWork.SaveAsync();

            return new QuizScoreResultDto
            {
                AttemptId = attempt.Id,
                StudentId = attempt.StudentId,
                QuizId = quiz.Id,
                QuizTitle = quiz.Title,
                TotalQuestions = totalQuestions,
                CorrectAnswers = correctAnswers,
                Score = score,
                PassingScore = quiz.PassingScore,
                IsPassed = isPassed,
                CompletedAt = attempt.CompletedAt
            };
        }
    }
}
