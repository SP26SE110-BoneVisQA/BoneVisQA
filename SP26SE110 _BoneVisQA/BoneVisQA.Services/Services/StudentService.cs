using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;

namespace BoneVisQA.Services.Services;

public class StudentService : IStudentService
{
    private readonly StudentRepository _studentRepository;

    public StudentService(StudentRepository studentRepository)
    {
        _studentRepository = studentRepository;
    }

    public async Task<IReadOnlyList<CaseListItemDto>> GetCasesAsync(Guid studentId)
    {
        var cases = await _studentRepository.GetAllCasesAsync();

        return cases
            .Select(c => new CaseListItemDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                Difficulty = c.Difficulty,
                CategoryName = c.Category?.Name,
                ThumbnailImageUrl = c.MedicalImages.FirstOrDefault()?.ImageUrl
            })
            .ToList();
    }

    public async Task<CaseDetailDto?> GetCaseDetailAsync(Guid caseId, Guid studentId)
    {
        var entity = await _studentRepository.GetCaseWithImagesAsync(caseId);
        if (entity == null)
        {
            return null;
        }

        var viewLog = new CaseViewLog
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CaseId = caseId,
            ViewedAt = DateTime.UtcNow,
            IsCompleted = false
        };

        await _studentRepository.AddCaseViewLogAsync(viewLog);

        return new CaseDetailDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Difficulty = entity.Difficulty,
            CategoryName = entity.Category?.Name,
            Images = entity.MedicalImages
                .OrderBy(i => i.CreatedAt)
                .Select(i => new MedicalImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    Modality = i.Modality
                })
                .ToList()
        };
    }

    public async Task<AnnotationDto> CreateAnnotationAsync(Guid studentId, CreateAnnotationRequestDto request)
    {
        var entity = new CaseAnnotation
        {
            Id = Guid.NewGuid(),
            ImageId = request.ImageId,
            Label = request.Label,
            Coordinates = request.Coordinates,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _studentRepository.CreateAnnotationAsync(entity);

        return new AnnotationDto
        {
            Id = created.Id,
            ImageId = created.ImageId,
            Label = created.Label,
            Coordinates = created.Coordinates,
            CreatedAt = created.CreatedAt
        };
    }

    public async Task<StudentQuestionDto> AskQuestionAsync(Guid studentId, AskQuestionRequestDto request)
    {
        var question = new StudentQuestion
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CaseId = request.CaseId,
            AnnotationId = request.AnnotationId,
            QuestionText = request.QuestionText,
            Language = request.Language,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _studentRepository.CreateStudentQuestionAsync(question);

        // Tạm thời chỉ lưu câu hỏi, phần trả lời AI/RAG sẽ được tích hợp ở module khác.

        return new StudentQuestionDto
        {
            Id = created.Id,
            StudentId = created.StudentId,
            CaseId = created.CaseId,
            AnnotationId = created.AnnotationId,
            QuestionText = created.QuestionText,
            Language = created.Language,
            CreatedAt = created.CreatedAt
        };
    }

    public async Task<IReadOnlyList<StudentQuestionHistoryItemDto>> GetQuestionHistoryAsync(Guid studentId)
    {
        var items = await _studentRepository.GetQuestionsByStudentAsync(studentId);

        return items
            .Select(q => new StudentQuestionHistoryItemDto
            {
                Id = q.Id,
                CaseId = q.CaseId,
                QuestionText = q.QuestionText,
                CreatedAt = q.CreatedAt
            })
            .ToList();
    }

    //public async Task<IReadOnlyList<QuizListItemDto>> GetAvailableQuizzesAsync(Guid studentId)
    //{
    //    var utcNow = DateTime.UtcNow;
    //    var quizzes = await _studentRepository.GetQuizzesForStudentAsync(studentId, utcNow);

    //    return quizzes
    //        .Select(q =>
    //        {
    //            var attempt = q.QuizAttempts.FirstOrDefault(a => a.StudentId == studentId);
    //            return new QuizListItemDto
    //            {
    //                QuizId = q.Id,
    //                Title = q.Title,
    //                OpenTime = q.OpenTime,
    //                CloseTime = q.CloseTime,
    //                TimeLimit = q.TimeLimit,
    //                PassingScore = q.PassingScore,
    //                IsCompleted = attempt?.CompletedAt != null,
    //                Score = attempt?.Score
    //            };
    //        })
    //        .ToList();
    //}

    public async Task<QuizSessionDto> StartQuizAsync(Guid studentId, Guid quizId)
    {
        var quiz = await _studentRepository.GetQuizWithQuestionsAsync(quizId);
        if (quiz == null)
        {
            throw new InvalidOperationException("Quiz không tồn tại.");
        }

        var existingAttempt = await _studentRepository.GetQuizAttemptAsync(studentId, quizId);
        QuizAttempt attempt;

        if (existingAttempt != null)
        {
            attempt = existingAttempt;
        }
        else
        {
            attempt = new QuizAttempt
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                QuizId = quizId,
                StartedAt = DateTime.UtcNow
            };

            attempt = await _studentRepository.CreateQuizAttemptAsync(attempt);
        }

        var questions = quiz.QuizQuestions
            .Select(q => new QuizQuestionDto
            {
                QuestionId = q.Id,
                QuestionText = q.QuestionText,
                Type = q.Type,
                CaseId = q.CaseId
            })
            .ToList();

        return new QuizSessionDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            Title = quiz.Title,
            Questions = questions
        };
    }

    public async Task<QuizResultDto> SubmitQuizAsync(Guid studentId, SubmitQuizRequestDto request)
    {
        var attempt = await _studentRepository.GetQuizAttemptAsync(studentId, request.AttemptId);
        if (attempt == null)
        {
            throw new InvalidOperationException("Lần làm quiz không tồn tại.");
        }

        var quiz = await _studentRepository.GetQuizWithQuestionsAsync(attempt.QuizId);
        if (quiz == null)
        {
            throw new InvalidOperationException("Quiz không tồn tại.");
        }

        var questionDict = quiz.QuizQuestions.ToDictionary(q => q.Id, q => q);

        var answers = new List<StudentQuizAnswer>();
        var correctCount = 0;

        foreach (var a in request.Answers)
        {
            if (!questionDict.TryGetValue(a.QuestionId, out var question))
            {
                continue;
            }

            var isCorrect = false;
            if (question.CorrectAnswer != null && a.StudentAnswer != null)
            {
                isCorrect = string.Equals(
                    question.CorrectAnswer.Trim(),
                    a.StudentAnswer.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }

            if (isCorrect)
            {
                correctCount++;
            }

            answers.Add(new StudentQuizAnswer
            {
                Id = Guid.NewGuid(),
                AttemptId = attempt.Id,
                QuestionId = question.Id,
                StudentAnswer = a.StudentAnswer,
                IsCorrect = isCorrect
            });
        }

        var totalQuestions = quiz.QuizQuestions.Count;
        double? score = null;
        if (totalQuestions > 0)
        {
            score = correctCount * 100.0 / totalQuestions;
        }

        attempt.Score = score;
        attempt.CompletedAt = DateTime.UtcNow;

        await _studentRepository.AddStudentQuizAnswersAsync(answers);
        await _studentRepository.UpdateQuizAttemptAsync(attempt);

        var passed = score.HasValue && quiz.PassingScore.HasValue && score.Value >= quiz.PassingScore.Value;

        return new QuizResultDto
        {
            AttemptId = attempt.Id,
            QuizId = attempt.QuizId,
            Score = score,
            PassingScore = quiz.PassingScore,
            Passed = passed
        };
    }

    public async Task<StudentProgressDto> GetProgressAsync(Guid studentId)
    {
        var (totalCasesViewed, totalQuestionsAsked, avgQuizScore) =
            await _studentRepository.GetStudentAggregateStatsAsync(studentId);

        return new StudentProgressDto
        {
            TotalCasesViewed = totalCasesViewed,
            TotalQuestionsAsked = totalQuestionsAsked,
            AvgQuizScore = avgQuizScore
        };
    }
}

