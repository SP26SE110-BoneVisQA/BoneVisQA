using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using BoneVisQA.Services.Models.Student;
using BoneVisQA.Services.Models.VisualQA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Student;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StudentService(IStudentRepository studentRepository, IUnitOfWork unitOfWork)
    {
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
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
                IsApproved = c.IsApproved ?? false,
                ThumbnailImageUrl = c.MedicalImages.FirstOrDefault()?.ImageUrl,
                Tags = c.CaseTags?.Select(ct => ct.Tag.Name).ToList()
            })
            .ToList();
    }

    public async Task<IReadOnlyList<CaseListItemDto>> GetFilteredCasesAsync(Guid studentId, CaseFilterRequestDto filter)
    {
        var repoFilter = new CaseFilter
        {
            CategoryId = filter.CategoryId,
            Difficulty = filter.Difficulty,
            Location = filter.Location,
            LessonType = filter.LessonType
        };
        var cases = await _studentRepository.GetFilteredCasesAsync(repoFilter);

        return cases
            .Select(c => new CaseListItemDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                Difficulty = c.Difficulty,
                CategoryName = c.Category?.Name,
                IsApproved = c.IsApproved ?? false,
                ThumbnailImageUrl = c.MedicalImages.FirstOrDefault()?.ImageUrl,
                Tags = c.CaseTags?.Select(ct => ct.Tag.Name).ToList()
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
            IsApproved = entity.IsApproved ?? false,
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
        var coordinatesJson = TryParseCoordinatesJson(request.Coordinates);

        var entity = new CaseAnnotation
        {
            Id = Guid.NewGuid(),
            ImageId = request.ImageId,
            Label = request.Label,
            Coordinates = coordinatesJson,
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
        var language = NormalizeLanguage(request.Language);

        var question = new StudentQuestion
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CaseId = request.CaseId == Guid.Empty ? null : request.CaseId,
            AnnotationId = request.AnnotationId,
            QuestionText = request.QuestionText,
            Language = language,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _studentRepository.CreateStudentQuestionAsync(question);

        return new StudentQuestionDto
        {
            Id = created.Id,
            StudentId = created.StudentId,
            CaseId = created.CaseId ?? Guid.Empty,
            AnnotationId = created.AnnotationId,
            QuestionText = created.QuestionText,
            Language = created.Language,
            CreatedAt = created.CreatedAt
        };
    }


    public async Task<StudentQuestionDto> CreateVisualQAQuestionAsync(Guid studentId, VisualQARequestDto request)
    {
        var language = NormalizeLanguage(request.Language);

        var question = new StudentQuestion
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CaseId = request.CaseId,
            AnnotationId = request.AnnotationId,
            QuestionText = request.QuestionText,

            Language = language,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _studentRepository.CreateStudentQuestionAsync(question);

        return new StudentQuestionDto
        {
            Id = created.Id,
            StudentId = created.StudentId,
            CaseId = created.CaseId ?? Guid.Empty,
            AnnotationId = created.AnnotationId,
            QuestionText = created.QuestionText,
            Language = created.Language,
            CreatedAt = created.CreatedAt
        };
    }

    public async Task SaveVisualQAAnswerAsync(Guid questionId, VisualQAResponseDto response)
    {
        var answer = new CaseAnswer
        {
            Id = Guid.NewGuid(),
            QuestionId = questionId,
            AnswerText = response.AnswerText,
            StructuredDiagnosis = response.SuggestedDiagnosis,
            DifferentialDiagnoses = response.DifferentialDiagnoses,

            Status = "answered",
            GeneratedAt = DateTime.UtcNow
        };

        await _studentRepository.CreateCaseAnswerAsync(answer);

        if (response.Citations != null && response.Citations.Count > 0)
        {
            var citations = response.Citations.Select(c => new Citation
            {
                Id = Guid.NewGuid(),
                AnswerId = answer.Id,
                ChunkId = c.ChunkId,
                SimilarityScore = c.SimilarityScore

            });

            await _studentRepository.AddCitationsAsync(citations);
        }
    }

    private static string? NormalizeLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "vi";
        var v = value.Trim().ToLowerInvariant();
        if (v == "vi" || v == "vie") return "vi";
        if (v == "en" || v == "eng") return "en";
        return "vi";
    }

    private static string? TryParseCoordinatesJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var _ = JsonDocument.Parse(value);
            return value;
        }
        catch
        {
            return null;

        }
    }

    public async Task<IReadOnlyList<StudentQuestionHistoryItemDto>> GetQuestionHistoryAsync(Guid studentId)
    {
        var items = await _studentRepository.GetQuestionsByStudentAsync(studentId);

        return items
            .Select(q => new StudentQuestionHistoryItemDto
            {
                Id = q.Id,
                CaseId = q.CaseId ?? Guid.Empty,
                QuestionText = q.QuestionText,
                CreatedAt = q.CreatedAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<StudentAnnouncementDto>> GetAnnouncementsAsync(Guid studentId)
    {
        var announcements = await _studentRepository.GetAnnouncementsForStudentAsync(studentId);

        return announcements
            .Select(a => new StudentAnnouncementDto
            {
                Id = a.Id,
                ClassId = a.ClassId,
                ClassName = a.Class?.ClassName,
                Title = a.Title,
                Content = a.Content,
                CreatedAt = a.CreatedAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<QuizListItemDto>> GetAvailableQuizzesAsync(Guid studentId)
    {
        var utcNow = DateTime.UtcNow;
        var quizzes = await _studentRepository.GetQuizzesForStudentAsync(studentId, utcNow);
        return quizzes.Select(q => new QuizListItemDto
        {
            QuizId = q.Id,
            Title = q.Title ?? string.Empty,
            OpenTime = q.OpenTime,
            CloseTime = q.CloseTime,
            TimeLimit = q.TimeLimit,
            PassingScore = q.PassingScore,
            IsCompleted = false,
            Score = null
        }).ToList();
    }


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
            .Select(q => new StudentQuizQuestionDto
            {
                QuestionId = q.Id,
                QuestionText = q.QuestionText,
                Type = q.Type,
                CaseId = q.CaseId ?? Guid.Empty
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
    //co 2 ham student submit question va submit quiz, ham submit question de luu tung cau hoi 1, ham submit quiz de tinh diem va ket thuc quiz

    //===================== phan nam =====================   
   
    private string? GetOptionText(QuizQuestion question, string? optionKey)
    {
        return optionKey?.ToUpper() switch
        {
            "A" => question.OptionA,
            "B" => question.OptionB,
            "C" => question.OptionC,
            "D" => question.OptionD,
            _ => null
        };
    }

    public async Task<StudentSubmitQuestionResponseDto> SubmitQuizAsync(Guid studentId, StudentSubmitQuestionDto submit)
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

        return new StudentSubmitQuestionResponseDto
        {
            QuizTitle = quiz.Title,
            QuestionText = question.QuestionText,
            OptionA = question.OptionA,
            OptionB = question.OptionB,
            OptionC = question.OptionC,
            OptionD = question.OptionD,
            StudentAnswer = submit.StudentAnswer?.ToUpper(),
            StudentAnswerText = GetOptionText(question, submit.StudentAnswer), 
            CorrectAnswer = question.CorrectAnswer,
            CorrectAnswerText = GetOptionText(question, question.CorrectAnswer),
            IsCorrect = isCorrect
        };
    }

                                       //===================== phan tran =====================   
    //public async Task<QuizResultDto> SubmitQuizAsync(Guid studentId, SubmitQuizRequestDto request)
    //{
    //    await _unitOfWork.BeginTransactionAsync();
    //    try
    //    {
    //        var attempt = await _studentRepository.GetQuizAttemptByIdAsync(request.AttemptId, studentId);
    //        if (attempt == null)
    //        {
    //            await _unitOfWork.RollbackTransactionAsync();
    //            throw new InvalidOperationException(
    //                "Lần làm quiz không tồn tại hoặc không thuộc về sinh viên này. Kiểm tra attemptId và studentId (phải trùng với student_id của lần làm bài trong bảng quiz_attempts).");
    //        }

    //        var quiz = await _studentRepository.GetQuizWithQuestionsAsync(attempt.QuizId);
    //        if (quiz == null)
    //        {
    //            await _unitOfWork.RollbackTransactionAsync();
    //            throw new InvalidOperationException("Quiz không tồn tại.");
    //        }

    //        var questionDict = quiz.QuizQuestions.ToDictionary(q => q.Id, q => q);

    //        var answers = new List<StudentQuizAnswer>();
    //        var correctCount = 0;
    //        var unmatchedQuestionIds = new List<Guid>();

    //        foreach (var a in request.Answers)
    //        {
    //            if (!questionDict.TryGetValue(a.QuestionId, out var question))
    //            {
    //                unmatchedQuestionIds.Add(a.QuestionId);
    //                continue;
    //            }

    //            var isCorrect = false;
    //            if (question.CorrectAnswer != null && a.StudentAnswer != null)
    //            {
    //                isCorrect = string.Equals(
    //                    question.CorrectAnswer.Trim(),
    //                    a.StudentAnswer.Trim(),
    //                    StringComparison.OrdinalIgnoreCase);
    //            }

    //            if (isCorrect)
    //            {
    //                correctCount++;
    //            }

    //            answers.Add(new StudentQuizAnswer
    //            {
    //                Id = Guid.NewGuid(),
    //                AttemptId = attempt.Id,
    //                QuestionId = question.Id,
    //                StudentAnswer = a.StudentAnswer,
    //                IsCorrect = isCorrect
    //            });
    //        }

    //        if (unmatchedQuestionIds.Count > 0)
    //        {
    //            var validIds = string.Join(", ", questionDict.Keys.OrderBy(x => x));
    //            throw new InvalidOperationException(
    //                "Một hoặc nhiều questionId không thuộc quiz này: " +
    //                string.Join(", ", unmatchedQuestionIds.Distinct()) +
    //                ". Phải dùng QuestionId từ POST /api/Students/quizzes/{quizId}/start (mỗi phần tử questions[].questionId — đó là cột id trong bảng quiz_questions). " +
    //                "Không dùng quizId (bảng quizzes) làm questionId. " +
    //                (string.IsNullOrEmpty(validIds)
    //                    ? "Quiz hiện không có câu hỏi nào trong quiz_questions."
    //                    : $"Các questionId hợp lệ: {validIds}."));
    //        }

    //        var totalQuestions = quiz.QuizQuestions.Count;
    //        double? score = null;
    //        if (totalQuestions > 0)
    //        {
    //            score = correctCount * 100.0 / totalQuestions;
    //        }

    //        attempt.Score = score;
    //        attempt.CompletedAt = DateTime.UtcNow;

    //        await _studentRepository.AddStudentQuizAnswersAsync(answers);
    //        await _studentRepository.UpdateQuizAttemptAsync(attempt);

    //        await _unitOfWork.CommitTransactionAsync();

    //        var passed = score.HasValue && quiz.PassingScore.HasValue && score.Value >= quiz.PassingScore.Value;

    //        return new QuizResultDto
    //        {
    //            AttemptId = attempt.Id,
    //            QuizId = attempt.QuizId,
    //            Score = score,
    //            PassingScore = quiz.PassingScore,
    //            Passed = passed
    //        };
    //    }
    //    catch
    //    {
    //        await _unitOfWork.RollbackTransactionAsync();
    //        throw;
    //    }
    //}

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
