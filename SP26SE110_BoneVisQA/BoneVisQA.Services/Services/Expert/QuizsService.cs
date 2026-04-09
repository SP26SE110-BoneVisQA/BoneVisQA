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
        public async Task<PagedResult<GetQuizDTO>> GetQuizDTO(int pageIndex, int pageSize)
        {
            var query = await _unitOfWork.QuizRepository.GetAllAsync();

            var totalCount = query.Count();

            var quizzes = query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new GetQuizDTO
                {
                    Id = q.Id,
                    Title = q.Title,
                    Topic = q.Topic,
                    OpenTime = q.OpenTime,
                    CloseTime = q.CloseTime,
                    TimeLimit = q.TimeLimit,
                    PassingScore = q.PassingScore,
                    IsAiGenerated = q.IsAiGenerated,
                    Difficulty = q.Difficulty,
                    Classification = q.Classification,
                    CreatedAt = q.CreatedAt
                })
                .ToList();

            return new PagedResult<GetQuizDTO>
            {
                Items = quizzes,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
        public async Task<CreateQuizResponseDTO> CreateQuizAsync(CreateQuizRequestDTO request)
        {
            var quiz = new Quiz
            {
                Id = Guid.NewGuid(),
                Title = request.Title,

                CreatedByExpertId = request.CreatedByExpertId,

                Topic = request.Topic,

                OpenTime = request.OpenTime.HasValue? DateTime.SpecifyKind(request.OpenTime.Value, DateTimeKind.Utc): null,

                CloseTime = request.CloseTime.HasValue? DateTime.SpecifyKind(request.CloseTime.Value, DateTimeKind.Utc): null,

                TimeLimit = request.TimeLimit,
                PassingScore = request.PassingScore,

                IsAiGenerated = false,

                Difficulty = request.Difficulty,
                Classification = request.Classification,

                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.QuizRepository.AddAsync(quiz);
            await _unitOfWork.SaveAsync();

            string? expertName = null;

            if (request.CreatedByExpertId.HasValue)
            {
                var expert = await _unitOfWork.UserRepository.GetByIdAsync(request.CreatedByExpertId.Value);

                expertName = expert?.FullName;
            }

            return new CreateQuizResponseDTO
            {
                Id = quiz.Id,
                Title = quiz.Title,

                ExpertName = expertName,

                Topic = quiz.Topic,
                OpenTime = quiz.OpenTime,
                CloseTime = quiz.CloseTime,

                TimeLimit = quiz.TimeLimit,
                PassingScore = quiz.PassingScore,

                IsAiGenerated = quiz.IsAiGenerated,
                Difficulty = quiz.Difficulty,
                Classification = quiz.Classification,

                CreatedAt = quiz.CreatedAt
            };
        }
        public async Task<UpdateQuizResponseDTO> UpdateQuizAsync(UpdateQuizRequestDTO update)
        {
            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(update.Id);

            if (quiz == null)
                throw new KeyNotFoundException("Quiz không tồn tại.");

            quiz.Title = update.Title;

            quiz.Topic = update.Topic;

            quiz.OpenTime = update.OpenTime.HasValue
                ? DateTime.SpecifyKind(update.OpenTime.Value, DateTimeKind.Utc)
                : null;

            quiz.CloseTime = update.CloseTime.HasValue
                ? DateTime.SpecifyKind(update.CloseTime.Value, DateTimeKind.Utc)
                : null;

            quiz.TimeLimit = update.TimeLimit;

            quiz.PassingScore = update.PassingScore;

            quiz.Difficulty = update.Difficulty;

            quiz.Classification = update.Classification;

            await _unitOfWork.QuizRepository.UpdateAsync(quiz);

            await _unitOfWork.SaveAsync();

            return new UpdateQuizResponseDTO
            {
                Title = quiz.Title,
                Topic = quiz.Topic,
                OpenTime = quiz.OpenTime,
                CloseTime = quiz.CloseTime,
                TimeLimit = quiz.TimeLimit,
                PassingScore = quiz.PassingScore,
                Difficulty = quiz.Difficulty,
                Classification = quiz.Classification,
                CreatedAt = quiz.CreatedAt
            };
        }
        public async Task<bool> DeleteQuizAsync(Guid quizId)
        {
            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(quizId);

            if (quiz == null)
                return false;

            await _unitOfWork.QuizRepository.RemoveAsync(quiz);

            await _unitOfWork.SaveAsync();

            return true;
        }

        //================================================================================================================
        public async Task<List<GetQuizQuestionDTO>> GetQuizQuestionDTO(Guid quizId)
        {
            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(quizId);

            if (quiz == null)
                throw new KeyNotFoundException("Không tìm thấy quiz.");

            var questions = await _unitOfWork.QuizQuestionRepository
                .FindAsync(q => q.QuizId == quizId);

            var result = new List<GetQuizQuestionDTO>();

            foreach (var q in questions)
            {
                string? caseTitle = null;

                if (q.CaseId.HasValue)
                {
                    var medicalCase = await _unitOfWork.MedicalCaseRepository
                        .GetByIdAsync(q.CaseId.Value);

                    caseTitle = medicalCase?.Title;
                }

                result.Add(new GetQuizQuestionDTO
                {
                    QuestionId = q.Id,
                    QuizTitle = quiz.Title,
                    CaseTitle = caseTitle,

                    QuestionText = q.QuestionText,
                    Type = q.Type,

                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,

                    CorrectAnswer = q.CorrectAnswer
                });
            }

            return result;
        }
        public async Task<CreateQuizQuestionResponseDTO> CreateQuizQuestionAsync(Guid quizId, CreateQuizQuestionRequestDTO request)
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

            return new CreateQuizQuestionResponseDTO
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
        public async Task<UpdateQuizQuestionResponseDTO> UpdateQuizQuestionAsync(UpdateQuizQuestionRequestDTO update)
        {
            var question = await _unitOfWork.QuizQuestionRepository
                .GetByIdAsync(update.QuestionId);

            if (question == null)
                throw new KeyNotFoundException("Không tìm thấy câu hỏi.");

            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(update.QuizId);

            if (quiz == null)
                throw new KeyNotFoundException("Không tìm thấy quiz.");

            var medicalCase = await _unitOfWork.MedicalCaseRepository
                .GetByIdAsync(update.CaseId);

            if (medicalCase == null)
                throw new KeyNotFoundException("Không tìm thấy medical case.");

            question.QuizId = update.QuizId;
            question.CaseId = update.CaseId;

            question.QuestionText = update.QuestionText;
            question.Type = update.Type;

            question.OptionA = update.OptionA;
            question.OptionB = update.OptionB;
            question.OptionC = update.OptionC;
            question.OptionD = update.OptionD;

            question.CorrectAnswer = update.CorrectAnswer;

            await _unitOfWork.QuizQuestionRepository.UpdateAsync(question);

            await _unitOfWork.SaveAsync();

            return new UpdateQuizQuestionResponseDTO
            {
                QuestionText = question.QuestionText,

                QuizTitle = quiz.Title,

                CaseTitle = medicalCase.Title,

                Type = question.Type,

                CorrectAnswer = question.CorrectAnswer,

                OptionA = question.OptionA,
                OptionB = question.OptionB,
                OptionC = question.OptionC,
                OptionD = question.OptionD
            };
        }
        public async Task<bool> DeleteQuizQuestionAsync(Guid questionId)
        {
            var question = await _unitOfWork.QuizQuestionRepository
                .GetByIdAsync(questionId);

            if (question == null)
                return false;

            await _unitOfWork.QuizQuestionRepository.RemoveAsync(question);

            await _unitOfWork.SaveAsync();

            return true;
        }


        //================================================================================================================
        public async Task<PagedResult<ClassQuizSessionDTO>> GetAssignQuizDTO(
    int pageIndex,
    int pageSize)
        {
            var query = _unitOfWork.ClassQuizSessionRepository
                .GetQueryable();

            var totalCount = await query.CountAsync();

            var sessions = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ClassQuizSessionDTO
                {
                    ClassId = x.ClassId,
                    ClassName = x.Class.ClassName,

                    QuizId = x.QuizId,
                    QuizName = x.Quiz.Title,

                    AssignedAt = x.CreatedAt,

                    OpenTime = x.OpenTime,
                    CloseTime = x.CloseTime,

                    PassingScore = x.PassingScore,
                    TimeLimitMinutes = x.TimeLimitMinutes
                })
                .ToListAsync();

            return new PagedResult<ClassQuizSessionDTO>
            {
                Items = sessions,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
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
           
            string? expertName = null;

            if (dto.AssignedExpertId.HasValue)
            {
                var expert = await _unitOfWork.UserRepository
                    .GetByIdAsync(dto.AssignedExpertId.Value);

                expertName = expert?.FullName;
            }

            return new ClassQuizSessionResponseDTO
            {
                ClassId = classQuiz.ClassId,
                ClassName = academicClass.ClassName,

                QuizId = classQuiz.QuizId,
                QuizName = quiz.Title,

                ExpertName = expertName, 
                AssignedAt = classQuiz.CreatedAt,

                OpenTime = classQuiz.OpenTime,
                CloseTime = classQuiz.CloseTime,

                PassingScore = classQuiz.PassingScore,
                TimeLimitMinutes = classQuiz.TimeLimitMinutes
            };
        }
        public async Task<List<GetQuizAttemptDTO>> GetAttemptsByQuizAsync(Guid quizId)
        {
            var attempts = await _unitOfWork.QuizAttemptRepository
        .GetQueryable()
        .Where(x => x.QuizId == quizId)
        .Select(x => new GetQuizAttemptDTO
        {
            AttemptId = x.Id,
            QuizId = x.QuizId,

            StudentId = x.StudentId,
            StudentName = x.Student.FullName,

            QuizTitle = x.Quiz.Title,

            StartedAt = x.StartedAt,
            CompletedAt = x.CompletedAt,
            Score = x.Score
        })
        .ToListAsync();

            return attempts;
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

        public async Task<PagedResult<GetClassDTO>> GetAllClass(int pageIndex, int pageSize)
        {
            var query = _unitOfWork.AcademicClassRepository.GetQueryable();

            var totalCount = await query.CountAsync();

            var classes = await query
                .OrderBy(x => x.ClassName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetClassDTO
                {
                    Id = x.Id,
                    ClassName = x.ClassName
                })
                .ToListAsync();

            return new PagedResult<GetClassDTO>
            {
                Items = classes,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
        public async Task<PagedResult<GetExpertDTO>> GetAllExpert(int pageIndex, int pageSize)
        {
            var query = _unitOfWork.UserRepository
                .GetQueryable()
                .Where(u => u.UserRoles.Any(ur => ur.Role.Name == "Expert"));

            var totalCount = await query.CountAsync();

            var experts = await query
                .OrderBy(x => x.FullName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetExpertDTO
                {
                    Id = x.Id,
                    FullName = x.FullName
                })
                .ToListAsync();

            return new PagedResult<GetExpertDTO>
            {
                Items = experts,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
    }
}
