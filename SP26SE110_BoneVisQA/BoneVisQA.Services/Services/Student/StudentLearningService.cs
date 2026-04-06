using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
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
                (cqs.OpenTime == null || cqs.OpenTime <= utcNow) &&
                (cqs.CloseTime == null || cqs.CloseTime >= utcNow)))
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
            return await CreateSessionFromQuizAsync(quiz, studentId);
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
                (cqs.OpenTime == null || cqs.OpenTime <= utcNow) &&
                (cqs.CloseTime == null || cqs.CloseTime >= utcNow)))
            .Where(q => q.QuizQuestions.Any())
            .FirstOrDefaultAsync();

        if (anyQuiz != null)
            return await CreateSessionFromQuizAsync(anyQuiz, studentId);

        throw new KeyNotFoundException("Không tìm thấy quiz luyện tập phù hợp.");
    }

    private async Task<QuizSessionDto> CreateSessionFromQuizAsync(
        BoneVisQA.Repositories.Models.Quiz quiz, 
        Guid studentId)
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

        return new QuizSessionDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            Title = quiz.Title,
            Topic = quiz.Topic,
            Questions = quiz.QuizQuestions
                .OrderBy(q => q.QuestionText)
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
            ?? throw new KeyNotFoundException("Không tìm thấy lần làm quiz.");

        if (attempt.CompletedAt.HasValue)
            throw new InvalidOperationException("Quiz này đã được nộp.");

        var utcNow = DateTime.UtcNow;

        var classIds = await _unitOfWork.Context.ClassEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync();

        // Kiểm tra quiz còn đang mở không
        var session = await _unitOfWork.Context.ClassQuizSessions
            .FirstOrDefaultAsync(cqs =>
                cqs.QuizId == attempt.QuizId &&
                classIds.Contains(cqs.ClassId) &&
                (cqs.OpenTime == null || cqs.OpenTime <= utcNow) &&
                (cqs.CloseTime == null || cqs.CloseTime >= utcNow));

        if (session == null)
            throw new InvalidOperationException("Quiz đã đóng. Không thể nộp bài.");

        var quiz = await _unitOfWork.Context.Quizzes
            .Include(q => q.QuizQuestions)
            .FirstOrDefaultAsync(q => q.Id == attempt.QuizId)
            ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

        var questionMap = quiz.QuizQuestions.ToDictionary(q => q.Id, q => q);
        var incomingAnswers = request.Answers
            .GroupBy(a => a.QuestionId)
            .Select(g => g.Last())
            .ToList();

        foreach (var answer in incomingAnswers)
        {
            if (!questionMap.TryGetValue(answer.QuestionId, out var question))
                throw new InvalidOperationException("Phát hiện câu trả lời không thuộc quiz này.");

            var existing = attempt.StudentQuizAnswers.FirstOrDefault(a => a.QuestionId == answer.QuestionId);
            var isCorrect = string.Equals(
                answer.StudentAnswer?.Trim(),
                question.CorrectAnswer?.Trim(),
                StringComparison.OrdinalIgnoreCase);

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

        var totalQuestions = quiz.QuizQuestions.Count;
        var correctAnswers = attempt.StudentQuizAnswers.Count(a => a.IsCorrect == true);
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

    public async Task<StudentProgressDto> GetProgressSummaryAsync(Guid studentId)
    {
        var totalCasesViewed = await _unitOfWork.Context.CaseViewLogs
            .CountAsync(v => v.StudentId == studentId);

        var totalQuestionsAsked = await _unitOfWork.Context.StudentQuestions
            .CountAsync(q => q.StudentId == studentId);

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
            .CountAsync(a => a.Question.StudentId == studentId && a.Status == "Escalated");

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

        var questionRows = await _unitOfWork.Context.StudentQuestions
            .AsNoTracking()
            .Include(q => q.Case)
                .ThenInclude(c => c!.Category)
            .Where(q => q.StudentId == studentId)
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

        foreach (var question in questionRows)
        {
            var topic = InferQuestionTopic(question);
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
        var recentQuestions = await _unitOfWork.Context.StudentQuestions
            .AsNoTracking()
            .Include(q => q.Case)
                .ThenInclude(c => c!.Category)
            .Where(q => q.StudentId == studentId)
            .OrderByDescending(q => q.CreatedAt)
            .Take(10)
            .Select(q => new StudentRecentActivityDto
            {
                ActivityType = "Question",
                Title = q.Case != null ? $"Asked a question on {q.Case.Title}" : "Asked a visual QA question",
                Description = q.QuestionText,
                Topic = q.Case != null
                    ? q.Case.Category != null
                        ? q.Case.Category.Name
                        : q.Case.Title
                    : "Personal Upload",
                OccurredAt = q.CreatedAt ?? DateTime.MinValue
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
            .Where(s => s.CloseTime != null && s.CloseTime < utcNow)
            .Select(s => new { s.QuizId, s.ClassId })
            .ToListAsync();

        if (expiredSessions.Count == 0)
            return;

        var expiredQuizClassPairs = expiredSessions
            .Select(s => new { s.QuizId, s.ClassId })
            .ToList();

        // Tìm các attempt chưa nộp và thuộc quiz đã đóng
        var expiredAttempts = await _unitOfWork.Context.QuizAttempts
            .Include(a => a.StudentQuizAnswers)
            .Include(a => a.Quiz)
                .ThenInclude(q => q.QuizQuestions)
            .Where(a => a.CompletedAt == null)
            .Where(a => expiredQuizClassPairs.Any(p => p.QuizId == a.QuizId))
            .ToListAsync();

        foreach (var attempt in expiredAttempts)
        {
            var quiz = attempt.Quiz;
            if (quiz == null) continue;

            // Kiểm tra student có trong lớp của quiz đã đóng không
            var isStudentInClass = await _unitOfWork.Context.ClassEnrollments
                .AnyAsync(e => e.StudentId == attempt.StudentId &&
                              expiredQuizClassPairs.Any(p => p.QuizId == attempt.QuizId && p.ClassId == e.ClassId));

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

    private static string InferQuestionTopic(BoneVisQA.Repositories.Models.StudentQuestion question)
    {
        if (!string.IsNullOrWhiteSpace(question.Case?.Category?.Name))
            return question.Case.Category.Name;

        if (!string.IsNullOrWhiteSpace(question.Case?.Title))
            return question.Case.Title;

        return "Personal Upload";
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
