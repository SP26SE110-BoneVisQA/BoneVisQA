using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Constants;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerService : ILecturerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IMedicalCaseService _medicalCaseService;

    public LecturerService(IUnitOfWork unitOfWork, IEmailService emailService, IMedicalCaseService medicalCaseService)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _medicalCaseService = medicalCaseService;
    }

    /// <summary>
    /// Chuyển DateTime sang UTC một cách chính xác:
    /// - Kind=Utc: giữ nguyên
    /// - Kind=Local: cộng thêm offset để ra UTC
    /// - Kind=Unspecified: giả sử là giờ local → cộng offset để ra UTC
    /// Kết quả luôn được đánh dấu Kind=Utc để đảm bảo lưu đúng múi giờ.
    /// </summary>
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

    private async Task EnsureLecturerOwnsClassAsync(Guid lecturerId, Guid classId)
    {
        var ownsClass = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId && c.LecturerId == lecturerId)
            .AnyAsync();
        if (!ownsClass)
            throw new UnauthorizedAccessException("Lecturer does not have permission to access this class.");
    }

    public async Task<ClassDto> CreateClassAsync(Guid lecturerId, CreateClassRequestDto request)
    {
        var now = DateTime.UtcNow;
        var entity = new AcademicClass
        {
            Id = Guid.NewGuid(),
            ClassName = request.ClassName,
            Semester = request.Semester,
            LecturerId = lecturerId,
            ExpertId = request.ExpertId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _unitOfWork.AcademicClassRepository.AddAsync(entity);
        await _unitOfWork.SaveAsync();

        return new ClassDto
        {
            Id = entity.Id,
            ClassName = entity.ClassName,
            Semester = entity.Semester,
            LecturerId = entity.LecturerId,
            ExpertId = entity.ExpertId,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<IReadOnlyList<ClassDto>> GetClassesForLecturerAsync(Guid lecturerId)
    {
        var classes = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.LecturerId == lecturerId)
            .ToListAsync();

        return classes
            .Select(c => new ClassDto
            {
                Id = c.Id,
                ClassName = c.ClassName,
                Semester = c.Semester,
                LecturerId = c.LecturerId,
                ExpertId = c.ExpertId,
                CreatedAt = c.CreatedAt
            })
            .ToList();
    }

    public async Task<bool> EnrollStudentAsync(Guid lecturerId, Guid classId, Guid studentId)
    {
        await EnsureLecturerOwnsClassAsync(lecturerId, classId);
        var existing = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.ClassId == classId && e.StudentId == studentId)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            return false;
        }

        // Get class info to store class name
        var classEntity = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId)
            .FirstOrDefaultAsync();

        var enrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = studentId,
            ClassName = classEntity?.ClassName,
            EnrolledAt = DateTime.UtcNow
        };

        await _unitOfWork.ClassEnrollmentRepository.AddAsync(enrollment);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task<IReadOnlyList<StudentEnrollmentDto>> EnrollStudentsAsync(Guid lecturerId, Guid classId, EnrollStudentsRequestDto request)
    {
        await EnsureLecturerOwnsClassAsync(lecturerId, classId);
        // Get class info to store class name
        var classEntity = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId)
            .FirstOrDefaultAsync();

        var className = classEntity?.ClassName;

        foreach (var studentId in request.StudentIds)
        {
            var existing = await _unitOfWork.ClassEnrollmentRepository
                .FindByCondition(e => e.ClassId == classId && e.StudentId == studentId)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                continue;
            }

            var enrollment = new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                StudentId = studentId,
                ClassName = className,
                EnrolledAt = DateTime.UtcNow
            };

            await _unitOfWork.ClassEnrollmentRepository.AddAsync(enrollment);
        }

        await _unitOfWork.SaveAsync();
        return await GetStudentsInClassAsync(lecturerId, classId);
    }

    public async Task<bool> RemoveStudentAsync(Guid lecturerId, Guid classId, Guid studentId)
    {
        await EnsureLecturerOwnsClassAsync(lecturerId, classId);
        var enrollment = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.ClassId == classId && e.StudentId == studentId)
            .FirstOrDefaultAsync();

        if (enrollment == null)
        {
            return false;
        }

        await _unitOfWork.ClassEnrollmentRepository.DeleteAsync(enrollment.Id);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task<IReadOnlyList<StudentEnrollmentDto>> GetStudentsInClassAsync(Guid lecturerId, Guid classId)
    {
        await EnsureLecturerOwnsClassAsync(lecturerId, classId);
        var enrollments = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.ClassId == classId)
            .Include(e => e.Student)
            .ToListAsync();

        return enrollments
            .Select(e => new StudentEnrollmentDto
            {
                EnrollmentId = e.Id,
                StudentId = e.StudentId,
                StudentName = e.Student?.FullName ?? string.Empty,
                StudentEmail = e.Student?.Email ?? string.Empty,
                StudentCode = e.Student?.SchoolCohort,
                ClassName = e.ClassName,
                EnrolledAt = e.EnrolledAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<StudentEnrollmentDto>> GetAvailableStudentsAsync(Guid lecturerId, Guid classId)
    {
        await EnsureLecturerOwnsClassAsync(lecturerId, classId);
        var enrolledStudentIds = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var studentRole = await _unitOfWork.RoleRepository
            .FindByCondition(r => r.Name == "Student")
            .FirstOrDefaultAsync();

        if (studentRole == null)
        {
            return new List<StudentEnrollmentDto>();
        }

        var studentUserIds = await _unitOfWork.UserRoleRepository
            .FindByCondition(ur => ur.RoleId == studentRole.Id)
            .Select(ur => ur.UserId)
            .ToListAsync();

        var availableStudents = await _unitOfWork.UserRepository
            .FindByCondition(u => studentUserIds.Contains(u.Id) && !enrolledStudentIds.Contains(u.Id))
            .ToListAsync();

        return availableStudents
            .Select(u => new StudentEnrollmentDto
            {
                EnrollmentId = null,
                StudentId = u.Id,
                StudentName = u.FullName,
                StudentEmail = u.Email,
                StudentCode = u.SchoolCohort,
                EnrolledAt = null
            })
            .ToList();
    }

    public async Task<AnnouncementDto> CreateAnnouncementAsync(Guid lecturerId, Guid classId, CreateAnnouncementRequestDto request)
    {
        await EnsureLecturerOwnsClassAsync(lecturerId, classId);
        var now = DateTime.UtcNow;

        // Get class and lecturer information
        var academicClass = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId)
            .Include(c => c.Lecturer)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Class not found.");

        var lecturerName = academicClass.Lecturer?.FullName ?? "Lecturer";
        var className = academicClass.ClassName;

        // Create announcement entity
        var entity = new Announcement
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            Title = request.Title,
            Content = request.Content,
            SendEmail = request.SendEmail,
            CreatedAt = now
        };

        await _unitOfWork.AnnouncementRepository.AddAsync(entity);
        await _unitOfWork.SaveAsync();

        if (request.SendEmail)
            await SendAnnouncementEmailsToEnrolledStudentsAsync(classId, lecturerName, className, request.Title, request.Content);

        return new AnnouncementDto
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            Title = entity.Title,
            Content = entity.Content,
            SendEmail = entity.SendEmail,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<AnnouncementDto> UpdateAnnouncementAsync(Guid classId, Guid announcementId, UpdateAnnouncementRequestDto request)
    {
        var entity = await _unitOfWork.AnnouncementRepository
            .FindByCondition(a => a.Id == announcementId && a.ClassId == classId)
            .Include(a => a.Class)
                .ThenInclude(c => c!.Lecturer)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Announcement not found.");

        entity.Title = request.Title.Trim();
        entity.Content = request.Content.Trim();

        await _unitOfWork.AnnouncementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveAsync();

        var lecturerName = entity.Class?.Lecturer?.FullName ?? "Lecturer";
        var className = entity.Class?.ClassName ?? "";
        if (request.SendEmail)
            await SendAnnouncementEmailsToEnrolledStudentsAsync(classId, lecturerName, className, entity.Title, entity.Content);

        return new AnnouncementDto
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            ClassName = entity.Class?.ClassName,
            Title = entity.Title,
            Content = entity.Content,
            SendEmail = entity.SendEmail,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<bool> DeleteAnnouncementAsync(Guid classId, Guid announcementId)
    {
        var entity = await _unitOfWork.AnnouncementRepository
            .FindByCondition(a => a.Id == announcementId && a.ClassId == classId)
            .FirstOrDefaultAsync();
        if (entity == null)
            return false;

        await _unitOfWork.AnnouncementRepository.RemoveAsync(entity);
        await _unitOfWork.SaveAsync();
        return true;
    }

    private async Task SendAnnouncementEmailsToEnrolledStudentsAsync(
        Guid classId,
        string lecturerName,
        string className,
        string title,
        string content)
    {
        var enrolledStudents = await _unitOfWork.Context.ClassEnrollments
            .Include(e => e.Student)
            .Where(e => e.ClassId == classId)
            .Select(e => e.Student)
            .ToListAsync();

        foreach (var student in enrolledStudents)
        {
            if (student == null || string.IsNullOrWhiteSpace(student.Email)) continue;
            var studentName = student.FullName ?? "Student";
            _ = _emailService.SendAnnouncementEmailAsync(
                student.Email,
                studentName,
                lecturerName,
                className,
                title,
                content);
        }
    }

    //====================================================================================================
    public async Task<List<QuizQuestionDto>> GetQuizQuestionsAsync(Guid quizId)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId)
            ?? throw new KeyNotFoundException("Quiz not found.");

        var questions = await _unitOfWork.QuizQuestionRepository
            .FindIncludeAsync(
                q => q.QuizId == quizId,
                q => q.Quiz,
                q => q.Case  // ✅ add include Case
            );

        return questions.Select(q => new QuizQuestionDto
        {
            Id = q.Id,
            QuizId = q.QuizId,
            QuizTitle = quiz.Title,
            CaseId = q.CaseId,
            CaseTitle = q.Case?.Title ?? "",
            QuestionText = q.QuestionText,
            Type = q.Type?.ToString(),
            OptionA = q.OptionA,
            OptionB = q.OptionB,
            OptionC = q.OptionC,
            OptionD = q.OptionD,
            CorrectAnswer = q.CorrectAnswer,
            ImageUrl = q.ImageUrl
        }).ToList();
    }

    public async Task<QuizQuestionDto?> GetQuizQuestionByIdAsync(Guid questionId)
    {
        var questions = await _unitOfWork.QuizQuestionRepository
            .FindIncludeAsync(
                q => q.Id == questionId,
                q => q.Quiz,
                q => q.Case
            );

        var question = questions.FirstOrDefault();
        if (question == null)
            return null;

        return new QuizQuestionDto
        {
            Id = question.Id,
            QuizId = question.QuizId,
            QuizTitle = question.Quiz?.Title ?? "",
            CaseId = question.CaseId,
            CaseTitle = question.Case?.Title,
            QuestionText = question.QuestionText,
            Type = question.Type?.ToString(),
            OptionA = question.OptionA,
            OptionB = question.OptionB,
            OptionC = question.OptionC,
            OptionD = question.OptionD,
            CorrectAnswer = question.CorrectAnswer,
            ImageUrl = question.ImageUrl
        };
    }

    public async Task<QuizDto> CreateQuizAsync(CreateQuizRequestDto request, Guid? creatingUserId = null)
    {
        var now = DateTime.UtcNow;

        if (request.ClassId != Guid.Empty)
        {
            _ = await _unitOfWork.AcademicClassRepository.GetByIdAsync(request.ClassId)
                ?? throw new KeyNotFoundException("Class not found.");
        }

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Topic = request.Topic,
            Difficulty = request.Difficulty,
            Classification = request.Classification,
            IsAiGenerated = request.IsAiGenerated,
            IsVerifiedCurriculum = request.IsVerifiedCurriculum,
            CreatedByExpertId = creatingUserId,
            OpenTime = ToUtc(request.OpenTime),
            CloseTime = ToUtc(request.CloseTime),
            TimeLimit = request.TimeLimit,
            PassingScore = request.PassingScore, // Lecturer quiz uses 100-point scale directly
            CreatedAt = now
        };

        await _unitOfWork.QuizRepository.AddAsync(quiz);
        await _unitOfWork.SaveAsync();

        if (request.ClassId != Guid.Empty)
        {
            var classQuizSession = new ClassQuizSession
            {
                Id = Guid.NewGuid(),
                ClassId = request.ClassId,
                QuizId = quiz.Id,
                OpenTime = ToUtc(request.OpenTime),
                CloseTime = ToUtc(request.CloseTime),
                TimeLimitMinutes = request.TimeLimit,
                PassingScore = request.PassingScore,
                CreatedAt = now
            };
            await _unitOfWork.ClassQuizSessionRepository.AddAsync(classQuizSession);
            await _unitOfWork.SaveAsync();
        }

        return new QuizDto
        {
            Id = quiz.Id,
            ClassId = request.ClassId != Guid.Empty ? request.ClassId : Guid.Empty,
            Title = quiz.Title,
            Topic = quiz.Topic,
            IsAiGenerated = quiz.IsAiGenerated,
            IsVerifiedCurriculum = quiz.IsVerifiedCurriculum,
            Difficulty = quiz.Difficulty,
            Classification = quiz.Classification,
            OpenTime = quiz.OpenTime,
            CloseTime = quiz.CloseTime,
            TimeLimit = quiz.TimeLimit,
            PassingScore = NormalizePassingScore(quiz.PassingScore, quiz.IsAiGenerated),
            CreatedAt = quiz.CreatedAt
        };
    }

    public async Task<bool> DeleteQuizAsync(Guid quizId)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId);
        if (quiz == null)
            return false;

        // ============================================================
        // IMPORTANT: Only delete the original Quiz if it belongs to the lecturer
        // - Expert quizzes (CreatedByExpertId != null) must be kept in Expert Library
        // - Only remove the class link (ClassQuizSession) and attempt history for expert quizzes
        // - For lecturer-owned quizzes (CreatedByExpertId == null), delete everything including the quiz itself
        // ============================================================

        var isExpertQuiz = quiz.CreatedByExpertId.HasValue;

        // 1. Remove from all class assignments (ClassQuizSession)
        var classSessions = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => cqs.QuizId == quizId)
            .ToListAsync();

        foreach (var session in classSessions)
            _unitOfWork.Context.ClassQuizSessions.Remove(session);

        // 2. Remove quiz attempts (attempt history)
        var attempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.QuizId == quizId)
            .ToListAsync();

        foreach (var attempt in attempts)
            _unitOfWork.Context.QuizAttempts.Remove(attempt);

        // 3. Remove quiz questions (only if we're deleting the quiz itself)
        if (!isExpertQuiz)
        {
            var questions = await _unitOfWork.Context.QuizQuestions
                .Where(q => q.QuizId == quizId)
                .ToListAsync();

            foreach (var question in questions)
                _unitOfWork.Context.QuizQuestions.Remove(question);
        }

        // 4. Delete the original Quiz only if it's NOT an expert quiz
        if (!isExpertQuiz)
        {
            _unitOfWork.Context.Quizzes.Remove(quiz);
        }

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

    public async Task<QuizQuestionDto> AddQuizQuestionAsync(Guid quizId, CreateQuizQuestionDto request)
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
            ImageUrl = request.ImageUrl
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
            Type = question.Type?.ToString(),
            OptionA = question.OptionA,
            OptionB = question.OptionB,
            OptionC = question.OptionC,
            OptionD = question.OptionD,
            CorrectAnswer = question.CorrectAnswer,
            ImageUrl = question.ImageUrl
        };
    }

    public async Task<List<QuizQuestionDto>> AddQuizQuestionsBatchAsync(Guid quizId, List<CreateQuizQuestionDto> requests)
    {
        if (requests == null || requests.Count == 0)
            return new List<QuizQuestionDto>();

        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId)
            ?? throw new KeyNotFoundException("Quiz not found.");

        var questions = new List<QuizQuestion>();
        var result = new List<QuizQuestionDto>();

        foreach (var request in requests)
        {
            MedicalCase? medicalCase = null;
            if (request.CaseId.HasValue)
            {
                medicalCase = await _unitOfWork.MedicalCaseRepository
                    .GetByIdAsync(request.CaseId.Value)
                    ?? throw new KeyNotFoundException($"Medical case with ID {request.CaseId} not found.");
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
                ImageUrl = request.ImageUrl
            };

            questions.Add(question);
        }

        await _unitOfWork.QuizQuestionRepository.AddRangeAsync(questions);
        await _unitOfWork.SaveAsync();

        foreach (var question in questions)
        {
            // Fetch medical case again for each question (could be optimized)
            MedicalCase? medicalCase = null;
            if (question.CaseId.HasValue)
            {
                medicalCase = await _unitOfWork.MedicalCaseRepository.GetByIdAsync(question.CaseId.Value);
            }

            result.Add(new QuizQuestionDto
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
                ImageUrl = question.ImageUrl
            });
        }

        return result;
    }

    public async Task<UpdateQuizsQuestionResponseDto> UpdateQuizQuestionAsync(Guid questionId, UpdateQuizsQuestionRequestDto request)
    {
        var entity = await _unitOfWork.QuizQuestionRepository.GetByIdAsync(questionId)
            ?? throw new KeyNotFoundException("Question not found.");

        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(entity.QuizId)
            ?? throw new KeyNotFoundException("Quiz not found.");

        if (entity == null)
            throw new KeyNotFoundException("Question not found.");


        entity.QuestionText = request.QuestionText;
        var questionType = string.IsNullOrEmpty(request.Type)
            ? QuestionType.MultipleChoice
            : Enum.TryParse<QuestionType>(request.Type, out var parsed) ? parsed : QuestionType.MultipleChoice;
        entity.Type = questionType;
        entity.OptionA = request.OptionA;
        entity.OptionB = request.OptionB;
        entity.OptionC = request.OptionC;
        entity.OptionD = request.OptionD;
        entity.CorrectAnswer = request.CorrectAnswer;
        entity.ImageUrl = request.ImageUrl;

        await _unitOfWork.QuizQuestionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveAsync();

        return new UpdateQuizsQuestionResponseDto
        {
            QuizTitle = quiz.Title,
            QuestionText = request.QuestionText,
            Type = request.Type,
            OptionA = request.OptionA,
            OptionB = request.OptionB,
            OptionC = request.OptionC,
            OptionD = request.OptionD,
            CorrectAnswer = request.CorrectAnswer,
            ImageUrl = request.ImageUrl,
        };
    }



    //================================================ code tran ====================================================


    //public async Task<QuizDto> CreateQuizAsync(Guid classId, CreateQuizRequestDto request)
    //{
    //    var now = DateTime.UtcNow;
    //    var entity = new Quiz
    //    {
    //        Id = Guid.NewGuid(),
    //        Title = request.Title,
    //        OpenTime = request.OpenTime,
    //        CloseTime = request.CloseTime,
    //        TimeLimit = request.TimeLimit,
    //        PassingScore = request.PassingScore,
    //        CreatedAt = now
    //    };

    //    await _unitOfWork.QuizRepository.AddAsync(entity);
    //    await _unitOfWork.SaveAsync();

    //    var session = new ClassQuizSession { ClassId = classId, QuizId = entity.Id, CreatedAt = now };
    //    await _unitOfWork.ClassQuizSessionRepository.AddAsync(session);
    //    await _unitOfWork.SaveAsync();

    //    return new QuizDto
    //    {
    //        Id = entity.Id,
    //        ClassId = classId,
    //        Title = entity.Title,
    //        OpenTime = entity.OpenTime,
    //        CloseTime = entity.CloseTime,
    //        TimeLimit = entity.TimeLimit,
    //        PassingScore = entity.PassingScore
    //    };
    //}
    //public async Task<QuizQuestionDto> AddQuizQuestionAsync(CreateQuizQuestionRequestDto request)
    //{
    //    var entity = new QuizQuestion
    //    {
    //        Id = Guid.NewGuid(),
    //        QuizId = request.QuizId,
    //        CaseId = request.CaseId,
    //        QuestionText = request.QuestionText,
    //        Type = request.Type,
    //        CorrectAnswer = request.CorrectAnswer
    //    };

    //    await _unitOfWork.QuizQuestionRepository.AddAsync(entity);
    //    await _unitOfWork.SaveAsync();

    //    return new QuizQuestionDto
    //    {
    //        Id = entity.Id,
    //        QuizId = entity.QuizId,
    //        CaseId = entity.CaseId,
    //        QuestionText = entity.QuestionText,
    //        Type = entity.Type ?? "multiple_choice",
    //        CorrectAnswer = entity.CorrectAnswer
    //    };
    //}

    //public async Task<IReadOnlyList<QuizQuestionDto>> GetQuizQuestionsAsync(Guid quizId)
    //{
    //    var questions = await _unitOfWork.QuizQuestionRepository
    //        .FindByCondition(q => q.QuizId == quizId)
    //        .Include(q => q.Case)
    //        .ToListAsync();

    //    return questions
    //        .Select(q => new QuizQuestionDto
    //        {
    //            Id = q.Id,
    //            QuizId = q.QuizId,
    //            CaseId = q.CaseId ?? Guid.Empty,
    //            CaseTitle = q.Case?.Title,
    //            QuestionText = q.QuestionText,
    //            Type = q.Type ?? "multiple_choice",
    //            CorrectAnswer = q.CorrectAnswer
    //        })
    //        .ToList();
    //}

    //public async Task<bool> UpdateQuizQuestionAsync(Guid questionId, UpdateQuizQuestionRequestDto request)
    //{
    //    var entity = await _unitOfWork.QuizQuestionRepository
    //        .FindByCondition(q => q.Id == questionId)
    //        .FirstOrDefaultAsync();

    //    if (entity == null)
    //    {
    //        return false;
    //    }

    //    entity.QuestionText = request.QuestionText;
    //    entity.Type = request.Type ?? entity.Type;
    //    entity.CorrectAnswer = request.CorrectAnswer ?? entity.CorrectAnswer;

    //    await _unitOfWork.QuizQuestionRepository.UpdateAsync(entity);
    //    await _unitOfWork.SaveAsync();
    //    return true;
    //}

    public async Task<bool> DeleteQuizQuestionAsync(Guid questionId)
    {
        var entity = await _unitOfWork.QuizQuestionRepository
            .FindByCondition(q => q.Id == questionId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return false;
        }

        await _unitOfWork.QuizQuestionRepository.DeleteAsync(entity.Id);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task<IReadOnlyList<CaseDto>> GetAllCasesAsync()
    {
        var cases = await _unitOfWork.MedicalCaseRepository
            .FindByCondition(c => true)
            .Include(c => c.Category)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return cases
            .Select(c => new CaseDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                Difficulty = c.Difficulty,
                CategoryName = c.Category?.Name,
                IsApproved = c.IsApproved ?? false,

                IsActive = c.IsActive ?? false,
                CreatedAt = c.CreatedAt
            })
            .ToList();
    }

    public async Task<ClassStatsDto> GetClassStatsAsync(Guid classId)
    {
        // Do not use Task.WhenAll for multiple queries on the same DbContext - EF Core is not thread-safe, may cause 500 errors.
        var studentIds = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var quizIdsInClass = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Where(cqs => cqs.ClassId == classId)
            .Select(cqs => cqs.QuizId)
            .Distinct()
            .ToListAsync();

        var totalStudents = studentIds.Count;

        var totalCasesViewed = studentIds.Count > 0
            ? await _unitOfWork.CaseViewLogRepository
                .FindByCondition(v => studentIds.Contains(v.StudentId))
                .CountAsync()
            : 0;

        var totalQuestionsAsked = studentIds.Count > 0
            ? await _unitOfWork.Context.QaMessages
                .Where(m => m.Role == "User" && studentIds.Contains(m.Session.StudentId))
                .CountAsync()
            : 0;

        var avgQuizScore = await GetAvgQuizScoreForClassAsync(studentIds, quizIdsInClass);

        var caseAssignmentCount = await _unitOfWork.Context.ClassCases
            .AsNoTracking()
            .CountAsync(cc => cc.ClassId == classId);

        return new ClassStatsDto
        {
            ClassId = classId,
            TotalStudents = totalStudents,
            TotalCasesViewed = totalCasesViewed,
            TotalQuestionsAsked = totalQuestionsAsked,
            AvgQuizScore = avgQuizScore,
            TotalAssignments = caseAssignmentCount + quizIdsInClass.Count,
            CompletedAssignments = 0
        };
    }

    private async Task<double?> GetAvgQuizScoreForClassAsync(List<Guid> studentIds, List<Guid> quizIds)
    {
        if (studentIds.Count == 0 || quizIds.Count == 0)
            return null;

        var scores = await _unitOfWork.QuizAttemptRepository
            .FindByCondition(a =>
                studentIds.Contains(a.StudentId)
                && quizIds.Contains(a.QuizId)
                && a.Score.HasValue
                && a.CompletedAt.HasValue)
            .Select(a => a.Score!.Value)
            .ToListAsync();

        return scores.Count > 0 ? scores.Average() : null;
    }

    public async Task<IReadOnlyList<CaseDto>> AssignCasesToClassAsync(Guid classId, AssignCasesToClassRequestDto request)
    {
        var classEntity = await _unitOfWork.AcademicClassRepository.GetByIdAsync(classId)
            ?? throw new KeyNotFoundException("Class not found.");

        var caseIds = request.CaseIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (caseIds.Count == 0)
            return new List<CaseDto>();

        var existing = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId && caseIds.Contains(cc.CaseId))
            .Select(cc => cc.CaseId)
            .ToListAsync();

        var toAdd = caseIds
            .Except(existing)
            .Select(caseId => new ClassCase
            {
                ClassId = classEntity.Id,
                CaseId = caseId,
                AssignedAt = DateTime.UtcNow,
                IsMandatory = false
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _unitOfWork.ClassCaseRepository.AddRangeAsync(toAdd);
        }

        await _unitOfWork.SaveAsync();

        var assignedCases = await _unitOfWork.Context.ClassCases
            .Where(cc => cc.ClassId == classId)
            .Select(cc => cc.Case)
            .Include(c => c.Category)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return assignedCases.Select(c => new CaseDto
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            Difficulty = c.Difficulty,
            CategoryName = c.Category?.Name,
            IsApproved = c.IsApproved ?? false,
            IsActive = c.IsActive ?? false,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    public async Task<bool> ApproveCaseAsync(Guid caseId, ApproveCaseRequestDto request)
    {
        var entity = await _unitOfWork.MedicalCaseRepository
            .FindByCondition(c => c.Id == caseId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return false;
        }

        entity.IsApproved = request.IsApproved;
        await _unitOfWork.MedicalCaseRepository.UpdateAsync(entity);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task<ClassDto?> GetClassByIdAsync(Guid lecturerId, Guid classId)
    {
        var c = await _unitOfWork.AcademicClassRepository
            .FindByCondition(x => x.Id == classId && x.LecturerId == lecturerId)
            .Include(x => x.Expert)
            .FirstOrDefaultAsync();
        if (c == null) return null;
        return new ClassDto
        {
            Id = c.Id, ClassName = c.ClassName, Semester = c.Semester,
            LecturerId = c.LecturerId, ExpertId = c.ExpertId,
            ExpertName = c.Expert?.FullName, CreatedAt = c.CreatedAt
        };
    }

    public async Task<ClassDto> UpdateClassAsync(Guid lecturerId, Guid classId, UpdateClassRequestDto request)
    {
        var c = await _unitOfWork.AcademicClassRepository
            .FindByCondition(x => x.Id == classId && x.LecturerId == lecturerId)
            .Include(x => x.Expert)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Class not found.");
        c.ClassName = request.ClassName;
        c.Semester = request.Semester;
        c.ExpertId = request.ExpertId;
        c.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.AcademicClassRepository.Update(c);
        await _unitOfWork.SaveAsync();
        return new ClassDto
        {
            Id = c.Id, ClassName = c.ClassName, Semester = c.Semester,
            LecturerId = c.LecturerId, ExpertId = c.ExpertId,
            ExpertName = c.Expert?.FullName, CreatedAt = c.CreatedAt
        };
    }

    public async Task<bool> DeleteClassAsync(Guid lecturerId, Guid classId)
    {
        var existing = await _unitOfWork.AcademicClassRepository
            .FindByCondition(x => x.Id == classId && x.LecturerId == lecturerId)
            .FirstOrDefaultAsync();
        if (existing == null) return false;
        await _unitOfWork.AcademicClassRepository.DeleteAsync(classId);
        await _unitOfWork.SaveAsync();
        return true;
    }

    private static string? ResolveSessionImageUrl(VisualQASession? session)
    {
        if (session == null)
            return null;
        if (!string.IsNullOrWhiteSpace(session.CustomImageUrl))
            return session.CustomImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(session.Image?.ImageUrl))
            return session.Image.ImageUrl.Trim();
        return session.Case?.MedicalImages?
            .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
            .ThenBy(m => m.Id)
            .Select(m => m.ImageUrl)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
    }

    public async Task<IReadOnlyList<LecturerTriageRowDto>> GetTriageListAsync(Guid classId)
    {
        var cls = await _unitOfWork.AcademicClassRepository.GetByIdAsync(classId)
            ?? throw new KeyNotFoundException("Class not found.");

        var studentIds = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        if (studentIds.Count == 0)
            return new List<LecturerTriageRowDto>();

        var sessions = await _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Image)
            .Include(s => s.Messages)
            .Where(s => studentIds.Contains(s.StudentId))
            .Where(s => s.Status == "PendingLecturerReview" || s.Status == "EscalatedToExpert")
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return sessions.Select(s =>
            {
                var userMessage = s.Messages
                    .Where(m => m.Role == "User")
                    .OrderBy(m => m.CreatedAt)
                    .FirstOrDefault();
                var assistantMessage = s.Messages
                    .Where(m => m.Role == "Assistant")
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();
                var flatImageUrl = ResolveSessionImageUrl(s);

                return new LecturerTriageRowDto
                {
                    AnswerId = s.Id,
                    QuestionId = userMessage?.Id ?? Guid.Empty,
                    StudentId = s.StudentId,
                    StudentName = s.Student?.FullName ?? string.Empty,
                    StudentEmail = s.Student?.Email,
                    ClassId = classId,
                    ClassName = cls.ClassName,
                    CaseId = s.CaseId,
                    CaseTitle = s.Case?.Title,
                    ThumbnailUrl = flatImageUrl,
                    ImageUrl = flatImageUrl,
                    QuestionText = userMessage?.Content ?? string.Empty,
                    AnswerText = assistantMessage?.Content,
                    Status = s.Status,
                    AiConfidenceScore = assistantMessage?.AiConfidenceScore,
                    AskedAt = s.CreatedAt,
                    IsEscalated = string.Equals(s.Status, "EscalatedToExpert", StringComparison.Ordinal),
                    EscalatedByName = null,
                    EscalatedAt = null
                };
            })
            .ToList();
    }

    public async Task<LectStudentQuestionDetailDto?> GetQuestionDetailAsync(Guid classId, Guid questionId)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Student)
            .Include(s => s.Case!)
                .ThenInclude(c => c.MedicalImages)
            .Include(s => s.Image)
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == questionId);

        if (session == null) return null;

        // verify class ownership via enrollment
        var enrollment = await _unitOfWork.ClassEnrollmentRepository
            .FirstOrDefaultAsync(e => e.ClassId == classId && e.StudentId == session.StudentId);
        if (enrollment == null) return null;

        var orderedMessages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();

        var userMessage = orderedMessages
            .Where(m => m.Role == "User")
            .FirstOrDefault();
        var latestAssistant = orderedMessages
            .Where(m => m.Role == "Assistant")
            .LastOrDefault();
        var detailImageUrl = ResolveSessionImageUrl(session);

        return new LectStudentQuestionDetailDto
        {
            Id = session.Id,
            StudentId = session.StudentId,
            StudentName = session.Student?.FullName ?? string.Empty,
            StudentEmail = session.Student?.Email ?? string.Empty,
            CaseId = session.CaseId,
            CaseTitle = session.Case?.Title,
            CaseDescription = session.Case?.Description,
            CaseThumbnailUrl = detailImageUrl,
            ImageUrl = detailImageUrl,
            CaseDifficulty = session.Case?.Difficulty,
            QuestionText = userMessage?.Content ?? string.Empty,
            Language = "vi",
            CreatedAt = session.CreatedAt,
            AnswerId = latestAssistant?.Id,
            AnswerText = latestAssistant?.Content,
            StructuredDiagnosis = latestAssistant?.SuggestedDiagnosis,
            DifferentialDiagnoses = DeserializeJsonArray(latestAssistant?.DifferentialDiagnoses),
            KeyImagingFindings = latestAssistant?.KeyImagingFindings,
            AnswerStatus = session.Status,
            AiConfidenceScore = latestAssistant?.AiConfidenceScore,
            ReviewedById = null,
            ReviewedByName = null,
            ReviewedAt = session.UpdatedAt,
            IsEscalated = string.Equals(session.Status, "EscalatedToExpert", StringComparison.Ordinal),
            EscalatedByName = null,
            EscalatedAt = null,
            Messages = orderedMessages.Select(m => new LectQAMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                Coordinates = m.Coordinates,
                CreatedAt = m.CreatedAt
            }).ToList()
        };
    }

    public async Task<LecturerAnswerDto> RespondToQuestionAsync(Guid classId, Guid questionId, RespondToQuestionRequestDto request)
    {
        var session = await _unitOfWork.Context.VisualQaSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == questionId)
            ?? throw new KeyNotFoundException("Q&A session not found.");

        // verify class ownership
        var enrollment = await _unitOfWork.ClassEnrollmentRepository
            .FirstOrDefaultAsync(e => e.ClassId == classId && e.StudentId == session.StudentId)
            ?? throw new InvalidOperationException("Lecturer does not have permission to answer this question.");

        var answer = session.Messages
            .Where(m => m.Role == "Assistant")
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();

        if (answer == null)
        {
            answer = new QAMessage
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Role = "Assistant",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Context.QaMessages.AddAsync(answer);
        }

        answer.Content = request.AnswerText;
        answer.SuggestedDiagnosis = request.StructuredDiagnosis;
        answer.DifferentialDiagnoses = SerializeJsonArray(request.DifferentialDiagnoses);
        answer.CreatedAt = DateTime.UtcNow;

        session.Status = request.Approve ? "LecturerApproved" : "PendingLecturerReview";
        session.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveAsync();

        return new LecturerAnswerDto
        {
            AnswerId = answer.Id,
            AnswerText = answer.Content,
            StructuredDiagnosis = answer.SuggestedDiagnosis,
            DifferentialDiagnoses = request.DifferentialDiagnoses,
            Status = session.Status,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static List<string>? DeserializeJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string? SerializeJsonArray(List<string>? values)
    {
        if (values == null || values.Count == 0)
            return null;
        return JsonSerializer.Serialize(values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList());
    }

    public async Task<IReadOnlyList<ClassStudentProgressDto>> GetClassStudentProgressAsync(Guid classId)
    {
        var cls = await _unitOfWork.AcademicClassRepository.GetByIdAsync(classId)
            ?? throw new KeyNotFoundException("Class not found.");

        var enrollments = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.ClassId == classId)
            .Include(e => e.Student)
            .ToListAsync();

        var studentIds = enrollments.Select(e => e.StudentId).ToList();
        if (studentIds.Count == 0)
            return new List<ClassStudentProgressDto>();

        var casesViewedMap = await _unitOfWork.Context.CaseViewLogs
            .AsNoTracking()
            .Where(v => studentIds.Contains(v.StudentId))
            .GroupBy(v => v.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Count = g.Count(),
                LastViewedAt = g.Max(x => x.ViewedAt)
            })
            .ToDictionaryAsync(x => x.StudentId, x => new { x.Count, x.LastViewedAt });

        var questionsAskedMap = await _unitOfWork.Context.QaMessages
            .AsNoTracking()
            .Where(m => m.Role == "User" && studentIds.Contains(m.Session.StudentId))
            .GroupBy(m => m.Session.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count);

        var quizAggMap = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Where(a => studentIds.Contains(a.StudentId))
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Attempts = g.Count(),
                AvgScore = g.Where(x => x.Score.HasValue).Average(x => (double?)x.Score)
            })
            .ToDictionaryAsync(x => x.StudentId, x => new { x.Attempts, x.AvgScore });

        var escalatedMap = await _unitOfWork.Context.CaseAnswers
            .AsNoTracking()
            .Where(a =>
                (a.Status == CaseAnswerStatuses.EscalatedToExpert || a.Status == CaseAnswerStatuses.Escalated)
                && a.Question != null
                && studentIds.Contains(a.Question.StudentId))
            .GroupBy(a => a.Question.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count);

        return enrollments
            .Select(enrollment =>
            {
                var studentId = enrollment.StudentId;
                var casesAgg = casesViewedMap.GetValueOrDefault(studentId);
                var quizAgg = quizAggMap.GetValueOrDefault(studentId);

                return new ClassStudentProgressDto
                {
                    StudentId = studentId,
                    StudentName = enrollment.Student?.FullName ?? string.Empty,
                    StudentEmail = enrollment.Student?.Email,
                    StudentCode = enrollment.Student?.SchoolCohort,
                    TotalCasesViewed = casesAgg?.Count ?? 0,
                    TotalQuestionsAsked = questionsAskedMap.GetValueOrDefault(studentId),
                    AvgQuizScore = quizAgg?.AvgScore,
                    QuizAttempts = quizAgg?.Attempts ?? 0,
                    EscalatedAnswers = escalatedMap.GetValueOrDefault(studentId),
                    LastActivityAt = casesAgg?.LastViewedAt
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<LectStudentQuestionDto>> GetStudentQuestionsAsync(Guid classId, Guid? caseId, Guid? studentId)
    {
        var studentIdsInClass = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var query = _unitOfWork.StudentQuestionRepository
            .FindByCondition(q => studentIdsInClass.Contains(q.StudentId))
            .Include(q => q.Student)
            .Include(q => q.Case)
                .ThenInclude(c => c!.MedicalImages)
            .Include(q => q.CaseAnswers)
            .AsQueryable();

        if (caseId.HasValue)
        {
            query = query.Where(q => q.CaseId == caseId.Value);
        }

        if (studentId.HasValue)
        {
            query = query.Where(q => q.StudentId == studentId.Value);
        }

        var questions = await query.OrderByDescending(q => q.CreatedAt).ToListAsync();

        return questions
            .Select(q =>
            {
                var latestAnswer = q.CaseAnswers
                    .OrderByDescending(a => a.GeneratedAt ?? DateTime.MinValue)
                    .ThenByDescending(a => a.Id)
                    .FirstOrDefault();

                return new LectStudentQuestionDto
                {
                    Id = q.Id,
                    AnswerId = latestAnswer?.Id,
                    StudentId = q.StudentId,
                    StudentName = q.Student?.FullName ?? string.Empty,
                    StudentEmail = q.Student?.Email ?? string.Empty,
                    CaseId = q.CaseId ?? Guid.Empty,
                    CaseTitle = q.Case?.Title ?? string.Empty,
                    QuestionText = q.QuestionText,
                    Language = q.Language,
                    CreatedAt = q.CreatedAt,
                    AnswerText = latestAnswer?.AnswerText,
                    AnswerStatus = latestAnswer?.Status,
                    EscalatedById = latestAnswer?.EscalatedById,
                    EscalatedAt = latestAnswer?.EscalatedAt,
                    AiConfidenceScore = latestAnswer?.AiConfidenceScore
                };
            })
            .ToList();
    }

    public async Task<List<ClassAssignmentDto>> GetClassAssignmentsAsync(Guid classId)
    {
        var academicClass = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Class not found.");

        var className = academicClass.ClassName ?? "";

        // Sequential: the same DbContext is not safe when running multiple parallel queries (Task.WhenAll).
        var totalStudents = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .CountAsync(e => e.ClassId == classId);

        var caseAssignments = await _unitOfWork.Context.ClassCases
            .AsNoTracking()
            .Where(cc => cc.ClassId == classId)
            .Select(cc => new {
                cc.CaseId,
                cc.DueDate,
                cc.IsMandatory,
                cc.AssignedAt,
                CaseTitle = cc.Case.Title
            })
            .OrderByDescending(cc => cc.AssignedAt)
            .ToListAsync();

        var quizSessions = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Where(cqs => cqs.ClassId == classId)
            .Select(cqs => new {
                cqs.Id,
                cqs.QuizId,
                cqs.CloseTime,
                cqs.CreatedAt,
                QuizTitle = cqs.Quiz.Title
            })
            .OrderByDescending(cqs => cqs.CreatedAt)
            .ToListAsync();

        var quizQuizIds = quizSessions.Select(qs => qs.QuizId).Distinct().ToList();

        var submittedCounts = quizQuizIds.Count > 0
            ? await _unitOfWork.Context.QuizAttempts
                .Where(a => quizQuizIds.Contains(a.QuizId))
                .GroupBy(a => a.QuizId)
                .Select(g => new { QuizId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuizId, x => x.Count)
            : new Dictionary<Guid, int>();

        var gradedCounts = quizQuizIds.Count > 0
            ? await _unitOfWork.Context.QuizAttempts
                .Where(a => quizQuizIds.Contains(a.QuizId) && a.Score != null)
                .GroupBy(a => a.QuizId)
                .Select(g => new { QuizId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuizId, x => x.Count)
            : new Dictionary<Guid, int>();

        var results = new List<ClassAssignmentDto>();

        foreach (var cc in caseAssignments)
        {
            results.Add(new ClassAssignmentDto
            {
                Id = cc.CaseId,
                ClassId = classId,
                ClassName = className,
                Type = "case",
                Title = string.IsNullOrWhiteSpace(cc.CaseTitle) ? "Unknown Case" : cc.CaseTitle,
                DueDate = cc.DueDate,
                IsMandatory = cc.IsMandatory,
                AssignedAt = cc.AssignedAt,
                TotalStudents = totalStudents,
                SubmittedCount = 0,
                GradedCount = 0,
            });
        }

        foreach (var qs in quizSessions)
        {
            results.Add(new ClassAssignmentDto
            {
                Id = qs.Id,
                ClassId = classId,
                ClassName = className,
                Type = "quiz",
                Title = string.IsNullOrWhiteSpace(qs.QuizTitle) ? "Unknown Quiz" : qs.QuizTitle,
                DueDate = qs.CloseTime,
                IsMandatory = false,
                AssignedAt = qs.CreatedAt,
                TotalStudents = totalStudents,
                SubmittedCount = submittedCounts.GetValueOrDefault(qs.QuizId, 0),
                GradedCount = gradedCounts.GetValueOrDefault(qs.QuizId, 0),
            });
        }

        return results.OrderByDescending(a => a.AssignedAt).ToList();
    }

    public async Task<List<ClassAssignmentDto>> GetAllAssignmentsForLecturerAsync(Guid lecturerId)
    {
        var classList = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.LecturerId == lecturerId)
            .ToListAsync();

        if (classList.Count == 0)
            return new List<ClassAssignmentDto>();

        var classIds = classList.Select(c => c.Id).ToList();
        var classNameMap = classList.ToDictionary(c => c.Id, c => c.ClassName ?? "");

        // Sequential: do not start ToDictionaryAsync then await another query - EF DbContext does not allow multiple simultaneous operations.
        var enrollmentCounts = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId))
            .GroupBy(e => e.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);

        var allCaseAssignments = await _unitOfWork.Context.ClassCases
            .AsNoTracking()
            .Where(cc => classIds.Contains(cc.ClassId))
            .Include(cc => cc.Case)
            .ToListAsync();

        var allQuizSessions = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Where(cqs => classIds.Contains(cqs.ClassId))
            .Include(cqs => cqs.Quiz)
            .ToListAsync();

        // Extract quiz IDs after sessions are loaded
        var quizQuizIds = allQuizSessions.Select(q => q.QuizId).ToList();

        // Batch count queries (sequential but fast — no nested nav properties)
        var submittedCounts = quizQuizIds.Count > 0
            ? await _unitOfWork.Context.QuizAttempts
                .Where(a => quizQuizIds.Contains(a.QuizId))
                .GroupBy(a => a.QuizId)
                .Select(g => new { QuizId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuizId, x => x.Count)
            : new Dictionary<Guid, int>();

        var gradedCounts = quizQuizIds.Count > 0
            ? await _unitOfWork.Context.QuizAttempts
                .Where(a => quizQuizIds.Contains(a.QuizId) && a.Score != null)
                .GroupBy(a => a.QuizId)
                .Select(g => new { QuizId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuizId, x => x.Count)
            : new Dictionary<Guid, int>();

        // Build results — pure in-memory, zero DB queries
        var results = new List<ClassAssignmentDto>();

        foreach (var cc in allCaseAssignments)
        {
            results.Add(new ClassAssignmentDto
            {
                Id = cc.CaseId,
                ClassId = cc.ClassId,
                ClassName = classNameMap.GetValueOrDefault(cc.ClassId, ""),
                Type = "case",
                Title = cc.Case?.Title ?? "Unknown Case",
                DueDate = cc.DueDate,
                IsMandatory = cc.IsMandatory,
                AssignedAt = cc.AssignedAt,
                TotalStudents = enrollmentCounts.GetValueOrDefault(cc.ClassId, 0),
                SubmittedCount = 0,
                GradedCount = 0,
            });
        }

        foreach (var qs in allQuizSessions)
        {
            results.Add(new ClassAssignmentDto
            {
                Id = qs.Id,
                ClassId = qs.ClassId,
                ClassName = classNameMap.GetValueOrDefault(qs.ClassId, ""),
                Type = "quiz",
                Title = qs.Quiz?.Title ?? "Unknown Quiz",
                DueDate = qs.CloseTime,
                IsMandatory = false,
                AssignedAt = qs.CreatedAt,
                TotalStudents = enrollmentCounts.GetValueOrDefault(qs.ClassId, 0),
                SubmittedCount = submittedCounts.GetValueOrDefault(qs.QuizId, 0),
                GradedCount = gradedCounts.GetValueOrDefault(qs.QuizId, 0),
            });
        }

        return results.OrderByDescending(a => a.AssignedAt).ToList();
    }

    public async Task<IReadOnlyList<AnnouncementDto>> GetClassAnnouncementsAsync(Guid classId)
    {
        var announcements = await _unitOfWork.AnnouncementRepository
            .FindByCondition(a => a.ClassId == classId)
            .Include(a => a.Class)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return announcements
            .Select(a => new AnnouncementDto
            {
                Id = a.Id,
                ClassId = a.ClassId,
                ClassName = a.Class?.ClassName,
                Title = a.Title,
                Content = a.Content,
                SendEmail = a.SendEmail,
                CreatedAt = a.CreatedAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ClassQuizDto>> GetQuizzesByLecturerAsync(Guid lecturerId)
    {
        // Get all classes owned by this lecturer
        var classIds = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.LecturerId == lecturerId)
            .Select(c => c.Id)
            .ToListAsync();

        // Get all quizzes assigned to these classes (existing behavior)
        var classQuizzes = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => classIds.Contains(cqs.ClassId))
            .Include(cqs => cqs.Quiz)
            .Include(cqs => cqs.Class)
            .OrderByDescending(cqs => cqs.CreatedAt)
            .ToListAsync();

        // Get quiz IDs that are already assigned to lecturer's classes
        var assignedQuizIds = classQuizzes.Select(cq => cq.QuizId).ToHashSet();

        // Also get unassigned quizzes — both AI-generated AND manually created
        // (quiz may be in DB but not yet assigned to any class)
        var unassignedQuizIds = await _unitOfWork.Context.ClassQuizSessions
            .Select(cqs => cqs.QuizId)
            .ToListAsync();

        // Only unassigned quizzes belonging to the lecturer (or legacy null). Student AI quizzes have CreatedByExpertId = studentId — do not display here.
        var unassignedQuizzes = await _unitOfWork.Context.Quizzes
            .Where(q => !unassignedQuizIds.Contains(q.Id)
                && (!q.CreatedByExpertId.HasValue || q.CreatedByExpertId == lecturerId))
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        // Combine quiz IDs
        var allQuizIds = classQuizzes.Select(cq => cq.QuizId)
            .Concat(unassignedQuizzes.Select(q => q.Id))
            .Distinct()
            .ToList();

        // Get question counts for all quizzes
        var questionCounts = await _unitOfWork.Context.QuizQuestions
            .AsNoTracking()
            .Where(qq => allQuizIds.Contains(qq.QuizId))
            .GroupBy(qq => qq.QuizId)
            .Select(g => new { QuizId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.QuizId, x => x.Count);

        // Build result list: assigned quizzes + unassigned quizzes (only from lecturer or legacy CreatedByExpertId null)
        var results = classQuizzes
            .Select(cq => new ClassQuizDto
            {
                ClassId = cq.ClassId,
                QuizId = cq.QuizId,
                QuizName = cq.Quiz?.Title,
                ClassName = cq.Class?.ClassName,
                Topic = cq.Quiz?.Topic,
                AssignedAt = cq.CreatedAt,
                OpenTime = cq.Quiz?.OpenTime,
                CloseTime = cq.Quiz?.CloseTime,
                QuestionCount = questionCounts.GetValueOrDefault(cq.QuizId),
                IsFromExpertLibrary = cq.Quiz != null && cq.Quiz.CreatedByExpertId.HasValue
            })
            .ToList();

        // Unassigned quizzes (filtered to exclude AI quizzes created by students — CreatedByExpertId = studentId)
        foreach (var quiz in unassignedQuizzes)
        {
            results.Add(new ClassQuizDto
            {
                ClassId = Guid.Empty,
                QuizId = quiz.Id,
                QuizName = quiz.Title,
                ClassName = null,
                Topic = quiz.Topic,
                AssignedAt = quiz.CreatedAt,
                OpenTime = quiz.OpenTime,
                CloseTime = quiz.CloseTime,
                QuestionCount = questionCounts.GetValueOrDefault(quiz.Id),
                IsFromExpertLibrary = quiz.CreatedByExpertId.HasValue
            });
        }

        return results.OrderByDescending(r => r.AssignedAt).ToList();
    }

    /// <summary>
    /// Get unassigned quizzes created by the lecturer (CreatedByExpertId = null, CreatedByLecturerId = lecturerId)
    /// These are quizzes in lecturer's personal pool that have NOT been assigned to any class yet.
    /// </summary>
    public async Task<IReadOnlyList<QuizDto>> GetUnassignedLecturerQuizzesAsync(Guid lecturerId)
    {
        // Get all quiz IDs that are already assigned to ANY class (not just lecturer's classes)
        var assignedQuizIds = await _unitOfWork.Context.ClassQuizSessions
            .Select(cqs => cqs.QuizId)
            .ToListAsync();

        // Get unassigned quizzes created by this lecturer
        var quizzes = await _unitOfWork.Context.Quizzes
            .Where(q =>
                // Quiz created by this lecturer (not from Expert Library)
                q.CreatedByExpertId == null &&
                // Not assigned to any class yet
                !assignedQuizIds.Contains(q.Id)
            )
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        // Get question counts
        var quizIds = quizzes.Select(q => q.Id).ToList();
        var questionCounts = quizIds.Count > 0
            ? await _unitOfWork.Context.QuizQuestions
                .AsNoTracking()
                .Where(qq => quizIds.Contains(qq.QuizId))
                .GroupBy(qq => qq.QuizId)
                .Select(g => new { QuizId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuizId, x => x.Count)
            : new Dictionary<Guid, int>();

        return quizzes
            .Select(q => new QuizDto
            {
                Id = q.Id,
                ClassId = Guid.Empty,
                Title = q.Title,
                Topic = q.Topic,
                IsAiGenerated = q.IsAiGenerated,
                IsVerifiedCurriculum = q.IsVerifiedCurriculum,
                Difficulty = q.Difficulty,
                Classification = q.Classification,
                OpenTime = q.OpenTime,
                CloseTime = q.CloseTime,
                TimeLimit = q.TimeLimit,
                PassingScore = q.PassingScore,
                CreatedAt = q.CreatedAt,
                IsFromExpertLibrary = false
            })
            .ToList();
    }

    /// <summary>
    /// Get all quizzes available to lecturer (both own and expert library quizzes),
    /// excluding AI-generated quizzes. Does not include assigned quizzes.
    /// Used for "My Quizzes" tab with full library view.
    /// </summary>
    public async Task<IReadOnlyList<QuizDto>> GetAllLecturerQuizzesAsync(Guid lecturerId)
    {
        // Get all quiz IDs that are already assigned to ANY class
        var assignedQuizIds = await _unitOfWork.Context.ClassQuizSessions
            .Select(cqs => cqs.QuizId)
            .ToListAsync();

        // Get quizzes that are either:
        // 1. Created by this lecturer (not from Expert Library, not AI-generated)
        // 2. From Expert Library (CreatedByExpertId != null), not AI-generated
        var quizzes = await _unitOfWork.Context.Quizzes
            .Where(q =>
                // Not AI-generated
                !q.IsAiGenerated &&
                // Not assigned to any class yet
                !assignedQuizIds.Contains(q.Id)
            )
            .OrderBy(q => q.Title)
            .ThenByDescending(q => q.CreatedAt)
            .ToListAsync();

        // Get question counts
        var quizIds = quizzes.Select(q => q.Id).ToList();
        var questionCounts = quizIds.Count > 0
            ? await _unitOfWork.Context.QuizQuestions
                .AsNoTracking()
                .Where(qq => quizIds.Contains(qq.QuizId))
                .GroupBy(qq => qq.QuizId)
                .Select(g => new { QuizId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuizId, x => x.Count)
            : new Dictionary<Guid, int>();

        return quizzes
            .Select(q => new QuizDto
            {
                Id = q.Id,
                ClassId = Guid.Empty,
                Title = q.Title,
                Topic = q.Topic,
                IsAiGenerated = q.IsAiGenerated,
                IsVerifiedCurriculum = q.IsVerifiedCurriculum,
                Difficulty = q.Difficulty,
                Classification = q.Classification,
                OpenTime = q.OpenTime,
                CloseTime = q.CloseTime,
                TimeLimit = q.TimeLimit,
                PassingScore = q.PassingScore,
                CreatedAt = q.CreatedAt,
                IsFromExpertLibrary = q.CreatedByExpertId != null
            })
            .ToList();
    }

    /// <summary>
    /// Get all quizzes assigned to lecturer's classes (ClassQuizSession).
    /// Includes both lecturer-created quizzes and expert library quizzes that have been assigned.
    /// </summary>
    public async Task<IReadOnlyList<AssignedQuizDto>> GetAssignedQuizzesAsync(Guid lecturerId)
    {
        // Get all classes owned by this lecturer
        var classIds = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.LecturerId == lecturerId)
            .Select(c => c.Id)
            .ToListAsync();

        // Get all ClassQuizSessions for these classes
        var sessions = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => classIds.Contains(cqs.ClassId))
            .Include(cqs => cqs.Quiz)
                .ThenInclude(q => q.CreatedByExpert)
            .Include(cqs => cqs.Class)
            .OrderByDescending(cqs => cqs.CreatedAt)
            .ToListAsync();

        // Get question counts for all quizzes
        var quizIds = sessions.Select(s => s.QuizId).Distinct().ToList();
        var questionCounts = quizIds.Count > 0
            ? await _unitOfWork.Context.QuizQuestions
                .AsNoTracking()
                .Where(qq => quizIds.Contains(qq.QuizId))
                .GroupBy(qq => qq.QuizId)
                .Select(g => new { QuizId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuizId, x => x.Count)
            : new Dictionary<Guid, int>();

        return sessions
            .Select(s =>
            {
                var quiz = s.Quiz;
                string? creatorName = null;
                string? creatorType = null;

                if (quiz != null)
                {
                    if (quiz.CreatedByExpertId.HasValue)
                    {
                        creatorType = "Expert";
                        creatorName = quiz.CreatedByExpert?.FullName;
                    }
                    else
                    {
                        creatorType = "Lecturer";
                        creatorName = "Lecturer";
                    }
                }

                return new AssignedQuizDto
                {
                    AssignmentId = s.Id,
                    ClassId = s.ClassId,
                    QuizId = s.QuizId,
                    QuizName = quiz?.Title,
                    ClassName = s.Class?.ClassName,
                    Topic = quiz?.Topic,
                    AssignedAt = s.CreatedAt,
                    OpenTime = s.OpenTime ?? quiz?.OpenTime,
                    CloseTime = s.CloseTime ?? quiz?.CloseTime,
                    QuestionCount = questionCounts.GetValueOrDefault(s.QuizId),
                    IsFromExpertLibrary = quiz != null && quiz.CreatedByExpertId.HasValue,
                    CreatorName = creatorName,
                    CreatorType = creatorType
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<QuizDto>> GetQuizzesForClassAsync(Guid classId)
    {
        var sessions = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Quiz)
            .Where(cqs => cqs.ClassId == classId)
            .ToListAsync();

        return sessions
            .Select(cqs => new QuizDto
            {
                Id = cqs.QuizId,
                ClassId = classId,
                Title = cqs.Quiz?.Title ?? string.Empty,
                OpenTime = cqs.OpenTime,
                CloseTime = cqs.CloseTime,
                TimeLimit = cqs.TimeLimitMinutes,
                PassingScore = (int?)cqs.PassingScore,
                CreatedAt = cqs.CreatedAt,
                IsFromExpertLibrary = cqs.Quiz != null && cqs.Quiz.CreatedByExpertId.HasValue
            })
            .ToList();
    }

    public async Task<QuizDto?> GetQuizByIdAsync(Guid quizId)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId);
        if (quiz == null)
            return null;

        var classLink = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Where(cqs => cqs.QuizId == quizId)
            .OrderByDescending(cqs => cqs.CreatedAt)
            .FirstOrDefaultAsync();

        // IsFromExpertLibrary: true nếu quiz được tạo bởi Expert (CreatedByExpertId != null)
        // và không phải do lecturer hiện tại tạo (tức là CreatedByExpertId != lecturerId, nhưng ở đây chỉ cần biết có expertId là đủ)
        var isFromExpertLibrary = quiz.CreatedByExpertId.HasValue;

        return new QuizDto
        {
            Id = quiz.Id,
            ClassId = classLink?.ClassId ?? Guid.Empty,
            Title = quiz.Title,
            Topic = quiz.Topic,
            IsAiGenerated = quiz.IsAiGenerated,
            Difficulty = quiz.Difficulty,
            Classification = quiz.Classification,
            OpenTime = quiz.OpenTime,
            CloseTime = quiz.CloseTime,
            TimeLimit = quiz.TimeLimit,
            PassingScore = NormalizePassingScore(quiz.PassingScore, quiz.IsAiGenerated),
            CreatedAt = quiz.CreatedAt,
            IsFromExpertLibrary = isFromExpertLibrary
        };
    }

    public async Task<QuizWithQuestionsDto> GetQuizWithQuestionsAsync(Guid quizId)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId)
            ?? throw new KeyNotFoundException("Quiz not found.");

        var questions = await _unitOfWork.QuizQuestionRepository
            .FindByCondition(q => q.QuizId == quizId)
            .Include(q => q.Case)
            .OrderBy(q => q.Id) // keep order
            .ToListAsync();

        var isFromExpertLibrary = quiz.CreatedByExpertId.HasValue;

        // Get the latest class assignment (if any)
        var latestClassSession = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Where(cqs => cqs.QuizId == quizId)
            .OrderByDescending(cqs => cqs.CreatedAt)
            .FirstOrDefaultAsync();

        var classId = latestClassSession?.ClassId ?? Guid.Empty;

        return new QuizWithQuestionsDto
        {
            Quiz = new QuizDto
            {
                Id = quiz.Id,
                ClassId = classId,
                Title = quiz.Title,
                Topic = quiz.Topic,
                IsAiGenerated = quiz.IsAiGenerated,
                Difficulty = quiz.Difficulty,
                Classification = quiz.Classification,
                OpenTime = quiz.OpenTime,
                CloseTime = quiz.CloseTime,
                TimeLimit = quiz.TimeLimit,
                PassingScore = NormalizePassingScore(quiz.PassingScore, quiz.IsAiGenerated),
                CreatedAt = quiz.CreatedAt,
                IsFromExpertLibrary = isFromExpertLibrary
            },
            Questions = questions.Select(q => new QuizQuestionDto
            {
                Id = q.Id,
                QuizId = q.QuizId,
                CaseId = q.CaseId,
                CaseTitle = q.Case?.Title,
                QuestionText = q.QuestionText,
                Type = q.Type?.ToString(),
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD,
                CorrectAnswer = q.CorrectAnswer,
                ImageUrl = q.ImageUrl
            }).ToList()
        };
    }

    public async Task<QuizDto> UpdateQuizAsync(Guid quizId, UpdateQuizRequestDto request)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId);
        if (quiz == null)
            throw new KeyNotFoundException("Quiz does not exist.");

        quiz.Title = request.Title;
        quiz.OpenTime = ToUtc(request.OpenTime);
        quiz.CloseTime = ToUtc(request.CloseTime);
        quiz.TimeLimit = request.TimeLimit;
        quiz.PassingScore = request.PassingScore;

        _unitOfWork.QuizRepository.Update(quiz);
        await _unitOfWork.SaveAsync();

        // Update all ClassQuizSessions associated with this quiz to reflect new settings
        var classSessions = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => cqs.QuizId == quizId)
            .ToListAsync();

        if (classSessions.Any())
        {
            foreach (var session in classSessions)
            {
                session.OpenTime = ToUtc(request.OpenTime);
                session.CloseTime = ToUtc(request.CloseTime);
                session.TimeLimitMinutes = request.TimeLimit;
                session.PassingScore = NormalizePassingScore(request.PassingScore, quiz.IsAiGenerated);
            }
            _unitOfWork.Context.ClassQuizSessions.UpdateRange(classSessions);
            await _unitOfWork.SaveAsync();
        }

        var classLink = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Where(cqs => cqs.QuizId == quizId)
            .OrderByDescending(cqs => cqs.CreatedAt)
            .FirstOrDefaultAsync();

        return new QuizDto
        {
            Id = quiz.Id,
            ClassId = classLink?.ClassId ?? Guid.Empty,
            Title = quiz.Title,
            Topic = quiz.Topic,
            IsAiGenerated = quiz.IsAiGenerated,
            Difficulty = quiz.Difficulty,
            Classification = quiz.Classification,
            OpenTime = quiz.OpenTime,
            CloseTime = quiz.CloseTime,
            TimeLimit = quiz.TimeLimit,
            PassingScore = NormalizePassingScore(quiz.PassingScore, quiz.IsAiGenerated),
            CreatedAt = quiz.CreatedAt
        };
    }

    public async Task<IReadOnlyList<QuizDto>> GetQuizzesByIdsAsync(IReadOnlyList<Guid> quizIds)
    {
        if (quizIds == null || quizIds.Count == 0)
            return new List<QuizDto>();

        var distinct = quizIds.Distinct().ToList();
        var quizzes = await _unitOfWork.QuizRepository
            .FindByCondition(q => distinct.Contains(q.Id))
            .ToListAsync();

        var sessions = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Where(cqs => distinct.Contains(cqs.QuizId))
            .OrderByDescending(cqs => cqs.CreatedAt)
            .ToListAsync();

        var latestClassByQuizId = sessions
            .GroupBy(s => s.QuizId)
            .ToDictionary(g => g.Key, g => g.First());

        return quizzes
            .Select(q =>
            {
                latestClassByQuizId.TryGetValue(q.Id, out var link);
                return new QuizDto
                {
                    Id = q.Id,
                    ClassId = link?.ClassId ?? Guid.Empty,
                    Title = q.Title,
                    Topic = q.Topic,
                    IsAiGenerated = q.IsAiGenerated,
                    Difficulty = q.Difficulty,
                    Classification = q.Classification,
                    OpenTime = q.OpenTime,
                    CloseTime = q.CloseTime,
                    TimeLimit = q.TimeLimit,
                    PassingScore = q.PassingScore,
                    CreatedAt = q.CreatedAt
                };
            })
            .ToList();
    }

    public async Task<ClassQuizDto> AssignQuizToClassAsync(Guid classId, Guid quizId)
    {
        var academicClass = await _unitOfWork.AcademicClassRepository
            .GetByIdAsync(classId)
            ?? throw new KeyNotFoundException("Class not found.");

        var quiz = await _unitOfWork.QuizRepository
            .GetByIdAsync(quizId)
            ?? throw new KeyNotFoundException("Quiz not found.");

        var existing = await _unitOfWork.ClassQuizSessionRepository
            .FirstOrDefaultAsync(cq => cq.ClassId == classId && cq.QuizId == quizId);

            // Idempotent: Save from FE may be called again when quiz is already assigned (avoid 409 Conflict).
            if (existing != null)
        {
            var existingQuestionCount = await _unitOfWork.Context.QuizQuestions
                .AsNoTracking()
                .CountAsync(qq => qq.QuizId == quizId);

            return new ClassQuizDto
            {
                ClassId = existing.ClassId,
                ClassName = academicClass.ClassName,
                QuizId = existing.QuizId,
                QuizName = quiz.Title,
                Topic = quiz.Topic,
                AssignedAt = existing.CreatedAt,
                OpenTime = existing.OpenTime ?? quiz.OpenTime,
                CloseTime = existing.CloseTime ?? quiz.CloseTime,
                QuestionCount = existingQuestionCount
            };
        }

        var classQuiz = new ClassQuizSession
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            QuizId = quizId,
            OpenTime = quiz.OpenTime,
            CloseTime = quiz.CloseTime,
            TimeLimitMinutes = quiz.TimeLimit,
            PassingScore = NormalizePassingScore(quiz.PassingScore, quiz.IsAiGenerated),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ClassQuizSessionRepository.AddAsync(classQuiz);
        await _unitOfWork.SaveAsync();

        var questionCount = await _unitOfWork.Context.QuizQuestions
            .AsNoTracking()
            .CountAsync(qq => qq.QuizId == quizId);

        return new ClassQuizDto
        {
            ClassId = classQuiz.ClassId,
            ClassName = academicClass.ClassName,
            QuizId = classQuiz.QuizId,
            QuizName = quiz.Title,
            Topic = quiz.Topic,
            AssignedAt = classQuiz.CreatedAt,
            OpenTime = quiz.OpenTime,
            CloseTime = quiz.CloseTime,
            QuestionCount = questionCount
        };
    }

    public async Task<ImportStudentsSummaryDto> ImportStudentsFromExcelAsync(Guid classId, Stream fileStream, string fileName)
    {
        var existingClass = await _unitOfWork.AcademicClassRepository.GetByIdAsync(classId);
        if (existingClass == null)
        {
            return new ImportStudentsSummaryDto
            {
                FailedCount = 1,
                Results = new List<ImportStudentsResultItemDto>
                {
                    new()
                    {
                        RowNumber = 0,
                        Success = false,
                        ErrorMessage = "Class does not exist.",
                    },
                },
            };
        }

        var result = new ImportStudentsSummaryDto();

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var workbook = new ClosedXML.Excel.XLWorkbook(memoryStream);
        var worksheet = workbook.Worksheet(1);

        var rows = worksheet.RowsUsed().Skip(1).ToList();
        result.TotalRows = rows.Count;

        if (result.TotalRows == 0)
        {
            result.FailedCount = 1;
            result.Results.Add(new ImportStudentsResultItemDto
            {
                RowNumber = 0,
                Success = false,
                        ErrorMessage = "Excel file has no data (header only).",
            });
            return result;
        }

        var emails = rows
            .Select(r => r.Cell(1).GetString()?.Trim().ToLowerInvariant())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .ToList();

        var foundUsers = await _unitOfWork.UserRepository
            .FindByCondition(u => emails!.Contains(u.Email.ToLower()))
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .ToListAsync();

        var studentRoleName = "Student";
        var studentUsers = foundUsers
            .Where(u => u.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == studentRoleName))
            .ToDictionary(u => u.Email.ToLowerInvariant(), u => u);

        var enrolledStudentIds = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(ce => ce.ClassId == classId)
            .Select(ce => ce.StudentId)
            .ToListAsync();

        var enrolledSet = new HashSet<Guid>(enrolledStudentIds);

        foreach (var row in rows)
        {
            var rowNumber = row.RowNumber();
            var email = row.Cell(1).GetString()?.Trim();
            var item = new ImportStudentsResultItemDto { RowNumber = rowNumber, Email = email };

            if (string.IsNullOrWhiteSpace(email))
            {
                item.Success = false;
                item.ErrorMessage = "Email is empty.";
                result.Results.Add(item);
                result.FailedCount++;
                continue;
            }

            var emailLower = email.ToLowerInvariant();

            if (!studentUsers.TryGetValue(emailLower, out var user))
            {
                item.Success = false;
                item.ErrorMessage = $"Student not found with email '{email}'.";
                result.Results.Add(item);
                result.NotFoundCount++;
                result.FailedCount++;
                continue;
            }

            if (enrolledSet.Contains(user.Id))
            {
                item.Success = false;
                item.ErrorMessage = $"Student '{user.FullName}' is already in the class.";
                result.Results.Add(item);
                result.AlreadyEnrolledCount++;
                result.FailedCount++;
                continue;
            }

            var enrollment = new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                StudentId = user.Id,
                EnrolledAt = DateTime.UtcNow
            };

            _unitOfWork.ClassEnrollmentRepository.Add(enrollment);
            enrolledSet.Add(user.Id);

            item.Success = true;
            item.Student = new StudentEnrollmentDto
            {
                EnrollmentId = enrollment.Id,
                StudentId = user.Id,
                StudentName = user.FullName,
                StudentEmail = user.Email,
                StudentCode = user.SchoolCohort,
                EnrolledAt = enrollment.EnrolledAt
            };
            result.Results.Add(item);
            result.SuccessCount++;
        }

        await _unitOfWork.SaveAsync();
        return result;
    }

    public async Task<IReadOnlyList<ExpertOptionDto>> GetExpertsAsync()
    {
        var experts = await _unitOfWork.UserRepository
            .FindByCondition(u => u.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == "Expert"))
            .Select(u => new ExpertOptionDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email
            })
            .ToListAsync();

        return experts;
    }

    public async Task<ClassDto> AssignExpertToClassAsync(Guid lecturerId, Guid classId, Guid? expertId)
    {
        var academicClass = await _unitOfWork.AcademicClassRepository
            .FindByCondition(c => c.Id == classId && c.LecturerId == lecturerId)
            .Include(c => c.Expert)
            .FirstOrDefaultAsync();

        if (academicClass == null)
            throw new UnauthorizedAccessException("You do not have permission to edit this class.");

        academicClass.ExpertId = expertId;
        academicClass.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveAsync();

        return new ClassDto
        {
            Id = academicClass.Id,
            ClassName = academicClass.ClassName,
            Semester = academicClass.Semester,
            LecturerId = academicClass.LecturerId,
            ExpertId = academicClass.ExpertId,
            ExpertName = academicClass.Expert?.FullName,
            CreatedAt = academicClass.CreatedAt
        };
    }

    // ── Assignment CRUD Methods ─────────────────────────────────────────────────

    /// <summary>Get details of an assignment by ID.</summary>
    public async Task<AssignmentDetailDto> GetAssignmentByIdAsync(Guid assignmentId)
    {
        // Try to find in ClassCases (case assignment)
        var classCase = await _unitOfWork.Context.ClassCases
            .AsNoTracking()
            .Include(cc => cc.Class)
            .Include(cc => cc.Case)
            .FirstOrDefaultAsync(cc => cc.CaseId == assignmentId);

        if (classCase != null)
        {
            var totalStudents = await _unitOfWork.Context.ClassEnrollments
                .CountAsync(e => e.ClassId == classCase.ClassId);

            var submittedCount = await _unitOfWork.Context.CaseViewLogs
                .CountAsync(v => v.CaseId == classCase.CaseId && v.IsCompleted == true);

            return new AssignmentDetailDto
            {
                Id = classCase.CaseId,
                ClassId = classCase.ClassId,
                ClassName = classCase.Class?.ClassName ?? "",
                ClassCode = null,
                Type = "case",
                Title = classCase.Case?.Title ?? "Untitled Case",
                Description = classCase.Case?.Description,
                DueDate = classCase.DueDate,
                OpenDate = classCase.AssignedAt,
                IsMandatory = classCase.IsMandatory,
                AssignedAt = classCase.AssignedAt,
                TotalStudents = totalStudents,
                SubmittedCount = submittedCount,
                GradedCount = 0,
                AllowLate = false,
                CreatedAt = classCase.AssignedAt
            };
        }

        // Try to find in ClassQuizSessions (quiz assignment)
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Class)
            .Include(cqs => cqs.Quiz)
            .FirstOrDefaultAsync(cqs => cqs.Id == assignmentId);

        if (quizSession == null)
            throw new KeyNotFoundException("Assignment not found.");

        var quizTotalStudents = await _unitOfWork.Context.ClassEnrollments
            .CountAsync(e => e.ClassId == quizSession.ClassId);

        var quizSubmittedCount = await _unitOfWork.Context.QuizAttempts
            .CountAsync(a => a.QuizId == quizSession.QuizId && a.CompletedAt.HasValue);

        var quizGradedCount = await _unitOfWork.Context.QuizAttempts
            .CountAsync(a => a.QuizId == quizSession.QuizId && a.Score.HasValue);

        var quizAvgScore = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.QuizId == quizSession.QuizId && a.Score.HasValue)
            .AverageAsync(a => (double?)a.Score) ?? null;

        return new AssignmentDetailDto
        {
            Id = quizSession.Id,
            ClassId = quizSession.ClassId,
            ClassName = quizSession.Class?.ClassName ?? "",
            ClassCode = null,
            Type = "quiz",
            Title = quizSession.Quiz?.Title ?? "Untitled Quiz",
            Description = null,
            Instructions = null,
            DueDate = quizSession.CloseTime,
            OpenDate = quizSession.OpenTime,
            IsMandatory = false,
            AssignedAt = quizSession.CreatedAt,
            TotalStudents = quizTotalStudents,
            SubmittedCount = quizSubmittedCount,
            GradedCount = quizGradedCount,
            MaxScore = 100,
            PassingScore = quizSession.PassingScore,
            TimeLimitMinutes = quizSession.TimeLimitMinutes,
            AllowLate = quizSession.AllowLate,
            AllowRetake = quizSession.AllowRetake,
            ShowResultsAfterSubmission = quizSession.ShowResultsAfterSubmission,
            AvgScore = quizAvgScore,
            CreatedAt = quizSession.CreatedAt
        };
    }

    /// <summary>Update assignment information.</summary>
    public async Task<AssignmentDetailDto> UpdateAssignmentAsync(Guid assignmentId, UpdateAssignmentRequestDto request)
    {
        // Try to update ClassCase
        var classCase = await _unitOfWork.Context.ClassCases
            .Include(cc => cc.Class)
            .Include(cc => cc.Case)
            .FirstOrDefaultAsync(cc => cc.CaseId == assignmentId);

        if (classCase != null)
        {
            if (request.DueDate.HasValue)
                classCase.DueDate = ToUtc(request.DueDate);
            if (request.IsMandatory.HasValue)
                classCase.IsMandatory = request.IsMandatory.Value;

            await _unitOfWork.SaveAsync();
            return await GetAssignmentByIdAsync(assignmentId);
        }

        // Try to update ClassQuizSession
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .Include(cqs => cqs.Class)
            .Include(cqs => cqs.Quiz)
            .FirstOrDefaultAsync(cqs => cqs.Id == assignmentId);

        if (quizSession == null)
            throw new KeyNotFoundException("Assignment not found.");

        if (request.DueDate.HasValue)
            quizSession.CloseTime = ToUtc(request.DueDate);
        if (request.OpenDate.HasValue)
            quizSession.OpenTime = ToUtc(request.OpenDate);
        if (request.PassingScore.HasValue)
            quizSession.PassingScore = request.PassingScore.Value;
        if (request.TimeLimitMinutes.HasValue)
            quizSession.TimeLimitMinutes = request.TimeLimitMinutes.Value;
        if (request.AllowRetake.HasValue)
            quizSession.AllowRetake = request.AllowRetake.Value;
        if (request.AllowLate.HasValue)
            quizSession.AllowLate = request.AllowLate.Value;
        if (request.ShowResultsAfterSubmission.HasValue)
            quizSession.ShowResultsAfterSubmission = request.ShowResultsAfterSubmission.Value;

        await _unitOfWork.SaveAsync();
        return await GetAssignmentByIdAsync(assignmentId);
    }

    /// <summary>Delete an assignment.</summary>
    public async Task DeleteAssignmentAsync(Guid assignmentId)
    {
        // Try to delete ClassCase
        var classCase = await _unitOfWork.Context.ClassCases
            .FirstOrDefaultAsync(cc => cc.CaseId == assignmentId);

        if (classCase != null)
        {
            _unitOfWork.Context.ClassCases.Remove(classCase);
            await _unitOfWork.SaveAsync();
            return;
        }

        // Try to delete ClassQuizSession
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .FirstOrDefaultAsync(cqs => cqs.Id == assignmentId);

        if (quizSession == null)
            throw new KeyNotFoundException("Assignment not found.");

        _unitOfWork.Context.ClassQuizSessions.Remove(quizSession);
        await _unitOfWork.SaveAsync();
    }

    /// <summary>Get submissions list of an assignment.</summary>
    public async Task<IReadOnlyList<AssignmentSubmissionDto>> GetAssignmentSubmissionsAsync(Guid assignmentId)
    {
        // Try to get submissions from ClassCases (case)
        var classCase = await _unitOfWork.Context.ClassCases
            .AsNoTracking()
            .Include(cc => cc.Class)
            .FirstOrDefaultAsync(cc => cc.CaseId == assignmentId);

        if (classCase != null)
        {
            var enrollments = await _unitOfWork.Context.ClassEnrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Where(e => e.ClassId == classCase.ClassId)
                .ToListAsync();

            var viewLogs = await _unitOfWork.Context.CaseViewLogs
                .AsNoTracking()
                .Where(v => v.CaseId == assignmentId)
                .ToDictionaryAsync(v => v.StudentId);

            return enrollments.Select(e => new AssignmentSubmissionDto
            {
                StudentId = e.StudentId,
                StudentName = e.Student?.FullName ?? "Unknown",
                StudentCode = e.Student?.SchoolCohort,
                SubmittedAt = viewLogs.TryGetValue(e.StudentId, out var log) ? log.ViewedAt : null,
                Score = null,
                Status = viewLogs.ContainsKey(e.StudentId) ? "graded" : "not-submitted"
            }).ToList();
        }

        // Get submissions from ClassQuizSessions (quiz)
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Quiz)
            .FirstOrDefaultAsync(cqs => cqs.Id == assignmentId)
            ?? throw new KeyNotFoundException("Assignment not found.");

        var quizEnrollments = await _unitOfWork.Context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Where(e => e.ClassId == quizSession.ClassId)
            .ToListAsync();

        var attempts = await _unitOfWork.Context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.QuizId == quizSession.QuizId)
            .ToDictionaryAsync(a => a.StudentId);

        return quizEnrollments.Select(e => new AssignmentSubmissionDto
        {
            StudentId = e.StudentId,
            StudentName = e.Student?.FullName ?? "Unknown",
            StudentCode = e.Student?.SchoolCohort,
            SubmittedAt = attempts.TryGetValue(e.StudentId, out var attempt) ? attempt.CompletedAt : null,
            Score = attempts.TryGetValue(e.StudentId, out var attempt2) ? attempt2.Score : null,
            Status = attempts.TryGetValue(e.StudentId, out var attempt3)
                ? (attempt3.Score.HasValue ? "graded" : "pending")
                : "not-submitted"
        }).ToList();
    }

    /// <summary>Update scores for multiple submissions.</summary>
    public async Task<IReadOnlyList<AssignmentSubmissionDto>> UpdateAssignmentSubmissionsAsync(
        Guid assignmentId, UpdateSubmissionsRequestDto request)
    {
        var quizSession = await _unitOfWork.Context.ClassQuizSessions
            .AsNoTracking()
            .Include(cqs => cqs.Quiz)
            .FirstOrDefaultAsync(cqs => cqs.Id == assignmentId)
            ?? throw new KeyNotFoundException("Assignment not found.");

        var studentIds = request.Submissions.Select(s => s.StudentId).ToList();
        var attempts = await _unitOfWork.Context.QuizAttempts
            .Where(a => a.QuizId == quizSession.QuizId && studentIds.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId);

        foreach (var update in request.Submissions)
        {
            if (attempts.TryGetValue(update.StudentId, out var attempt))
            {
                attempt.Score = update.Score;
            }
        }

        await _unitOfWork.SaveAsync();
        return await GetAssignmentSubmissionsAsync(assignmentId);
    }

    public async Task<bool> DeleteMedicalImageAsync(Guid imageId)
    {
        return await _medicalCaseService.DeleteMedicalImageAsync(imageId);
    }
}
