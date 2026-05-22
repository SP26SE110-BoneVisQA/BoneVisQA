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
                return await CreateSessionFromQuizAsync(aiQuiz, studentId, false, false, null);
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
            var shuffleOptionsSetting = classSession?.ShuffleOptions ?? false;
            return await CreateSessionFromQuizAsync(quiz, studentId, shuffleSetting, shuffleOptionsSetting, classSession);
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
            var shuffleOptionsSetting = classSession?.ShuffleOptions ?? false;
            return await CreateSessionFromQuizAsync(anyQuiz, studentId, shuffleSetting, shuffleOptionsSetting, classSession);
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
        bool shuffleOptions = false,
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

        // Check if practice mode
        var quizMode = classSession?.QuizMode ?? quiz.QuizMode;
        var isPracticeMode = quizMode == 2; // 2 = Practice mode
        var allowHints = isPracticeMode;

        var questions = quiz.QuizQuestions.AsEnumerable();

        if (shuffleQuestions)
            questions = questions.OrderBy(_ => Random.Shared.Next());

        return new QuizSessionDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            Title = quiz.Title,
            Topic = quiz.Topic,
            QuizMode = classSession?.QuizMode ?? quiz.QuizMode,
            TimeLimit = classSession?.TimeLimitMinutes ?? quiz.TimeLimit,
            AllowHints = allowHints,
            Questions = questions
                .Select(q =>
                {
                    // Shuffle options if enabled
                    string? optA = q.OptionA;
                    string? optB = q.OptionB;
                    string? optC = q.OptionC;
                    string? optD = q.OptionD;

                    if (shuffleOptions && (q.Type == QuestionType.MultipleChoice || q.Type == QuestionType.MultiSelect))
                    {
                        var options = new List<(string key, string value)>();
                        if (!string.IsNullOrWhiteSpace(q.OptionA)) options.Add(("A", q.OptionA));
                        if (!string.IsNullOrWhiteSpace(q.OptionB)) options.Add(("B", q.OptionB));
                        if (!string.IsNullOrWhiteSpace(q.OptionC)) options.Add(("C", q.OptionC));
                        if (!string.IsNullOrWhiteSpace(q.OptionD)) options.Add(("D", q.OptionD));

                        var shuffled = options.OrderBy(_ => Random.Shared.Next()).ToList();
                        optA = shuffled.ElementAtOrDefault(0).value;
                        optB = shuffled.ElementAtOrDefault(1).value;
                        optC = shuffled.ElementAtOrDefault(2).value;
                        optD = shuffled.ElementAtOrDefault(3).value;
                    }

                    return new StudentQuizQuestionDto
                    {
                        QuestionId = q.Id,
                        QuestionText = q.QuestionText,
                        Type = q.Type?.ToString(),
                        CaseId = q.CaseId,
                        OptionA = optA,
                        OptionB = optB,
                        OptionC = optC,
                        OptionD = optD,
                        ImageUrl = q.ImageUrl,
                        MaxScore = 1, // Each question is worth 1 point
                        Hint = allowHints ? q.Hint : null,
                        HintAvailable = allowHints && !string.IsNullOrWhiteSpace(q.Hint),
                        CorrectAnswers = q.CorrectAnswers,
                        AcceptedAnswers = q.AcceptedAnswers
                    };
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

        // Calculate points per question: total quiz score always = 100, divided equally among all questions
        var totalQuestions = quiz.QuizQuestions.Count;
        var pointsPerQuestion = totalQuestions > 0 ? 100m / totalQuestions : 0;

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
            else // MultipleChoice, TrueFalse, MultiSelect, FillInBlank: auto-grade
            {
                var isCorrect = false;
                if (question.Type == QuestionType.MultiSelect)
                {
                    isCorrect = CheckMultiSelectAnswer(answer.SelectedAnswers, question.CorrectAnswers);
                }
                else if (question.Type == QuestionType.FillInBlank)
                {
                    isCorrect = CheckFillInBlankAnswer(answer.StudentAnswer, question.AcceptedAnswers);
                }
                else
                {
                    isCorrect = string.Equals(
                        answer.StudentAnswer?.Trim(),
                        question.CorrectAnswer?.Trim(),
                        StringComparison.OrdinalIgnoreCase);
                }

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
                        ScoreAwarded = isCorrect ? pointsPerQuestion : 0,
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
                    existing.ScoreAwarded = isCorrect ? pointsPerQuestion : 0;
                    existing.IsGraded = true;
                    await _unitOfWork.StudentQuizAnswerRepository.UpdateAsync(existing);
                }
            }
        }

        // Helper methods for grading
        bool CheckMultiSelectAnswer(string? studentAnswer, string? correctAnswersJson)
        {
            if (string.IsNullOrWhiteSpace(studentAnswer) || string.IsNullOrWhiteSpace(correctAnswersJson))
                return false;

            try
            {
                var studentAnswers = System.Text.Json.JsonSerializer.Deserialize<List<string>>(studentAnswer)?
                    .Select(a => a.Trim().ToUpperInvariant())
                    .OrderBy(a => a)
                    .ToList() ?? new List<string>();

                var correctAnswers = System.Text.Json.JsonSerializer.Deserialize<List<string>>(correctAnswersJson)?
                    .Select(a => a.Trim().ToUpperInvariant())
                    .OrderBy(a => a)
                    .ToList() ?? new List<string>();

                return studentAnswers.SequenceEqual(correctAnswers);
            }
            catch
            {
                return false;
            }
        }

        bool CheckFillInBlankAnswer(string? studentAnswer, string? acceptedAnswersJson)
        {
            if (string.IsNullOrWhiteSpace(studentAnswer))
                return false;

            var normalizedStudent = studentAnswer.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(acceptedAnswersJson))
                return false;

            try
            {
                var acceptedAnswers = System.Text.Json.JsonSerializer.Deserialize<List<string>>(acceptedAnswersJson)?
                    .Select(a => a.Trim().ToUpperInvariant())
                    .ToList() ?? new List<string>();

                return acceptedAnswers.Contains(normalizedStudent);
            }
            catch
            {
                return false;
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

        // Calculate score: total quiz score always = 100, divided equally among all questions
        // Essay not yet graded = 0 points for that question, but still counts as 1 question
        
        // Calculate earned points: MC = full points if correct, 0 if wrong
        // Essay = full points if graded, 0 if not yet graded
        decimal earnedPoints = 0;
        foreach (var answer in attempt.StudentQuizAnswers)
        {
            if (answer.Question.Type == QuestionType.Essay)
            {
                // Essay: add actual awarded points (can be partial credit)
                earnedPoints += answer.ScoreAwarded ?? 0;
            }
            else
            {
                // MC/TF/etc: full points if correct, 0 if wrong
                earnedPoints += (answer.IsCorrect == true) ? pointsPerQuestion : 0;
            }
        }
        
        // score = earnedPoints (each point is already worth 1 point since pointsPerQuestion = 100/total)
        double score = (double)earnedPoints;
        
        // Clamp score to 0-100 range
        score = Math.Max(0, Math.Min(100, score));
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

        // Đếm số essay chưa chấm (đếm theo questionId distinct để tránh trùng lặp)
        int ungradedEssayCount = attempt.StudentQuizAnswers
            .Where(a => a.Question.Type == QuestionType.Essay && !a.IsGraded)
            .Select(a => a.QuestionId)
            .Distinct()
            .Count();

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
            if (string.IsNullOrEmpty(q.Type) || !Enum.TryParse<QuestionType>(q.Type, true, out questionType))
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
                Hint = q.Hint,
                Explanation = q.Explanation,
                CorrectAnswers = q.CorrectAnswers,
                AcceptedAnswers = q.AcceptedAnswers
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
                MaxScore = 1 // Each question is worth 1 point
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
            .Include(a => a.StudentQuizAnswers)
                .ThenInclude(sa => sa.Question)
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

            // Calculate score dynamically: total quiz score always = 100, divided equally among all questions
            var totalQuestions = attempt.Quiz.QuizQuestions.Count;
            var pointsPerQuestion = totalQuestions > 0 ? 100m / totalQuestions : 0;

            // Calculate earned points: MC = full points if correct, 0 if wrong
            // Essay = full points if graded, 0 if not yet graded
            decimal earnedPoints = 0;
            foreach (var answer in attempt.StudentQuizAnswers)
            {
                if (answer.Question?.Type == QuestionType.Essay)
                {
                    // Essay: add actual awarded points (can be partial credit)
                    earnedPoints += answer.ScoreAwarded ?? 0;
                }
                else
                {
                    // MC/TF/etc: full points if correct, 0 if wrong
                    earnedPoints += (answer.IsCorrect == true) ? pointsPerQuestion : 0;
                }
            }

            // Score = earnedPoints (clamped to 0-100)
            double calculatedScore = Math.Max(0, Math.Min(100, (double)earnedPoints));

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
                Score = calculatedScore, // Use calculated score instead of saved score
                PassingScore = attempt.Quiz.PassingScore,
                Passed = calculatedScore >= (attempt.Quiz.PassingScore ?? 0),
                TotalQuestions = totalQuestions,
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

        // Kiểm tra xem đáp án đã được release chưa
        var utcNow = DateTime.UtcNow;
        bool answersReleased = false;

        // Lấy ClassQuizSession để kiểm tra release status
        var classIds = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        if (classIds.Count > 0)
        {
            var quizSession = await _unitOfWork.Context.ClassQuizSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.QuizId == attempt.QuizId && classIds.Contains(s.ClassId));

            if (quizSession != null)
            {
                // Quiz đã đóng HOẶC lecturer đã release đáp án
                var isQuizClosed = quizSession.CloseTime.HasValue && quizSession.CloseTime.Value < utcNow;
                answersReleased = isQuizClosed || quizSession.ReleaseAnswersAt.HasValue;
            }
        }

        var totalQuestions = attempt.Quiz.QuizQuestions.Count;
        var pointsPerQuestion = totalQuestions > 0 ? 100m / totalQuestions : 0;
        var correctCount = attempt.StudentQuizAnswers.Count(a => a.IsCorrect == true);

        // Calculate score dynamically: total quiz score always = 100, divided equally among all questions
        decimal earnedPoints = 0;
        foreach (var answer in attempt.StudentQuizAnswers)
        {
            if (answer.Question?.Type == QuestionType.Essay)
            {
                // Essay: add actual awarded points (can be partial credit)
                earnedPoints += answer.ScoreAwarded ?? 0;
            }
            else
            {
                // MC/TF/etc: full points if correct, 0 if wrong
                earnedPoints += (answer.IsCorrect == true) ? pointsPerQuestion : 0;
            }
        }

        // Score = earnedPoints (clamped to 0-100)
        var score = Math.Max(0, Math.Min(100, (double)earnedPoints));
        var passingScore = NormalizePassingScore(attempt.Quiz.PassingScore, attempt.Quiz.IsAiGenerated);

        // Determine if practice mode based on quiz/session settings
        var session = classIds.Count > 0
            ? await _unitOfWork.Context.ClassQuizSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.QuizId == attempt.QuizId && classIds.Contains(s.ClassId))
            : null;

        var quizMode = session?.QuizMode ?? attempt.Quiz?.QuizMode ?? 1;
        var isPracticeMode = quizMode == 2; // 2 = Practice mode

        // In practice mode, always show answers. Otherwise, only show when released.
        var showAnswers = isPracticeMode || answersReleased;

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
                StudentSelectedAnswers = answer?.StudentAnswer,
                StudentTextAnswer = answer?.StudentAnswer,
                // Show correct answer based on mode
                CorrectAnswer = showAnswers ? question.CorrectAnswer : null,
                CorrectAnswers = showAnswers ? question.CorrectAnswers : null,
                AcceptedAnswers = showAnswers ? question.AcceptedAnswers : null,
                IsCorrect = answer?.IsCorrect ?? false,
                ImageUrl = question.ImageUrl,
                CaseId = question.CaseId?.ToString(),
                ScoreAwarded = answer?.ScoreAwarded,
                LecturerFeedback = answer?.LecturerFeedback,
                IsGraded = answer?.IsGraded ?? (question.Type == QuestionType.Essay ? false : true),
                MaxScore = 1, // Each question is worth 1 point
                // Show explanation in practice mode or when released
                Explanation = showAnswers ? question.Explanation : null,
                Hint = question.Hint
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
            AnswersReleased = answersReleased,
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
        try
        {
            // Fetch recent QA messages
            var recentQuestions = await _unitOfWork.Context.QaMessages
                .AsNoTracking()
                .Where(m => m.Role == "User")
                .OrderByDescending(m => m.CreatedAt)
                .Take(20)
                .ToListAsync();

            var sessionIds = recentQuestions
                .Select(m => m.SessionId)
                .Distinct()
                .ToList();

            var sessions = await _unitOfWork.Context.VisualQaSessions
                .AsNoTracking()
                .Where(s => sessionIds.Contains(s.Id))
                .Include(s => s.Case)
                    .ThenInclude(c => c != null ? c.Category : null)
                .ToListAsync();

            var sessionDict = sessions.ToDictionary(s => s.Id);

            var questionActivities = recentQuestions
                .Where(m => sessionDict.ContainsKey(m.SessionId))
                .Where(m => sessionDict[m.SessionId].StudentId == studentId)
                .Select(m =>
                {
                    var session = sessionDict[m.SessionId];
                    var caseEntity = session.Case;
                    return new StudentRecentActivityDto
                    {
                        ActivityType = "visual_qa",
                        Title = caseEntity != null ? $"Asked a question on {caseEntity.Title}" : "Asked a visual QA question",
                        Description = m.Content ?? string.Empty,
                        Topic = caseEntity != null
                            ? caseEntity.Category != null
                                ? caseEntity.Category.Name ?? caseEntity.Title ?? "Case"
                                : caseEntity.Title ?? "Case"
                            : "Personal Upload",
                        OccurredAt = m.CreatedAt,
                        SessionId = m.SessionId,
                        TargetUrl = "/student/qa/image?sessionId=" + m.SessionId.ToString()
                    };
                })
                .ToList();

            // Fetch recent quiz attempts
            var recentQuizAttempts = await _unitOfWork.Context.QuizAttempts
                .AsNoTracking()
                .Include(a => a.Quiz)
                .Where(a => a.StudentId == studentId && a.CompletedAt.HasValue)
                .OrderByDescending(a => a.CompletedAt)
                .Take(10)
                .ToListAsync();

            var quizActivities = recentQuizAttempts.Select(a => new StudentRecentActivityDto
            {
                ActivityType = "quiz",
                Title = $"Completed Quiz {a.Quiz?.Title ?? "Unknown"}",
                Description = a.Score.HasValue
                    ? $"Completed Quiz {a.Quiz?.Title ?? "Unknown"} with {a.Score.Value:F0}%"
                    : $"Completed Quiz {a.Quiz?.Title ?? "Unknown"}",
                Topic = a.Quiz?.Title ?? "Quiz",
                OccurredAt = a.CompletedAt ?? a.StartedAt ?? DateTime.UtcNow,
                QuizId = a.QuizId
            }).ToList();

            return questionActivities
                .Concat(quizActivities)
                .OrderByDescending(x => x.OccurredAt)
                .Take(10)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recent activity for student {StudentId}", studentId);
            return new List<StudentRecentActivityDto>();
        }
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
