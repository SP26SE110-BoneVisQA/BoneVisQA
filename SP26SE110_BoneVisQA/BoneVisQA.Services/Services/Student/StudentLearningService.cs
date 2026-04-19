using System;
using System.Linq;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Quiz;
using BoneVisQA.Services.Models.Student;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services.Student;

public class StudentLearningService : IStudentLearningService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<StudentLearningService> _logger;

    public StudentLearningService(IUnitOfWork unitOfWork, IEmailService emailService, ILogger<StudentLearningService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Passing score luôn ở thang 100 (0-100). Identity function.
    /// </summary>
    private static int? NormalizePassingScore(int? passingScore, bool isAiGenerated)
    {
        return passingScore;
    }

    public async Task<QuizSessionDto> GetPracticeQuizAsync(Guid studentId, string? topic)
    {
        var utcNow = DateTime.UtcNow;
        var classIds = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        // 1. Tìm quiz AI-generated theo topic (ưu tiên cao nhất)
        if (!string.IsNullOrWhiteSpace(topic))
        {
            var normalizedTopic = topic.Trim().ToLower();
            var aiQuiz = await _unitOfWork.Context.Quizzes
                .AsNoTracking()
                .Include(q => q.QuizQuestions)
                    .ThenInclude(qq => qq.Case)
                        .ThenInclude(c => c!.Category)
                .Where(q => q.IsAiGenerated && q.Topic != null && q.Topic.ToLower() == normalizedTopic)
                .Where(q => q.QuizQuestions.Any())
                .FirstOrDefaultAsync();

            if (aiQuiz != null)
                return await CreateSessionFromQuizAsync(aiQuiz, studentId);
        }

        // 2. Fallback: Tìm quiz lecturer theo topic (is_ai_generated = false)
        var query = _unitOfWork.Context.Quizzes
            .AsNoTracking()
            .Include(q => q.QuizQuestions)
                .ThenInclude(qq => qq.Case)
                    .ThenInclude(c => c!.Category)
            .Include(q => q.ClassQuizSessions)
            .Where(q => q.ClassQuizSessions.Any(cqs =>
                classIds.Contains(cqs.ClassId) &&
                ((cqs.OpenTime ?? q.OpenTime) == null || (cqs.OpenTime ?? q.OpenTime) <= utcNow) &&
                ((cqs.CloseTime ?? q.CloseTime) == null || (cqs.CloseTime ?? q.CloseTime) > utcNow)))
            .Where(q => !q.IsAiGenerated)
            .Where(q => q.QuizQuestions.Any());

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var normalizedTopic = topic.Trim().ToLower();
            query = query.Where(q =>
                q.Topic != null && q.Topic.ToLower() == normalizedTopic ||
                q.Title.ToLower().Contains(normalizedTopic) ||
                q.QuizQuestions.Any(qq =>
                    qq.QuestionText.ToLower().Contains(normalizedTopic) ||
                    (qq.Case != null && qq.Case.Title.ToLower().Contains(normalizedTopic)) ||
                    (qq.Case != null && qq.Case.Category != null && qq.Case.Category.Name.ToLower() == normalizedTopic)));
        }

        var candidateQuizzes = await query.ToListAsync();
        if (candidateQuizzes.Count > 0)
        {
            var quiz = candidateQuizzes[Random.Shared.Next(candidateQuizzes.Count)];
            var classSession = quiz.ClassQuizSessions
                .FirstOrDefault(cqs => classIds.Contains(cqs.ClassId));
            var shuffleSetting = classSession?.ShuffleQuestions ?? false;
            return await CreateSessionFromQuizAsync(quiz, studentId, shuffleSetting, classSession);
        }

        // 3. Fallback cuối: Tìm bất kỳ quiz nào (AI hoặc lecturer)
        var anyQuiz = await _unitOfWork.Context.Quizzes
            .AsNoTracking()
            .Include(q => q.QuizQuestions)
                .ThenInclude(qq => qq.Case)
                    .ThenInclude(c => c!.Category)
            .Include(q => q.ClassQuizSessions)
            .Where(q => q.ClassQuizSessions.Any(cqs =>
                classIds.Contains(cqs.ClassId) &&
                ((cqs.OpenTime ?? q.OpenTime) == null || (cqs.OpenTime ?? q.OpenTime) <= utcNow) &&
                ((cqs.CloseTime ?? q.CloseTime) == null || (cqs.CloseTime ?? q.CloseTime) >= utcNow)))
            .Where(q => q.QuizQuestions.Any())
            .FirstOrDefaultAsync();

        if (anyQuiz != null)
        {
            var classSession = anyQuiz.ClassQuizSessions
                .FirstOrDefault(cqs => classIds.Contains(cqs.ClassId));
            var shuffleSetting = classSession?.ShuffleQuestions ?? false;
            return await CreateSessionFromQuizAsync(anyQuiz, studentId, shuffleSetting, classSession);
        }

        throw new KeyNotFoundException("No suitable practice quiz found.");
    }

    /// <summary>
    /// Xóa đáp án cũ và mở lại attempt (retake). Dùng khi DB chỉ cho phép một quiz_attempts / (student, quiz).
    /// </summary>
    private static async Task ResetQuizAttemptForRetakeAsync(
        IUnitOfWork unitOfWork,
        BoneVisQA.Repositories.Models.QuizAttempt attempt)
    {
        var rows = await unitOfWork.Context.StudentQuizAnswers
            .Where(a => a.AttemptId == attempt.Id)
            .ToListAsync();
        unitOfWork.Context.StudentQuizAnswers.RemoveRange(rows);
        attempt.CompletedAt = null;
        attempt.Score = null;
        attempt.StartedAt = DateTime.UtcNow;
        await unitOfWork.QuizAttemptRepository.UpdateAsync(attempt);
        await unitOfWork.SaveAsync();
    }

    private async Task<QuizSessionDto> CreateSessionFromQuizAsync(
        BoneVisQA.Repositories.Models.Quiz quiz,
        Guid studentId,
        bool shuffleQuestions = false,
        ClassQuizSession? classSession = null)
    {
        var attempt = await _unitOfWork.Context.QuizAttempts
            .Include(a => a.StudentQuizAnswers)
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.QuizId == quiz.Id);

        if (attempt == null)
        {
            attempt = new BoneVisQA.Repositories.Models.QuizAttempt
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                QuizId = quiz.Id,
                StartedAt = DateTime.UtcNow
            };

            await _unitOfWork.QuizAttemptRepository.AddAsync(attempt);
            await _unitOfWork.SaveAsync();
        }
        else if (attempt.CompletedAt.HasValue)
        {
            // DB unique (student_id, quiz_id): không thể thêm attempt thứ 2 — reset hàng hiện có để retake.
            await ResetQuizAttemptForRetakeAsync(_unitOfWork, attempt);
        }

        var questions = quiz.QuizQuestions.AsEnumerable();

        if (shuffleQuestions)
            questions = questions.OrderBy(_ => Random.Shared.Next());

        return new QuizSessionDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            Title = quiz.Title,
            Topic = quiz.Topic,
            TimeLimit = classSession?.TimeLimitMinutes ?? quiz.TimeLimit,
            Questions = questions
                .Select(q => new StudentQuizQuestionDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText,
                    Type = q.Type?.ToString(),
                    CaseId = q.CaseId,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    ImageUrl = q.ImageUrl,
                    MaxScore = q.MaxScore,
                    ReferenceAnswer = q.ReferenceAnswer
                })
                .ToList()
        };
    }

    public async Task<QuizResultDto> SubmitQuizAttemptAsync(Guid studentId, SubmitQuizRequestDto request)
    {
        var attempt = await _unitOfWork.Context.QuizAttempts
            .Include(a => a.Quiz)
            .Include(a => a.StudentQuizAnswers)
                .ThenInclude(sa => sa.Question)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId && a.StudentId == studentId)
            ?? throw new KeyNotFoundException("Quiz attempt not found.");

        if (attempt.CompletedAt.HasValue)
            throw new InvalidOperationException("This quiz has already been submitted.");

        if (attempt.Quiz == null)
            throw new KeyNotFoundException("Quiz not found.");

        // Quiz AI tự tạo không gắn ClassQuizSession — chỉ kiểm tra cửa sổ nộp cho quiz lớp.
        if (!attempt.Quiz.IsAiGenerated)
        {
            var utcNow = DateTime.UtcNow;
            var classIds = await _unitOfWork.Context.ClassEnrollments
                .Where(e => e.StudentId == studentId)
                .Select(e => e.ClassId)
                .ToListAsync();

            var session = await _unitOfWork.Context.ClassQuizSessions
                .AsNoTracking()
                .Include(cqs => cqs.Quiz)
                .FirstOrDefaultAsync(cqs =>
                    cqs.QuizId == attempt.QuizId &&
                    classIds.Contains(cqs.ClassId));

            if (session == null)
                throw new InvalidOperationException("This quiz is not assigned through a class.");

            // Kiểm tra quiz đã đóng chưa
            var effectiveCloseTime = session.CloseTime ?? session.Quiz?.CloseTime;
            if (effectiveCloseTime.HasValue && effectiveCloseTime.Value < utcNow)
                throw new InvalidOperationException("The quiz is closed. Submission is not allowed.");
        }

        var quiz = await _unitOfWork.Context.Quizzes
            .Include(q => q.QuizQuestions)
            .FirstOrDefaultAsync(q => q.Id == attempt.QuizId)
            ?? throw new KeyNotFoundException("Quiz not found.");

        var questionMap = quiz.QuizQuestions.ToDictionary(q => q.Id, q => q);
        var incomingAnswers = request.Answers
            .GroupBy(a => a.QuestionId)
            .Select(g => g.Last())
            .ToList();

        foreach (var answer in incomingAnswers)
        {
            if (!questionMap.TryGetValue(answer.QuestionId, out var question))
                throw new InvalidOperationException("An answer not belonging to this quiz was detected.");

            var existing = attempt.StudentQuizAnswers.FirstOrDefault(a => a.QuestionId == answer.QuestionId);

            // Handle different question types
            if (question.Type == QuestionType.Essay)
            {
                // Essay: store essay answer, no auto-grading
                if (existing == null)
                {
                    existing = new StudentQuizAnswer
                    {
                        Id = Guid.NewGuid(),
                        AttemptId = attempt.Id,
                        QuestionId = answer.QuestionId,
                        EssayAnswer = answer.EssayAnswer,
                        StudentAnswer = null,
                        IsCorrect = null,
                        ScoreAwarded = null,
                        IsGraded = false
                    };
                    await _unitOfWork.StudentQuizAnswerRepository.AddAsync(existing);
                    attempt.StudentQuizAnswers.Add(existing);
                }
                else
                {
                    existing.EssayAnswer = answer.EssayAnswer;
                    existing.StudentAnswer = null;
                    existing.IsCorrect = null;
                    // Don't change ScoreAwarded or IsGraded if already graded
                    await _unitOfWork.StudentQuizAnswerRepository.UpdateAsync(existing);
                }
            }
            else // MultipleChoice: auto-grade
            {
                var isCorrect = QuizAnswerMatchesCorrect(question.CorrectAnswer, answer.StudentAnswer);

                if (existing == null)
                {
                    existing = new StudentQuizAnswer
                    {
                        Id = Guid.NewGuid(),
                        AttemptId = attempt.Id,
                        QuestionId = answer.QuestionId,
                        StudentAnswer = answer.StudentAnswer,
                        EssayAnswer = null,
                        IsCorrect = isCorrect,
                        ScoreAwarded = isCorrect ? question.MaxScore : 0,
                        IsGraded = true // Auto-graded
                    };
                    await _unitOfWork.StudentQuizAnswerRepository.AddAsync(existing);
                    attempt.StudentQuizAnswers.Add(existing);
                }
                else
                {
                    existing.StudentAnswer = answer.StudentAnswer;
                    existing.EssayAnswer = null;
                    existing.IsCorrect = isCorrect;
                    existing.ScoreAwarded = isCorrect ? question.MaxScore : 0;
                    existing.IsGraded = true;
                    await _unitOfWork.StudentQuizAnswerRepository.UpdateAsync(existing);
                }
            }
        }

        // Ensure all questions have an answer entry (for unanswered questions)
        foreach (var q in quiz.QuizQuestions)
        {
            if (!attempt.StudentQuizAnswers.Any(a => a.QuestionId == q.Id))
            {
                // Unanswered question
                if (q.Type == QuestionType.Essay)
                {
                    var unansweredEssay = new StudentQuizAnswer
                    {
                        Id = Guid.NewGuid(),
                        AttemptId = attempt.Id,
                        QuestionId = q.Id,
                        EssayAnswer = null,
                        StudentAnswer = null,
                        IsCorrect = null,
                        ScoreAwarded = null,
                        IsGraded = false
                    };
                    await _unitOfWork.StudentQuizAnswerRepository.AddAsync(unansweredEssay);
                    attempt.StudentQuizAnswers.Add(unansweredEssay);
                }
                else
                {
                    var unansweredMcTf = new StudentQuizAnswer
                    {
                        Id = Guid.NewGuid(),
                        AttemptId = attempt.Id,
                        QuestionId = q.Id,
                        StudentAnswer = null,
                        EssayAnswer = null,
                        IsCorrect = false,
                        ScoreAwarded = 0,
                        IsGraded = true
                    };
                    await _unitOfWork.StudentQuizAnswerRepository.AddAsync(unansweredMcTf);
                    attempt.StudentQuizAnswers.Add(unansweredMcTf);
                }
            }
        }

        // Calculate score: sum of ScoreAwarded / sum of MaxScore * 100
        var totalMaxScore = quiz.QuizQuestions.Sum(q => q.MaxScore);
        var totalScoreAwarded = attempt.StudentQuizAnswers
            .Where(a => a.ScoreAwarded.HasValue)
            .Sum(a => a.ScoreAwarded.Value);

        var score = totalMaxScore == 0 ? 0 : (double)(totalScoreAwarded * 100 / totalMaxScore);
        attempt.Score = score;
        attempt.CompletedAt = DateTime.UtcNow;
        await _unitOfWork.QuizAttemptRepository.UpdateAsync(attempt);
        await _unitOfWork.SaveAsync();

        // Send email notification if quiz contains essay questions
        try
        {
            var hasEssay = quiz.QuizQuestions.Any(q => q.Type == QuestionType.Essay);
            if (hasEssay)
            {
                // Get lecturers for the class
                var classIds = await _unitOfWork.Context.ClassEnrollments
                    .Where(e => e.StudentId == studentId)
                    .Select(e => e.ClassId)
                    .ToListAsync();

                foreach (var classId in classIds.Distinct())
                {
                    var academicClass = await _unitOfWork.Context.AcademicClasses
                        .FirstOrDefaultAsync(c => c.Id == classId);

                    if (academicClass != null)
                    {
                        var lecturerIds = new List<Guid?>();
                        if (academicClass.LecturerId.HasValue)
                            lecturerIds.Add(academicClass.LecturerId);
                        if (academicClass.ExpertId.HasValue)
                            lecturerIds.Add(academicClass.ExpertId);

                        foreach (var lecturerId in lecturerIds.Distinct().Where(id => id.HasValue).Cast<Guid>())
                        {
                            var lecturer = await _unitOfWork.Context.Users
                                .FirstOrDefaultAsync(u => u.Id == lecturerId);

                            if (lecturer != null && !string.IsNullOrEmpty(lecturer.Email))
                            {
                                var attemptDetailUrl = $"/lecturer/classes/{classId}/assignments/quizzes/{quiz.Id}/attempts/{attempt.Id}";
                                await _emailService.SendEssaySubmittedNotificationAsync(
                                    lecturer.Email,
                                    lecturer.FullName ?? "Lecturer",
                                    attempt.Student?.FullName ?? "Student",
                                    quiz.Title,
                                    academicClass.ClassName,
                                    attemptDetailUrl);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StudentLearningService] Failed to send essay submission notification");
        }

        // Chuẩn hóa PassingScore về thang 100 trước khi so sánh
        int? normalizedPassingScore = NormalizePassingScore(quiz.PassingScore, quiz.IsAiGenerated);

        // Đếm số essay chưa chấm (cần load Question)
        int ungradedEssayCount = attempt.StudentQuizAnswers.Count(a =>
            a.Question.Type == QuestionType.Essay && !a.IsGraded);

        return new QuizResultDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            Score = score,
            PassingScore = normalizedPassingScore,
            Passed = !normalizedPassingScore.HasValue || score >= normalizedPassingScore.Value,
            TotalQuestions = quiz.QuizQuestions.Count,
            CorrectAnswers = quiz.QuizQuestions.Count(q =>
                attempt.StudentQuizAnswers.FirstOrDefault(a => a.QuestionId == q.Id)?.IsCorrect == true),
            UngradedEssayCount = ungradedEssayCount
        };
    }

    private static bool QuizAnswerMatchesCorrect(string? correctAnswer, string? studentAnswer)
    {
        return string.Equals(
            studentAnswer?.Trim(),
            correctAnswer?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// Lưu quiz AI vào DB (tạo Quiz + QuizAttempt mới), trả về session để student bắt đầu làm.
    public async Task<StudentGeneratedQuizAttemptDto> SaveAndStartGeneratedQuizAsync(
        Guid studentId,
        AIQuizGenerationResultDto generated,
        string? topic,
        string? difficulty)
    {
        if (!generated.Success || generated.Questions.Count == 0)
            throw new InvalidOperationException("There are no questions to save.");

        // 1. Tạo Quiz record
        var quiz = new BoneVisQA.Repositories.Models.Quiz
        {
            Id = Guid.NewGuid(),
            Title = $"AI Quiz: {topic ?? "Practice"} {(difficulty != null ? $"({difficulty})" : "")}",
            IsAiGenerated = true,
            Topic = topic,
            Difficulty = difficulty,
            PassingScore = 70,
            TimeLimit = 30,
            CreatedAt = DateTime.UtcNow,
            CreatedByExpertId = studentId,
        };
        await _unitOfWork.QuizRepository.AddAsync(quiz);

        // 2. Tạo QuizQuestion records
        foreach (var q in generated.Questions)
        {
            QuestionType questionType;
            if (string.IsNullOrEmpty(q.Type) || !Enum.TryParse<QuestionType>(q.Type, out questionType))
            {
                questionType = QuestionType.MultipleChoice; // default
            }

            var question = new BoneVisQA.Repositories.Models.QuizQuestion
            {
                Id = Guid.NewGuid(),
                QuizId = quiz.Id,
                QuestionText = q.QuestionText,
                Type = questionType,
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD,
                CorrectAnswer = q.CorrectAnswer,
                CaseId = q.CaseId,
                ImageUrl = q.ImageUrl,
            };
            await _unitOfWork.QuizQuestionRepository.AddAsync(question);
        }

        // 3. Tạo QuizAttempt (chưa nộp)
        var attempt = new BoneVisQA.Repositories.Models.QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            StudentId = studentId,
            StartedAt = DateTime.UtcNow,
            Score = null,
            CompletedAt = null,
        };
        await _unitOfWork.QuizAttemptRepository.AddAsync(attempt);
        await _unitOfWork.SaveAsync();

        // 4. Load questions để trả về
        var questions = await _unitOfWork.Context.QuizQuestions
            .AsNoTracking()
            .Where(q => q.QuizId == quiz.Id)
            .OrderBy(q => q.Id)
            .Select(q => new StudentQuizQuestionDto
            {
                QuestionId = q.Id,
                QuestionText = q.QuestionText,
                Type = q.Type.HasValue ? q.Type.Value.ToString() : null,
                CaseId = q.CaseId,
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD,
                ImageUrl = q.ImageUrl,
                MaxScore = q.MaxScore,
                ReferenceAnswer = q.ReferenceAnswer
            })
            .ToListAsync();

        return new StudentGeneratedQuizAttemptDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            Title = quiz.Title,
            Topic = topic,
            Questions = questions,
            SavedToHistory = true,
        };
    }

    /// Trả về lịch sử tất cả quiz attempt của student (gồm quiz giao + quiz AI tự tạo).
    public async Task<IReadOnlyList<StudentQuizAttemptSummaryDto>> GetQuizAttemptHistoryAsync(Guid studentId)
    {
        var attempts = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.CompletedAt)
            .ToListAsync();

        var classIds = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        var classQuizSessions = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Where(cqs => classIds.Contains(cqs.ClassId))
            .ToListAsync();

        var result = new List<StudentQuizAttemptSummaryDto>();

        foreach (var attempt in attempts)
        {
            if (attempt.Quiz == null) continue;

            var classSession = classQuizSessions.FirstOrDefault(cqs => cqs.QuizId == attempt.QuizId);

            var summary = new StudentQuizAttemptSummaryDto
            {
                AttemptId = attempt.Id,
                QuizId = attempt.QuizId,
                QuizTitle = attempt.Quiz.Title,
                Topic = attempt.Quiz.Topic,
                Difficulty = attempt.Quiz.Difficulty,
                ClassName = classSession != null ? await _unitOfWork.Context.AcademicClasses
                    .Where(c => c.Id == classSession.ClassId)
                    .Select(c => c.ClassName)
                    .FirstOrDefaultAsync() : null,
                StartedAt = attempt.StartedAt,
                CompletedAt = attempt.CompletedAt,
                Score = attempt.Score,
                PassingScore = attempt.Quiz.PassingScore,
                Passed = attempt.Score.HasValue && attempt.Quiz.PassingScore.HasValue
                    ? attempt.Score >= attempt.Quiz.PassingScore
                    : false,
                TotalQuestions = attempt.Quiz.QuizQuestions.Count,
                CorrectAnswers = attempt.StudentQuizAnswers.Count(a => a.IsCorrect == true),
                IsAiGenerated = attempt.Quiz.IsAiGenerated
            };

            result.Add(summary);
        }

        return result;
    }

    /// <summary>
    /// Trả về chi tiết 1 lần làm quiz để student xem lại đáp án.
    /// </summary>
    public async Task<QuizAttemptReviewDto> GetQuizAttemptReviewAsync(Guid studentId, Guid attemptId)
    {
        var attempt = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
                .ThenInclude(q => q.QuizQuestions)
            .Include(a => a.StudentQuizAnswers)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.StudentId == studentId)
            ?? throw new KeyNotFoundException("Không tìm thấy lần làm quiz.");

        if (attempt.Quiz == null)
            throw new KeyNotFoundException("Không tìm thấy quiz.");

        var totalQuestions = attempt.Quiz.QuizQuestions.Count;
        var correctCount = attempt.StudentQuizAnswers.Count(a => a.IsCorrect == true);
        var score = attempt.Score ?? 0;
        var passingScore = NormalizePassingScore(attempt.Quiz.PassingScore, attempt.Quiz.IsAiGenerated);

        var questionDtos = new List<QuestionReviewItemDto>();

        foreach (var question in attempt.Quiz.QuizQuestions)
        {
            var answer = attempt.StudentQuizAnswers.FirstOrDefault(a => a.QuestionId == question.Id);

            var questionDto = new QuestionReviewItemDto
            {
                QuestionId = question.Id,
                QuestionText = question.QuestionText,
                Type = question.Type?.ToString(),
                OptionA = question.OptionA,
                OptionB = question.OptionB,
                OptionC = question.OptionC,
                OptionD = question.OptionD,
                StudentAnswer = answer?.StudentAnswer,
                EssayAnswer = answer?.EssayAnswer,
                CorrectAnswer = question.CorrectAnswer,
                IsCorrect = answer?.IsCorrect ?? false,
                ImageUrl = question.ImageUrl,
                CaseId = question.CaseId?.ToString(),
                ScoreAwarded = answer?.ScoreAwarded,
                LecturerFeedback = answer?.LecturerFeedback,
                IsGraded = answer?.IsGraded ?? (question.Type == QuestionType.Essay ? false : true),
                MaxScore = question.MaxScore
            };

            questionDtos.Add(questionDto);
        }

        return new QuizAttemptReviewDto
        {
            AttemptId = attempt.Id,
            QuizTitle = attempt.Quiz.Title,
            Score = score,
            TotalQuestions = totalQuestions,
            CorrectAnswers = correctCount,
            Passed = passingScore.HasValue ? score >= passingScore.Value : true,
            PassingScore = passingScore,
            Questions = questionDtos
        };
    }

    public async Task<StudentProgressDto> GetProgressSummaryAsync(Guid studentId)
    {
        // TODO: Implement progress summary logic
        return new StudentProgressDto
        {
            TotalCasesViewed = 0,
            TotalQuestionsAsked = 0,
            QuizzesCompleted = 0,
            TotalQuizAnswersSubmitted = 0,
            AvgQuizScore = null,
            TotalQuizAttempts = 0,
            CompletedQuizzes = 0,
            EscalatedAnswers = 0,
            LatestQuizScore = null,
            QuizAccuracyRate = null
        };
    }

    public async Task<IReadOnlyList<StudentTopicStatDto>> GetTopicStatsAsync(Guid studentId)
    {
        // TODO: Implement topic statistics
        return new List<StudentTopicStatDto>();
    }

    public async Task<IReadOnlyList<StudentRecentActivityDto>> GetRecentActivityAsync(Guid studentId)
    {
        var recentQuestions = await _unitOfWork.Context.QaMessages
            .AsNoTracking()
            .Include(m => m.Session)
                .ThenInclude(s => s.Case)
                    .ThenInclude(c => c!.Category)
            .Where(m => m.Role == "User" && m.Session.StudentId == studentId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .Select(m => new StudentRecentActivityDto
            {
                ActivityType = "visual_qa",
                Title = m.Session.Case != null ? $"Asked a question on {m.Session.Case.Title}" : "Asked a visual QA question",
                Description = m.Content,
                Topic = m.Session.Case != null
                    ? m.Session.Case.Category != null
                        ? m.Session.Case.Category.Name
                        : m.Session.Case.Title
                    : "Personal Upload",
                OccurredAt = m.CreatedAt,
                SessionId = m.SessionId,
                TargetUrl = "/student/qa/image?sessionId=" + m.SessionId.ToString()
            })
            .ToListAsync();

        var recentQuizAttempts = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
                .ThenInclude(q => q.QuizQuestions)
                    .ThenInclude(qq => qq.Case)
                        .ThenInclude(c => c!.Category)
            .Where(a => a.StudentId == studentId && a.CompletedAt.HasValue)
            .OrderByDescending(a => a.CompletedAt)
            .Take(10)
            .ToListAsync();

        var recentQuizzes = recentQuizAttempts.Select(a => new StudentRecentActivityDto
        {
            ActivityType = "Quiz",
            Title = $"Completed Quiz {a.Quiz.Title}",
            Description = a.Score.HasValue
                ? $"Completed Quiz {a.Quiz.Title} with {a.Score.Value:F0}%"
                : $"Completed Quiz {a.Quiz.Title}",
            Topic = InferQuizTopic(a),
            OccurredAt = a.CompletedAt ?? a.StartedAt ?? DateTime.MinValue
        }).ToList();

        return recentQuestions
            .Concat(recentQuizzes)
            .OrderByDescending(x => x.OccurredAt)
            .Take(10)
            .ToList();
    }

    private static string InferQuizTopic(QuizAttempt a)
    {
        var firstQuestion = a.Quiz.QuizQuestions.FirstOrDefault();
        var caseEntity = firstQuestion?.Case;
        if (!string.IsNullOrWhiteSpace(caseEntity?.Category?.Name))
            return caseEntity!.Category!.Name!;
        if (!string.IsNullOrWhiteSpace(caseEntity?.Title))
            return caseEntity!.Title;
        if (!string.IsNullOrWhiteSpace(a.Quiz.Title))
            return a.Quiz.Title;
        return "Quiz";
    }

    public async Task AutoCloseExpiredAttemptsAsync()
    {
        // TODO: Implement auto-close for expired attempts
    }

    public async Task DeleteQuizAttemptAsync(Guid studentId, Guid attemptId)
    {
        var attempt = await _unitOfWork.Context.QuizAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.StudentId == studentId)
            ?? throw new KeyNotFoundException("Không tìm thấy lần làm quiz.");

        // Delete related answers first
        var answers = await _unitOfWork.Context.StudentQuizAnswers
            .Where(a => a.AttemptId == attemptId)
            .ToListAsync();
        _unitOfWork.Context.StudentQuizAnswers.RemoveRange(answers);

        // Delete attempt
        _unitOfWork.Context.QuizAttempts.Remove(attempt);
        await _unitOfWork.SaveAsync();
    }
}
