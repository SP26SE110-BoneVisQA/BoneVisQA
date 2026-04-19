using System;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Quiz;
using BoneVisQA.Services.Models.Student;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Student;

public class StudentLearningService : IStudentLearningService
{
    private readonly IUnitOfWork _unitOfWork;

    public StudentLearningService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
            var shuffleSetting = quiz.ClassQuizSessions
                .FirstOrDefault(cqs => classIds.Contains(cqs.ClassId))?.ShuffleQuestions ?? false;
            return await CreateSessionFromQuizAsync(quiz, studentId, shuffleSetting);
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
            var shuffleSetting = anyQuiz.ClassQuizSessions
                .FirstOrDefault(cqs => classIds.Contains(cqs.ClassId))?.ShuffleQuestions ?? false;
            return await CreateSessionFromQuizAsync(anyQuiz, studentId, shuffleSetting);
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
        bool shuffleQuestions = false)
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
            TimeLimit = quiz.TimeLimit,
            Questions = questions
                .Select(q => new StudentQuizQuestionDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText,
                    Type = q.Type,
                    CaseId = q.CaseId,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    ImageUrl = q.ImageUrl
                })
                .ToList()
        };
    }

    public async Task<QuizResultDto> SubmitQuizAttemptAsync(Guid studentId, SubmitQuizRequestDto request)
    {
        var attempt = await _unitOfWork.Context.QuizAttempts
            .Include(a => a.Quiz)
            .Include(a => a.StudentQuizAnswers)
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
            var isCorrect = QuizAnswerMatchesCorrect(question.CorrectAnswer, answer.StudentAnswer);

            if (existing == null)
            {
                existing = new BoneVisQA.Repositories.Models.StudentQuizAnswer
                {
                    Id = Guid.NewGuid(),
                    AttemptId = attempt.Id,
                    QuestionId = answer.QuestionId,
                    StudentAnswer = answer.StudentAnswer,
                    IsCorrect = isCorrect
                };
                await _unitOfWork.StudentQuizAnswerRepository.AddAsync(existing);
                attempt.StudentQuizAnswers.Add(existing);
            }
            else
            {
                existing.StudentAnswer = answer.StudentAnswer;
                existing.IsCorrect = isCorrect;
                await _unitOfWork.StudentQuizAnswerRepository.UpdateAsync(existing);
            }
        }

        // Đồng bộ cờ is_correct cho mọi dòng đã lưu (tránh NULL / lệch so với đáp án khi lecturer đếm).
        foreach (var row in attempt.StudentQuizAnswers.ToList())
        {
            if (!questionMap.TryGetValue(row.QuestionId, out var q)) continue;
            row.IsCorrect = QuizAnswerMatchesCorrect(q.CorrectAnswer, row.StudentAnswer);
        }

        var totalQuestions = quiz.QuizQuestions.Count;
        // Chỉ đếm đúng theo từng câu hiện có trong quiz (tránh dòng answer “mồ côi” sau khi đổi đề).
        var correctAnswers = quiz.QuizQuestions.Count(q =>
        {
            var row = attempt.StudentQuizAnswers.FirstOrDefault(a => a.QuestionId == q.Id);
            return row != null && row.IsCorrect == true;
        });
        var score = totalQuestions == 0 ? 0 : (double)correctAnswers * 100 / totalQuestions;

        attempt.Score = score;
        attempt.CompletedAt = DateTime.UtcNow;
        await _unitOfWork.QuizAttemptRepository.UpdateAsync(attempt);
        await _unitOfWork.SaveAsync();

        return new QuizResultDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            Score = score,
            PassingScore = quiz.PassingScore,
            Passed = !quiz.PassingScore.HasValue || score >= quiz.PassingScore.Value,
            TotalQuestions = totalQuestions,
            CorrectAnswers = correctAnswers
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
            var question = new BoneVisQA.Repositories.Models.QuizQuestion
            {
                Id = Guid.NewGuid(),
                QuizId = quiz.Id,
                QuestionText = q.QuestionText,
                Type = q.Type ?? "MultipleChoice",
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
                Type = q.Type,
                CaseId = q.CaseId,
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD,
                ImageUrl = q.ImageUrl,
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
            .OrderByDescending(a => a.CompletedAt ?? a.StartedAt)
            .ToListAsync();

        var result = new List<StudentQuizAttemptSummaryDto>();
        foreach (var attempt in attempts)
        {
            var correctCount = await _unitOfWork.Context.StudentQuizAnswers
                .CountAsync(a => a.AttemptId == attempt.Id && a.IsCorrect == true);

            result.Add(new StudentQuizAttemptSummaryDto
            {
                AttemptId = attempt.Id,
                QuizId = attempt.QuizId,
                QuizTitle = attempt.Quiz?.Title ?? "Untitled Quiz",
                Topic = attempt.Quiz?.Topic,
                Difficulty = attempt.Quiz?.Difficulty,
                ClassName = null, // AI quiz không gắn class
                StartedAt = attempt.StartedAt,
                CompletedAt = attempt.CompletedAt,
                Score = attempt.Score,
                PassingScore = attempt.Quiz?.PassingScore,
                Passed = attempt.Score.HasValue && attempt.Quiz != null
                    ? !attempt.Quiz.PassingScore.HasValue || attempt.Score >= attempt.Quiz.PassingScore.Value
                    : false,
                TotalQuestions = await _unitOfWork.Context.QuizQuestions.CountAsync(q => q.QuizId == attempt.QuizId),
                CorrectAnswers = correctCount,
                IsAiGenerated = attempt.Quiz?.IsAiGenerated ?? false,
            });
        }

        return result;
    }

    public async Task<StudentProgressDto> GetProgressSummaryAsync(Guid studentId)
    {
        var totalCasesViewed = await _unitOfWork.Context.CaseViewLogs
            .CountAsync(v => v.StudentId == studentId);

        var totalQuestionsAsked = await _unitOfWork.Context.QaMessages
            .CountAsync(m => m.Role == "User" && m.Session.StudentId == studentId);

        var attempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.CompletedAt ?? a.StartedAt)
            .ToListAsync();

        var completedAttempts = attempts.Where(a => a.CompletedAt.HasValue).ToList();
        double? avgQuizScore = completedAttempts.Count == 0
            ? null
            : completedAttempts.Average(a => a.Score ?? 0);
        var latestQuizScore = completedAttempts.FirstOrDefault()?.Score;

        var answerRows = await _unitOfWork.Context.StudentQuizAnswers
            .Include(a => a.Attempt)
            .Where(a => a.Attempt.StudentId == studentId)
            .ToListAsync();

        var totalAnswered = answerRows.Count;
        var correctAnswered = answerRows.Count(a => a.IsCorrect == true);
        double? accuracy = totalAnswered == 0 ? null : (double)correctAnswered * 100 / totalAnswered;

        var escalatedAnswers = await _unitOfWork.Context.CaseAnswers
            .Include(a => a.Question)
            .CountAsync(a =>
                a.Question.StudentId == studentId
                && (a.Status == CaseAnswerStatuses.EscalatedToExpert
                    || a.Status == CaseAnswerStatuses.Escalated));

        return new StudentProgressDto
        {
            TotalCasesViewed = totalCasesViewed,
            TotalQuestionsAsked = totalQuestionsAsked,
            AvgQuizScore = avgQuizScore,
            TotalQuizAttempts = attempts.Count,
            CompletedQuizzes = completedAttempts.Count,
            EscalatedAnswers = escalatedAnswers,
            LatestQuizScore = latestQuizScore,
            QuizAccuracyRate = accuracy
        };
    }

    public async Task<IReadOnlyList<StudentTopicStatDto>> GetTopicStatsAsync(Guid studentId)
    {
        var quizAttempts = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
                .ThenInclude(q => q.QuizQuestions)
                    .ThenInclude(qq => qq.Case)
                        .ThenInclude(c => c!.Category)
            .Include(a => a.StudentQuizAnswers)
            .Where(a => a.StudentId == studentId)
            .ToListAsync();

        var questionTopics = await _unitOfWork.Context.QaMessages
            .AsNoTracking()
            .Include(m => m.Session)
                .ThenInclude(s => s.Case)
                    .ThenInclude(c => c!.Category)
            .Where(m => m.Role == "User" && m.Session.StudentId == studentId)
            .Select(m => !string.IsNullOrWhiteSpace(m.Session.Case != null && m.Session.Case.Category != null ? m.Session.Case.Category.Name : null)
                ? m.Session.Case!.Category!.Name
                : !string.IsNullOrWhiteSpace(m.Session.Case != null ? m.Session.Case.Title : null)
                    ? m.Session.Case!.Title
                    : "Personal Upload")
            .ToListAsync();

        var topicMap = new Dictionary<string, StudentTopicStatAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var attempt in quizAttempts)
        {
            var topic = InferQuizTopic(attempt);
            var bucket = GetOrCreateTopicBucket(topicMap, topic);
            bucket.QuizAttempts++;
            if (attempt.Score.HasValue)
                bucket.QuizScores.Add(attempt.Score.Value);

            var answers = attempt.StudentQuizAnswers.ToList();
            bucket.TotalQuizAnswers += answers.Count;
            bucket.CorrectQuizAnswers += answers.Count(a => a.IsCorrect == true);
        }

        foreach (var topic in questionTopics)
        {
            var bucket = GetOrCreateTopicBucket(topicMap, topic);
            bucket.QuestionsAsked++;
        }

        return topicMap
            .Select(kvp => new StudentTopicStatDto
            {
                Topic = kvp.Key,
                QuizAttempts = kvp.Value.QuizAttempts,
                QuestionsAsked = kvp.Value.QuestionsAsked,
                AverageQuizScore = kvp.Value.QuizScores.Count == 0 ? null : kvp.Value.QuizScores.Average(),
                AccuracyRate = kvp.Value.TotalQuizAnswers == 0
                    ? null
                    : (double)kvp.Value.CorrectQuizAnswers * 100 / kvp.Value.TotalQuizAnswers,
                TotalInteractions = kvp.Value.QuizAttempts + kvp.Value.QuestionsAsked
            })
            .OrderByDescending(x => x.TotalInteractions)
            .ThenBy(x => x.Topic)
            .ToList();
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

        var recentQuizzes = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
                .ThenInclude(q => q.QuizQuestions)
                    .ThenInclude(qq => qq.Case)
                        .ThenInclude(c => c!.Category)
            .Where(a => a.StudentId == studentId && a.CompletedAt.HasValue)
            .OrderByDescending(a => a.CompletedAt)
            .Take(10)
            .Select(a => new StudentRecentActivityDto
            {
                ActivityType = "Quiz",
                Title = $"Completed Quiz {a.Quiz.Title}",
                Description = a.Score.HasValue
                    ? $"Completed Quiz {a.Quiz.Title} with {a.Score.Value:F0}%"
                    : $"Completed Quiz {a.Quiz.Title}",
                Topic = InferQuizTopic(a),
                OccurredAt = a.CompletedAt ?? a.StartedAt ?? DateTime.MinValue
            })
            .ToListAsync();

        return recentQuestions
            .Concat(recentQuizzes)
            .OrderByDescending(x => x.OccurredAt)
            .Take(10)
            .ToList();
    }

    public async Task AutoCloseExpiredAttemptsAsync()
    {
        var utcNow = DateTime.UtcNow;

        // Tìm tất cả quiz sessions đã đóng
        var expiredSessions = await _unitOfWork.Context.ClassQuizSessions
            .Include(s => s.Quiz)
            .Where(s => (s.CloseTime ?? s.Quiz!.CloseTime) != null
                        && (s.CloseTime ?? s.Quiz!.CloseTime) < utcNow)
            .Select(s => new { s.QuizId, s.ClassId })
            .ToListAsync();

        if (expiredSessions.Count == 0)
            return;

        var expiredQuizIds = expiredSessions
            .Select(s => s.QuizId)
            .Distinct()
            .ToList();

        var expiredQuizClassPairs = expiredSessions
            .Select(s => new { s.QuizId, s.ClassId })
            .ToList();

        // Tìm các attempt chưa nộp và thuộc quiz đã đóng
        var expiredAttempts = await _unitOfWork.Context.QuizAttempts
            .Include(a => a.StudentQuizAnswers)
            .Include(a => a.Quiz)
                .ThenInclude(q => q.QuizQuestions)
            .Where(a => a.CompletedAt == null)
            .Where(a => expiredQuizIds.Contains(a.QuizId))
            .ToListAsync();

        foreach (var attempt in expiredAttempts)
        {
            var quiz = attempt.Quiz;
            if (quiz == null) continue;

            // Các classId mà quiz này đã đóng cho sinh viên này
            var closedClassIds = expiredQuizClassPairs
                .Where(p => p.QuizId == attempt.QuizId)
                .Select(p => p.ClassId)
                .ToHashSet();

            // Kiểm tra student có trong lớp của quiz đã đóng không
            var isStudentInClass = await _unitOfWork.Context.ClassEnrollments
                .AnyAsync(e => e.StudentId == attempt.StudentId &&
                              closedClassIds.Contains(e.ClassId));

            if (!isStudentInClass) continue;

            // Tính điểm dựa trên các câu đã làm
            var totalQuestions = quiz.QuizQuestions.Count;
            var correctAnswers = attempt.StudentQuizAnswers.Count(a => a.IsCorrect == true);
            var score = totalQuestions == 0 ? 0 : (double)correctAnswers * 100 / totalQuestions;

            // Tự động nộp
            attempt.Score = score;
            attempt.CompletedAt = attempt.StartedAt.HasValue
                ? (attempt.StartedAt.Value > utcNow ? utcNow : attempt.StartedAt.Value.AddMinutes(quiz.TimeLimit ?? 0))
                : utcNow;

            await _unitOfWork.QuizAttemptRepository.UpdateAsync(attempt);
        }

        await _unitOfWork.SaveAsync();
    }

    private static StudentTopicStatAccumulator GetOrCreateTopicBucket(
        IDictionary<string, StudentTopicStatAccumulator> topicMap,
        string topic)
    {
        if (!topicMap.TryGetValue(topic, out var bucket))
        {
            bucket = new StudentTopicStatAccumulator();
            topicMap[topic] = bucket;
        }

        return bucket;
    }

    private static string InferQuizTopic(BoneVisQA.Repositories.Models.QuizAttempt attempt)
    {
        var categoryName = attempt.Quiz?.QuizQuestions
            .Select(q => q.Case?.Category?.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

        if (!string.IsNullOrWhiteSpace(categoryName))
            return categoryName!;

        return !string.IsNullOrWhiteSpace(attempt.Quiz?.Title)
            ? attempt.Quiz.Title
            : "General";
    }

    public async Task<QuizAttemptReviewDto> GetQuizAttemptReviewAsync(Guid studentId, Guid attemptId)
    {
        var attempt = await _unitOfWork.Context.QuizAttempts
            .Include(a => a.Quiz)
            .Include(a => a.StudentQuizAnswers)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.StudentId == studentId)
            ?? throw new KeyNotFoundException("Quiz attempt not found.");

        if (!attempt.CompletedAt.HasValue)
            throw new InvalidOperationException("This quiz has not been submitted yet.");

        var questions = await _unitOfWork.Context.QuizQuestions
            .AsNoTracking()
            .Where(q => q.QuizId == attempt.QuizId)
            .OrderBy(q => q.Id)
            .ToListAsync();

        var answerMap = attempt.StudentQuizAnswers.ToDictionary(a => a.QuestionId, a => a);

        var reviewItems = questions.Select(q =>
        {
            answerMap.TryGetValue(q.Id, out var studentAnswer);
            var isCorrect = studentAnswer != null &&
                QuizAnswerMatchesCorrect(q.CorrectAnswer, studentAnswer.StudentAnswer);
            return new QuestionReviewItemDto
            {
                QuestionId = q.Id,
                QuestionText = q.QuestionText,
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD,
                StudentAnswer = studentAnswer?.StudentAnswer,
                CorrectAnswer = q.CorrectAnswer,
                IsCorrect = isCorrect,
                ImageUrl = q.ImageUrl,
                CaseId = q.CaseId?.ToString(),
            };
        }).ToList();

        var correctCount = reviewItems.Count(r => r.IsCorrect);
        var total = reviewItems.Count;
        var score = total == 0 ? 0.0 : (double)correctCount * 100 / total;

        var quizForPass = attempt.Quiz;
        var passed = quizForPass == null || !quizForPass.PassingScore.HasValue
            || score >= quizForPass.PassingScore.Value;

        return new QuizAttemptReviewDto
        {
            AttemptId = attempt.Id,
            QuizTitle = attempt.Quiz?.Title ?? "Quiz",
            Score = score,
            TotalQuestions = total,
            CorrectAnswers = correctCount,
            Passed = passed,
            Questions = reviewItems,
        };
    }

    public async Task DeleteQuizAttemptAsync(Guid studentId, Guid attemptId)
    {
        var attempt = await _unitOfWork.Context.QuizAttempts
            .Include(a => a.StudentQuizAnswers)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.StudentId == studentId)
            ?? throw new KeyNotFoundException("Quiz attempt not found.");

        _unitOfWork.Context.StudentQuizAnswers.RemoveRange(attempt.StudentQuizAnswers);
        _unitOfWork.Context.QuizAttempts.Remove(attempt);
        await _unitOfWork.SaveAsync();
    }

    private sealed class StudentTopicStatAccumulator
    {
        public int QuizAttempts { get; set; }
        public int QuestionsAsked { get; set; }
        public int TotalQuizAnswers { get; set; }
        public int CorrectQuizAnswers { get; set; }
        public List<double> QuizScores { get; } = new();
    }
}
