using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BoneVisQA.Services.Services.Expert
{
    public class QuizsService : IQuizsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public QuizsService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
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

        /// <summary>
        /// Passing score luôn ở thang 100 (0-100). Identity function.
        /// </summary>
        private static int? NormalizePassingScore(int? passingScore, bool isAiGenerated)
        {
            return passingScore;
        }

        /// <summary>
        /// Resolve image URL to absolute URL.
        /// - If already absolute (http/https), return as-is.
        /// - If relative (e.g. /uploads/images/...), prepend base URL from config.
        /// </summary>
        private string ResolveImageUrl(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return string.Empty;

            var url = imageUrl.Trim();

            // Already absolute
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            // Relative path — prepend base URL from configuration
            var baseUrl = _configuration["App:BaseUrl"] ?? "http://localhost:5046";
            // Ensure base doesn't end with slash, path starts with slash
            var cleanBase = baseUrl.TrimEnd('/');
            var path = url.StartsWith('/') ? url : $"/{url}";
            return $"{cleanBase}{path}";
        }

        public async Task<PagedResult<GetQuizDTO>> GetQuizAsync(int pageIndex, int pageSize)
        {
            var query = _unitOfWork.QuizRepository.GetAllAsync().Result.AsQueryable();

            // Chỉ lấy quiz do Expert tạo (IsAiGenerated = false), không lấy quiz do AI tạo
            query = query.Where(q => !q.IsAiGenerated);

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
                    //  PassingScore = q.PassingScore,
                    PassingScore = NormalizePassingScore(q.PassingScore, q.IsAiGenerated),
                    IsAiGenerated = q.IsAiGenerated,
                    Difficulty = q.Difficulty,
                    Classification = q.Classification,
                    CreatedAt = q.CreatedAt,
                    // Deep classification
                    BoneSpecialtyId = q.BoneSpecialtyId,
                    PathologyCategoryId = q.PathologyCategoryId
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

                OpenTime = ToUtc(request.OpenTime),

                CloseTime = ToUtc(request.CloseTime),

                TimeLimit = request.TimeLimit,
                PassingScore = NormalizePassingScore(request.PassingScore, false),

                IsAiGenerated = false,

                Difficulty = request.Difficulty,
                Classification = request.Classification,

                CreatedAt = DateTime.UtcNow,

                // Deep classification
                BoneSpecialtyId = request.BoneSpecialtyId,
                PathologyCategoryId = request.PathologyCategoryId
            };

            await _unitOfWork.QuizRepository.AddAsync(quiz);
            await _unitOfWork.SaveAsync();

            string? expertName = null;

            if (request.CreatedByExpertId.HasValue)
            {
                var expert = await _unitOfWork.UserRepository.GetByIdAsync(request.CreatedByExpertId.Value);

                expertName = expert?.FullName;
            }

            // Get bone specialty name
            string? boneSpecialtyName = null;
            if (quiz.BoneSpecialtyId.HasValue)
            {
                var boneSpec = await _unitOfWork.Context.BoneSpecialties.FindAsync(quiz.BoneSpecialtyId.Value);
                boneSpecialtyName = boneSpec?.Name;
            }

            // Get pathology category name
            string? pathologyCategoryName = null;
            if (quiz.PathologyCategoryId.HasValue)
            {
                var pathCat = await _unitOfWork.Context.PathologyCategories.FindAsync(quiz.PathologyCategoryId.Value);
                pathologyCategoryName = pathCat?.Name;
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

                CreatedAt = quiz.CreatedAt,

                // Deep classification
                BoneSpecialtyId = quiz.BoneSpecialtyId,
                BoneSpecialtyName = boneSpecialtyName,
                PathologyCategoryId = quiz.PathologyCategoryId,
                PathologyCategoryName = pathologyCategoryName
            };
        }
        public async Task<UpdateQuizResponseDTO> UpdateQuizAsync(UpdateQuizRequestDTO update)
        {
            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(update.Id);

            if (quiz == null)
                throw new KeyNotFoundException("Quiz does not exist.");

            quiz.Title = update.Title;

            quiz.Topic = update.Topic;

            quiz.OpenTime = ToUtc(update.OpenTime);
            quiz.CloseTime = ToUtc(update.CloseTime);

            quiz.TimeLimit = update.TimeLimit;

            quiz.PassingScore = NormalizePassingScore(update.PassingScore, false);

            quiz.Difficulty = update.Difficulty;

            quiz.Classification = update.Classification;

            // Deep classification
            quiz.BoneSpecialtyId = update.BoneSpecialtyId;
            quiz.PathologyCategoryId = update.PathologyCategoryId;

            await _unitOfWork.QuizRepository.UpdateAsync(quiz);

            await _unitOfWork.SaveAsync();

            // Get bone specialty name
            string? boneSpecialtyName = null;
            if (quiz.BoneSpecialtyId.HasValue)
            {
                var boneSpec = await _unitOfWork.Context.BoneSpecialties.FindAsync(quiz.BoneSpecialtyId.Value);
                boneSpecialtyName = boneSpec?.Name;
            }

            // Get pathology category name
            string? pathologyCategoryName = null;
            if (quiz.PathologyCategoryId.HasValue)
            {
                var pathCat = await _unitOfWork.Context.PathologyCategories.FindAsync(quiz.PathologyCategoryId.Value);
                pathologyCategoryName = pathCat?.Name;
            }

            return new UpdateQuizResponseDTO
            {
                Title = quiz.Title,
                Topic = quiz.Topic,
                OpenTime = quiz.OpenTime,
                CloseTime = quiz.CloseTime,
                TimeLimit = quiz.TimeLimit,
                PassingScore = NormalizePassingScore(quiz.PassingScore, quiz.IsAiGenerated),
                Difficulty = quiz.Difficulty,
                Classification = quiz.Classification,
                CreatedAt = quiz.CreatedAt,
                // Deep classification
                BoneSpecialtyId = quiz.BoneSpecialtyId,
                BoneSpecialtyName = boneSpecialtyName,
                PathologyCategoryId = quiz.PathologyCategoryId,
                PathologyCategoryName = pathologyCategoryName
            };
        }
        public async Task<bool> DeleteQuizAsync(Guid quizId)
        {
            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(quizId);

            if (quiz == null)
                return false;

            // Nếu là quiz do Expert tạo, kiểm tra xem còn được gán vào lớp nào không
            if (quiz.CreatedByExpertId.HasValue)
            {
                var assignedCount = await _unitOfWork.ClassQuizSessionRepository
                    .GetQueryable()
                    .Where(cq => cq.QuizId == quizId)
                    .CountAsync();

                if (assignedCount > 0)
                    throw new InvalidOperationException("Cannot delete Expert Library quiz. You can only remove it from your classes.");

                // Đã gỡ hết khỏi các lớp → cho phép xóa
            }

            await _unitOfWork.QuizRepository.RemoveAsync(quiz);
            await _unitOfWork.SaveAsync();

            return true;
        }

        public async Task RemoveQuizFromClassAsync(Guid classId, Guid quizId)
        {
            var classQuizSession = await _unitOfWork.ClassQuizSessionRepository
                .FirstOrDefaultAsync(cqs => cqs.ClassId == classId && cqs.QuizId == quizId);

            if (classQuizSession == null)
                throw new KeyNotFoundException("Quiz is not assigned to this class.");

            await _unitOfWork.ClassQuizSessionRepository.DeleteAsync(classQuizSession.Id);
            await _unitOfWork.SaveAsync();
        }

        //================================================================================================================
        public async Task<List<GetQuizQuestionDTO>> GetQuizQuestionDTO(Guid quizId)
        {
            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(quizId);

            if (quiz == null)
                throw new KeyNotFoundException("Quiz not found.");

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
                    QuizId = q.QuizId,
                    QuizTitle = quiz.Title,
                    CaseTitle = caseTitle,

                    QuestionText = q.QuestionText,
                    Type = q.Type?.ToString(),

                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,

                    CorrectAnswer = q.CorrectAnswer,
                    ImageUrl = ResolveImageUrl(q.ImageUrl)
                });
            }

            return result;
        }
        public async Task<CreateQuizQuestionResponseDTO> CreateQuizQuestionAsync(Guid quizId, CreateQuizQuestionRequestDTO request)
        {
            var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId)
                ?? throw new KeyNotFoundException("Quiz not found.");

            MedicalCase? medicalCase = null;
            if (request.CaseId.HasValue)
            {
                medicalCase = await _unitOfWork.MedicalCaseRepository
                    .GetByIdAsync(request.CaseId.Value)
                    ?? throw new KeyNotFoundException("Medical case not found.");
            }

            var questionType = string.IsNullOrEmpty(request.Type)
                ? QuestionType.MultipleChoice
                : Enum.TryParse<QuestionType>(request.Type, out var parsed) ? parsed : QuestionType.MultipleChoice;

            var question = new QuizQuestion
            {
                QuizId = quizId,
                CaseId = request.CaseId,
                QuestionText = request.QuestionText,
                Type = questionType,
                OptionA = request.OptionA,
                OptionB = request.OptionB,
                OptionC = request.OptionC,
                OptionD = request.OptionD,
                CorrectAnswer = request.CorrectAnswer,
                ImageUrl = request.ImageUrl  // Lưu URL ảnh câu hỏi
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
                Type = question.Type?.ToString(),
                OptionA = question.OptionA,
                OptionB = question.OptionB,
                OptionC = question.OptionC,
                OptionD = question.OptionD,
                CorrectAnswer = question.CorrectAnswer,
                ImageUrl = ResolveImageUrl(question.ImageUrl)
            };
        }
        public async Task<UpdateQuizQuestionResponseDTO> UpdateQuizQuestionAsync(UpdateQuizQuestionRequestDTO update)
        {
            var question = await _unitOfWork.QuizQuestionRepository
                .GetByIdAsync(update.QuestionId);

            if (question == null)
                throw new KeyNotFoundException("Question not found.");

            // Update QuizId only if provided
            if (update.QuizId.HasValue)
            {
                var quiz = await _unitOfWork.QuizRepository
                    .GetByIdAsync(update.QuizId.Value);
                if (quiz == null)
                    throw new KeyNotFoundException("Không tìm thấy quiz.");
                question.QuizId = update.QuizId.Value;
            }

            // Update CaseId only if provided
            if (update.CaseId.HasValue)
            {
                var medicalCase = await _unitOfWork.MedicalCaseRepository
                    .GetByIdAsync(update.CaseId.Value);
                if (medicalCase == null)
                    throw new KeyNotFoundException("Medical case not found.");
                question.CaseId = update.CaseId;
            }

            question.QuestionText = update.QuestionText;
            var questionType = string.IsNullOrEmpty(update.Type)
                ? QuestionType.MultipleChoice
                : Enum.TryParse<QuestionType>(update.Type, out var parsed) ? parsed : QuestionType.MultipleChoice;
            question.Type = questionType;

            question.OptionA = update.OptionA;
            question.OptionB = update.OptionB;
            question.OptionC = update.OptionC;
            question.OptionD = update.OptionD;

            question.CorrectAnswer = update.CorrectAnswer;
            question.ImageUrl = update.ImageUrl;

            await _unitOfWork.QuizQuestionRepository.UpdateAsync(question);

            await _unitOfWork.SaveAsync();

            // Get related data for response
            var quizForResponse = await _unitOfWork.QuizRepository.GetByIdAsync(question.QuizId);
            string? caseTitle = null;
            if (question.CaseId.HasValue)
            {
                var medicalCaseForResponse = await _unitOfWork.MedicalCaseRepository
                    .GetByIdAsync(question.CaseId.Value);
                caseTitle = medicalCaseForResponse?.Title;
            }

            return new UpdateQuizQuestionResponseDTO
            {
                QuestionId = question.Id,
                QuestionText = question.QuestionText,
                QuizTitle = quizForResponse?.Title,
                CaseTitle = caseTitle,
                Type = question.Type?.ToString(),
                CorrectAnswer = question.CorrectAnswer,
                OptionA = question.OptionA,
                OptionB = question.OptionB,
                OptionC = question.OptionC,
                OptionD = question.OptionD,
                ImageUrl = ResolveImageUrl(question.ImageUrl)
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
                .GetQueryable()
                .Include(x => x.Quiz);

            var totalCount = await query.CountAsync();

            var sessions = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = sessions.Select(x => new ClassQuizSessionDTO
            {
                ClassId = x.ClassId,
                ClassName = x.Class.ClassName,

                QuizId = x.QuizId,
                QuizName = x.Quiz.Title,

                AssignedAt = x.CreatedAt,

                OpenTime = x.OpenTime,
                CloseTime = x.CloseTime,

                PassingScore = NormalizePassingScore(x.PassingScore, x.Quiz?.IsAiGenerated ?? true),
                TimeLimitMinutes = x.TimeLimitMinutes
            }).ToList();

            return new PagedResult<ClassQuizSessionDTO>
            {
                Items = result,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
        public async Task<ClassQuizSessionResponseDTO> AssignQuizToClassAsync(AssignQuizRequestDTO dto)
        {
            var academicClass = await _unitOfWork.AcademicClassRepository
                .GetByIdAsync(dto.ClassId)
                ?? throw new KeyNotFoundException("Class not found.");

            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(dto.QuizId)
                ?? throw new KeyNotFoundException("Quiz not found.");

            var existing = await _unitOfWork.ClassQuizSessionRepository
                .FirstOrDefaultAsync(cq => cq.ClassId == dto.ClassId && cq.QuizId == dto.QuizId);

            if (existing != null)
                throw new InvalidOperationException("This quiz has already been assigned to this class.");

            var openTime = ToUtc(dto.OpenTime) ?? quiz.OpenTime;
            var closeTime = ToUtc(dto.CloseTime) ?? quiz.CloseTime;

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
                ?? throw new KeyNotFoundException("Quiz attempt not found.");

            var quiz = await _unitOfWork.QuizRepository
                .GetByIdAsync(attempt.QuizId)
                ?? throw new KeyNotFoundException("Quiz not found.");

            var questions = await _unitOfWork.QuizQuestionRepository
                .FindAsync(q => q.QuizId == attempt.QuizId);

            if (!questions.Any())
                throw new InvalidOperationException("This quiz has no questions yet.");

            // ✅ FIX: Load student answers với Question để kiểm tra Type
            var studentAnswers = await _unitOfWork.Context.StudentQuizAnswers
                .Include(a => a.Question)
                .Where(a => a.AttemptId == attemptId)
                .ToListAsync();

            int totalQuestions = questions.Count;

            // ✅ FIX: Tính điểm theo ScoreAwarded (bao gồm essay đã chấm)
            var totalMaxScore = questions.Sum(q => q.MaxScore);
            var totalScoreAwarded = studentAnswers
                .Where(a => a.ScoreAwarded.HasValue)
                .Sum(a => a.ScoreAwarded.Value);

            float score = totalMaxScore == 0 ? 0 :
                (float)Math.Round((double)totalScoreAwarded / totalMaxScore * 100, 1);

            // Số câu đúng (chỉ MCQ/TF) - vẫn giữ cho thống kê
            int correctAnswers = studentAnswers.Count(a => a.IsCorrect == true);

            // Kiểm tra xem có essay nào chưa chấm không
            int ungradedEssayCount = studentAnswers.Count(a =>
                a.Question.Type == QuestionType.Essay && !a.IsGraded);

            int? normalizedPassingScore = NormalizePassingScore(quiz.PassingScore, quiz.IsAiGenerated);
            bool isPassed = normalizedPassingScore.HasValue && score >= normalizedPassingScore.Value;

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
                PassingScore = normalizedPassingScore,
                IsPassed = isPassed,
                CompletedAt = attempt.CompletedAt,
                // ✅ THÊM: Thông tin essay chưa chấm
                UngradedEssayCount = ungradedEssayCount
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

        //=====================================================   EXPERT QUIZZES FOR LECTURER  ==========================================================

        /// <summary>
        /// Lấy danh sách quiz từ thư viện của Expert cho Lecturer.
        /// 
        /// MÔ TẢ LUỒNG:
        /// 1. Expert tạo Quiz với N câu hỏi (ví dụ: 5 câu, 10 câu tùy bài học)
        /// 2. Quiz được lưu với CreatedByExpertId = Expert đã tạo
        /// 3. Lecturer muốn xem thư viện quiz để chọn quiz cho lớp mình
        /// 4. API này trả về danh sách quiz kèm số câu hỏi trong mỗi quiz
        /// 
        /// QUAN TRỌNG:
        /// - Chỉ trả về quiz có CreatedByExpertId != null (do Expert tạo)
        /// - Không trả về quiz của Lecturer tự tạo
        /// - Trả về QuestionCount để Lecturer biết quiz có bao nhiêu câu hỏi
        /// 
        /// FILTER:
        /// - topic: Lọc theo chủ đề (vd: "Lower Limb", "Chest X-Ray")
        /// - difficulty: Lọc theo độ khó (Easy, Medium, Hard)
        /// - classification: Lọc theo phân loại khóa học (vd: "Year 1", "Year 2")
        /// 
        /// TRẢ VỀ:
        /// - Danh sách quiz với thông tin: Id, Title, Topic, Difficulty, QuestionCount, ExpertName
        /// </summary>
        public async Task<PagedResult<ExpertQuizForLecturerDto>> GetExpertQuizzesForLecturerAsync(
            int pageIndex,
            int pageSize,
            string? topic = null,
            string? difficulty = null,
            string? classification = null)
        {
            var query = _unitOfWork.QuizRepository.GetQueryable();

            // ============================================
            // BƯỚC 1: Chỉ lấy quiz do Expert tạo
            // Quiz của Lecturer tự tạo sẽ có CreatedByExpertId = null
            // ============================================
            query = query.Where(q => q.CreatedByExpertId != null);

            // ============================================
            // BƯỚC 1.5: Lọc ra quiz được tạo thủ công bởi Expert (không phải AI)
            // Chỉ hiển thị quiz do Expert tạo thực tế, không phải quiz AI
            // ============================================
            query = query.Where(q => q.IsAiGenerated == false);

            // ============================================
            // BƯỚC 2: Áp dụng các bộ lọc tùy chọn
            // ============================================
            if (!string.IsNullOrWhiteSpace(topic))
            {
                // Tìm kiếm không phân biệt hoa thường
                query = query.Where(q => q.Topic != null && q.Topic.ToLower().Contains(topic.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(difficulty))
            {
                // Lọc chính xác theo độ khó
                query = query.Where(q => q.Difficulty == difficulty);
            }

            if (!string.IsNullOrWhiteSpace(classification))
            {
                // Lọc theo phân loại khóa học
                query = query.Where(q => q.Classification == classification);
            }

            // Đếm tổng số quiz thỏa điều kiện
            var totalCount = await query.CountAsync();

            // ============================================
            // BƯỚC 3: Lấy danh sách quiz với phân trang
            // ============================================
            var quizzes = await query
                .OrderByDescending(x => x.CreatedAt) // Mới nhất lên đầu
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new ExpertQuizForLecturerDto
                {
                    Id = q.Id,
                    Title = q.Title,
                    Topic = q.Topic,
                    OpenTime = q.OpenTime,
                    CloseTime = q.CloseTime,
                    TimeLimit = q.TimeLimit,
                PassingScore = NormalizePassingScore(q.PassingScore, q.IsAiGenerated),
                IsAiGenerated = q.IsAiGenerated,
                    Difficulty = q.Difficulty,
                    Classification = q.Classification,
                    CreatedAt = q.CreatedAt,
                    ExpertName = q.CreatedByExpert != null ? q.CreatedByExpert.FullName : null,
                    // QUAN TRỌNG: Đếm số câu hỏi trong quiz
                    // Đây là số câu hỏi mà Student sẽ nhận được khi làm quiz
                    QuestionCount = q.QuizQuestions.Count()
                })
                .ToListAsync();

            return new PagedResult<ExpertQuizForLecturerDto>
            {
                Items = quizzes,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Lấy danh sách câu hỏi trong một quiz (không có đáp án).
        /// 
        /// MÔ TẢ LUỒNG:
        /// 1. Lecturer đã xem danh sách quiz, chọn được quiz phù hợp
        /// 2. Trước khi gán vào lớp, Lecturer muốn xem trước các câu hỏi
        /// 3. API này trả về danh sách câu hỏi với các lựa chọn
        /// 
        /// BẢO MẬT:
        /// - KHÔNG trả về CorrectAnswer (đáp án đúng)
        /// - Chỉ trả về: QuestionText, OptionA, B, C, D, CaseTitle
        /// 
        /// KIỂM TRA:
        /// - Quiz phải tồn tại
        /// - Quiz phải do Expert tạo (CreatedByExpertId != null)
        /// 
        /// TRẢ VỀ:
        /// - Danh sách câu hỏi không có đáp án
        /// </summary>
        public async Task<List<ExpertQuizQuestionDto>> GetExpertQuizQuestionsAsync(Guid quizId)
        {
            var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId);
            if (quiz == null)
                throw new KeyNotFoundException("Không tìm thấy quiz.");

            // ============================================
            // BƯỚC 1: Kiểm tra quiz có phải do Expert tạo và KHÔNG PHẢI AI không
            // Chỉ cho phép xem câu hỏi từ quiz của Expert (không phải quiz AI)
            // ============================================
            if (quiz.CreatedByExpertId == null)
                throw new InvalidOperationException("Chỉ có thể xem câu hỏi từ quiz của Expert.");
            if (quiz.IsAiGenerated)
                throw new InvalidOperationException("Quiz này được tạo bởi AI và không nằm trong thư viện Expert.");

            // ============================================
            // BƯỚC 2: Lấy tất cả câu hỏi trong quiz
            // ============================================
            var questions = await _unitOfWork.QuizQuestionRepository
                .FindAsync(q => q.QuizId == quizId);

            var result = new List<ExpertQuizQuestionDto>();

            foreach (var q in questions)
            {
                string? caseTitle = null;
                if (q.CaseId.HasValue)
                {
                    var medicalCase = await _unitOfWork.MedicalCaseRepository.GetByIdAsync(q.CaseId.Value);
                    caseTitle = medicalCase?.Title;
                }

                // ============================================
                // BƯỚC 3: Map dữ liệu - CÓ CorrectAnswer + ImageUrl
                // Mỗi câu hỏi đều BẮT BUỘC có đáp án đúng khi được Expert tạo
                // CorrectAnswer là một trong: "A", "B", "C", "D"
                // ImageUrl là URL của ảnh đã upload lên Supabase
                // ============================================
                result.Add(new ExpertQuizQuestionDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText,
                    Type = q.Type?.ToString(),
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CaseTitle = caseTitle,
                    CorrectAnswer = q.CorrectAnswer,  // Đáp án đúng: A, B, C hoặc D
                    ImageUrl = ResolveImageUrl(q.ImageUrl)  // URL ảnh câu hỏi (absolute)
                });
            }

            return result;
        }

        /// <summary>
        /// Kiểm tra xem quiz đã được gán vào lớp nào chưa.
        /// 
        /// MỤC ĐÍCH:
        /// - Dùng để hiển thị warning cho Expert khi họ muốn edit quiz
        /// - Nếu quiz đã được assign, Expert sẽ thấy thông báo về các lớp đã gán
        /// - Expert vẫn có thể edit (vì họ là owner), nhưng sẽ biết impact
        /// 
        /// TRẢ VỀ:
        /// - IsAssigned: true nếu quiz đã được gán vào ít nhất 1 lớp
        /// - AssignedClassCount: số lượng lớp đã gán
        /// </summary>
        public async Task<(bool IsAssigned, int AssignedClassCount)> IsQuizAssignedAsync(Guid quizId)
        {
            var count = await _unitOfWork.ClassQuizSessionRepository
                .GetQueryable()
                .Where(cq => cq.QuizId == quizId)
                .CountAsync();

            return (count > 0, count);
        }

        /// <summary>
        /// Tạo bản copy của một Expert Quiz để Lecturer có thể tùy chỉnh.
        /// Quiz mới sẽ không có CreatedByExpertId và có thể edit câu hỏi.
        /// </summary>
        /// <param name="expertQuizId">ID của expert quiz cần copy</param>
        /// <param name="lecturerId">ID của lecturer đang thực hiện copy</param>
        /// <param name="newTitle">Tiêu đề mới cho quiz (optional)</param>
        /// <returns>Thông tin quiz đã được copy</returns>
        public async Task<CopiedExpertQuizDto> CopyExpertQuizForLecturerAsync(Guid expertQuizId, Guid lecturerId, string? newTitle = null)
        {
            var now = DateTime.UtcNow;

            // Lấy quiz gốc
            var originalQuiz = await _unitOfWork.QuizRepository.GetByIdAsync(expertQuizId)
                ?? throw new KeyNotFoundException("Expert quiz không tồn tại.");

            // Kiểm tra quiz phải do Expert tạo
            if (originalQuiz.CreatedByExpertId == null)
                throw new InvalidOperationException("Chỉ có thể copy quiz từ thư viện Expert.");

            // Tạo quiz mới (không có CreatedByExpertId để có thể edit)
            var newQuiz = new Quiz
            {
                Id = Guid.NewGuid(),
                Title = string.IsNullOrWhiteSpace(newTitle) ? originalQuiz.Title : newTitle,
                Topic = originalQuiz.Topic,
                Difficulty = originalQuiz.Difficulty,
                Classification = originalQuiz.Classification,
                IsAiGenerated = false,
                IsVerifiedCurriculum = false,
                CreatedByExpertId = null, // Quan trọng: Lecturer có thể edit
                OpenTime = null,
                CloseTime = null,
                TimeLimit = originalQuiz.TimeLimit,
                PassingScore = originalQuiz.PassingScore,
                CreatedAt = now
            };

            await _unitOfWork.QuizRepository.AddAsync(newQuiz);
            await _unitOfWork.SaveAsync();

            // Lấy tất cả câu hỏi từ quiz gốc
            var originalQuestions = await _unitOfWork.QuizQuestionRepository
                .FindAsync(q => q.QuizId == expertQuizId);

            // Copy từng câu hỏi
            foreach (var originalQ in originalQuestions)
            {
                var newQuestion = new QuizQuestion
                {
                    Id = Guid.NewGuid(),
                    QuizId = newQuiz.Id,
                    QuestionText = originalQ.QuestionText,
                    Type = originalQ.Type,
                    OptionA = originalQ.OptionA,
                    OptionB = originalQ.OptionB,
                    OptionC = originalQ.OptionC,
                    OptionD = originalQ.OptionD,
                    CorrectAnswer = originalQ.CorrectAnswer,
                    ImageUrl = originalQ.ImageUrl,
                    CaseId = originalQ.CaseId,
                    MaxScore = originalQ.MaxScore,
                    ReferenceAnswer = originalQ.ReferenceAnswer
                };

                await _unitOfWork.QuizQuestionRepository.AddAsync(newQuestion);
            }

            await _unitOfWork.SaveAsync();

            return new CopiedExpertQuizDto
            {
                NewQuizId = newQuiz.Id,
                NewQuizTitle = newQuiz.Title,
                OriginalQuizId = originalQuiz.Id,
                OriginalQuizTitle = originalQuiz.Title,
                QuestionCount = originalQuestions.Count,
                CreatedAt = now
            };
        }

        //================================================================================================================
        // Deep Classification - Lấy dữ liệu cho dropdown trong Create/Edit Quiz
        //================================================================================================================

        /// <summary>
        /// Lấy danh sách Bone Specialty dạng tree (hierarchical) để hiển thị dropdown.
        /// </summary>
        public async Task<List<BoneSpecialtyTreeDto>> GetBoneSpecialtiesTreeAsync()
        {
            var all = await _unitOfWork.Context.BoneSpecialties
                .Where(bs => bs.IsActive)
                .OrderBy(bs => bs.DisplayOrder)
                .ThenBy(bs => bs.Name)
                .ToListAsync();

            return BuildBoneSpecialtyTree(all, null, 0);
        }

        private List<BoneSpecialtyTreeDto> BuildBoneSpecialtyTree(List<Repositories.Models.BoneSpecialty> all, Guid? parentId, int level)
        {
            var result = new List<BoneSpecialtyTreeDto>();

            var children = all.Where(bs => bs.ParentId == parentId).ToList();

            foreach (var item in children)
            {
                var dto = new BoneSpecialtyTreeDto
                {
                    Id = item.Id,
                    Code = item.Code,
                    Name = item.Name,
                    ParentId = item.ParentId,
                    ParentName = item.Parent?.Name,
                    Description = item.Description,
                    DisplayOrder = item.DisplayOrder,
                    IsActive = item.IsActive,
                    Level = level,
                    Children = BuildBoneSpecialtyTree(all, item.Id, level + 1)
                };
                result.Add(dto);
            }

            return result;
        }

        /// <summary>
        /// Lấy danh sách Pathology Category dạng flat list để hiển thị dropdown.
        /// </summary>
        public async Task<List<PathologyCategorySimpleDto>> GetPathologyCategoriesAsync()
        {
            var categories = await _unitOfWork.Context.PathologyCategories
                .Include(pc => pc.BoneSpecialty)
                .Where(pc => pc.IsActive)
                .OrderBy(pc => pc.DisplayOrder)
                .ThenBy(pc => pc.Name)
                .ToListAsync();

            return categories.Select(pc => new PathologyCategorySimpleDto
            {
                Id = pc.Id,
                Code = pc.Code,
                Name = pc.Name,
                BoneSpecialtyId = pc.BoneSpecialtyId,
                BoneSpecialtyName = pc.BoneSpecialty?.Name,
                Description = pc.Description,
                DisplayOrder = pc.DisplayOrder,
                IsActive = pc.IsActive
            }).ToList();
        }
    }
}
