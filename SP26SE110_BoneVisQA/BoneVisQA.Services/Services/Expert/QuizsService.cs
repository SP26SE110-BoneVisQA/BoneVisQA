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

        public async Task<QuizDTO> CreateQuizAsync(QuizDTO request)
        {
            var quiz = new Quiz
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                OpenTime = request.OpenTime,
                CloseTime = request.CloseTime,
                TimeLimit = request.TimeLimit,
                PassingScore = request.PassingScore,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.QuizRepository.AddAsync(quiz);
            await _unitOfWork.SaveAsync();

            return new QuizDTO
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

        public async Task<QuizQuestionDTO> CreateQuestionAsync(Guid quizId, CreateQuizQuestionDTO request)
        {
            var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId)
                ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

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

            return new QuizQuestionDTO
            {
                Id = question.Id,
                QuizId = question.QuizId,
                QuizTitle = quiz.Title,
                CaseId = question.CaseId,
                QuestionText = question.QuestionText,
                Type = question.Type,
                OptionA = question.OptionA,
                OptionB = question.OptionB,
                OptionC = question.OptionC,
                OptionD = question.OptionD,
                CorrectAnswer = question.CorrectAnswer
            };
        }
        public async Task<ClassQuizDTO> AssignQuizToClassAsync(Guid classId, Guid quizId)
        {
            var academicClass = await _unitOfWork.AcademicClassRepository
                .GetByIdAsync(classId)
                ?? throw new KeyNotFoundException("Không tìm thấy lớp học.");

            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(quizId)
                ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

            var existing = await _unitOfWork.ClassQuizRepository
                .FirstOrDefaultAsync(cq => cq.ClassId == classId && cq.QuizId == quizId);

            if (existing != null)
                throw new InvalidOperationException("Quiz đã được gán cho lớp này rồi.");

            var classQuiz = new ClassQuiz
            {
                ClassId = classId,
                QuizId = quizId,
                AssignedAt = DateTime.UtcNow
            };

            await _unitOfWork.ClassQuizRepository.AddAsync(classQuiz);
            await _unitOfWork.SaveAsync();
           
            return new ClassQuizDTO
            {
                ClassId = classQuiz.ClassId,
                ClassName = academicClass.ClassName, 
                QuizId = classQuiz.QuizId,
                QuizName = quiz.Title,  
                AssignedAt = classQuiz.AssignedAt
            };
        }
        public async Task<QuizScoreResultDTO> CalculateScoreAsync(Guid attemptId)
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

            return new QuizScoreResultDTO
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

        //============================= Lecturer & Student =============================
        public async Task<List<QuizQuestionDTO>> GetQuizQuestionsAsync(Guid quizId)
        {
            var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId)
               ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

            var question = await _unitOfWork.QuizQuestionRepository
           .FindByCondition(q => q.QuizId == quizId)
           .Include(q => q.Quiz)
           .ToListAsync();

            return question
                .Select(q => new QuizQuestionDTO
                {
                    Id = q.Id,
                    QuizId = q.QuizId,
                    QuizTitle = quiz.Title,
                    CaseId = q.CaseId,
                    QuestionText = q.QuestionText,
                    Type = q.Type,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectAnswer = q.CorrectAnswer
                })
                .ToList();
        }

        public async Task<bool> UpdateQuizQuestionAsync(Guid questionId, UpdateQuizsQuestionRequestDto request)
        {
            var entity = await _unitOfWork.QuizQuestionRepository
                .FindByCondition(q => q.Id == questionId)
                .FirstOrDefaultAsync();

            if (entity == null)
            {
                return false;
            }

            entity.QuestionText = request.QuestionText;
            entity.Type = request.Type ?? entity.Type;
            entity.OptionA = request.OptionA;
            entity.OptionB = request.OptionB;
            entity.OptionC = request.OptionC;
            entity.OptionD = request.OptionD;
            entity.CorrectAnswer = request.CorrectAnswer ?? entity.CorrectAnswer;

            await _unitOfWork.QuizQuestionRepository.UpdateAsync(entity);
            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<StudentSubmitQuestionResponseDTO> StudentSubmitQuestionsAsync(Guid studentId, StudentSubmitQuestionDTO submit)
        {
            var attempt = await _unitOfWork.QuizAttemptRepository
                .FirstOrDefaultAsync(a => a.Id == submit.AttemptId && a.StudentId == studentId)
                ?? throw new KeyNotFoundException("Không tìm thấy lần làm quiz.");

            var question = await _unitOfWork.QuizQuestionRepository
                .GetByIdAsync(submit.QuestionId)
                ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");

            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(attempt.QuizId)
                ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

            var existing = await _unitOfWork.StudentQuizAnswerRepository
                .FirstOrDefaultAsync(a => a.AttemptId == submit.AttemptId
                                       && a.QuestionId == submit.QuestionId);
            if (existing != null)
                throw new InvalidOperationException("Câu hỏi này đã được trả lời.");

            bool isCorrect = string.Equals(
                submit.StudentAnswer?.Trim(),
                question.CorrectAnswer?.Trim(),
                StringComparison.OrdinalIgnoreCase
            );

            var studentQuizAnswer = new StudentQuizAnswer
            {
                Id = Guid.NewGuid(),
                AttemptId = submit.AttemptId,
                QuestionId = submit.QuestionId,
                StudentAnswer = submit.StudentAnswer,
                IsCorrect = isCorrect
            };

            await _unitOfWork.StudentQuizAnswerRepository.AddAsync(studentQuizAnswer);
            await _unitOfWork.SaveAsync();

            return new StudentSubmitQuestionResponseDTO
            {
                QuizTile = quiz.Title,
                QuestionText = question.QuestionText,
                StudentAnswer = submit.StudentAnswer,
                CorrectAnswer = question.CorrectAnswer,
                IsCorrect = isCorrect
            };
        }
    }
}
