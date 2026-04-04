using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerService : ILecturerService
{
    private readonly IUnitOfWork _unitOfWork;

    public LecturerService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ClassDto> CreateClassAsync(CreateClassRequestDto request)
    {
        var now = DateTime.UtcNow;
        var entity = new AcademicClass
        {
            Id = Guid.NewGuid(),
            ClassName = request.ClassName,
            Semester = request.Semester,
            LecturerId = request.LecturerId,
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

    public async Task<bool> EnrollStudentAsync(Guid classId, Guid studentId)
    {
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

    public async Task<IReadOnlyList<StudentEnrollmentDto>> EnrollStudentsAsync(Guid classId, EnrollStudentsRequestDto request)
    {
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
        return await GetStudentsInClassAsync(classId);
    }

    public async Task<bool> RemoveStudentAsync(Guid classId, Guid studentId)
    {
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

    public async Task<IReadOnlyList<StudentEnrollmentDto>> GetStudentsInClassAsync(Guid classId)
    {
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

    public async Task<IReadOnlyList<StudentEnrollmentDto>> GetAvailableStudentsAsync(Guid classId)
    {
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
                EnrollmentId = Guid.Empty,
                StudentId = u.Id,
                StudentName = u.FullName,
                StudentEmail = u.Email,
                StudentCode = u.SchoolCohort,
                EnrolledAt = null
            })
            .ToList();
    }

    public async Task<AnnouncementDto> CreateAnnouncementAsync(Guid classId, CreateAnnouncementRequestDto request)
    {
        var now = DateTime.UtcNow;
        var entity = new Announcement
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            Title = request.Title,
            Content = request.Content,
            CreatedAt = now
        };

        await _unitOfWork.AnnouncementRepository.AddAsync(entity);
        await _unitOfWork.SaveAsync();

        return new AnnouncementDto
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            Title = entity.Title,
            Content = entity.Content,
            CreatedAt = entity.CreatedAt
        };
    }

    //====================================================================================================
    public async Task<List<QuizQuestionDto>> GetQuizQuestionsAsync(Guid quizId)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId)
            ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

        var questions = await _unitOfWork.QuizQuestionRepository
            .FindIncludeAsync(
                q => q.QuizId == quizId,
                q => q.Quiz,
                q => q.Case  // ✅ thêm include Case
            );

        return questions.Select(q => new QuizQuestionDto
        {
            Id = q.Id,
            QuizId = q.QuizId,
            QuizTitle = quiz.Title,
            CaseId = q.CaseId,
            CaseTitle = q.Case?.Title ?? "",
            QuestionText = q.QuestionText,
            Type = q.Type,
            OptionA = q.OptionA,
            OptionB = q.OptionB,
            OptionC = q.OptionC,
            OptionD = q.OptionD,
            CorrectAnswer = q.CorrectAnswer
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
            Type = question.Type,
            OptionA = question.OptionA,
            OptionB = question.OptionB,
            OptionC = question.OptionC,
            OptionD = question.OptionD,
            CorrectAnswer = question.CorrectAnswer
        };
    }

    public async Task<QuizDto> CreateQuizAsync(CreateQuizRequestDto request)
    {
        var now = DateTime.UtcNow;

        if (request.ClassId != Guid.Empty)
        {
            _ = await _unitOfWork.AcademicClassRepository.GetByIdAsync(request.ClassId)
                ?? throw new KeyNotFoundException("Không tìm thấy lớp học.");
        }

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            OpenTime = request.OpenTime,
            CloseTime = request.CloseTime,
            TimeLimit = request.TimeLimit,
            PassingScore = request.PassingScore,
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
                OpenTime = request.OpenTime,
                CloseTime = request.CloseTime,
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
            OpenTime = quiz.OpenTime,
            CloseTime = quiz.CloseTime,
            TimeLimit = quiz.TimeLimit,
            PassingScore = quiz.PassingScore,
            CreatedAt = quiz.CreatedAt
        };
    }

    public async Task<QuizQuestionDto> AddQuizQuestionAsync(Guid quizId, CreateQuizQuestionDto request)
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

        return new QuizQuestionDto
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
    public async Task<UpdateQuizsQuestionResponseDto> UpdateQuizQuestionAsync(Guid questionId, UpdateQuizsQuestionRequestDto request)
    {
        var entity = await _unitOfWork.QuizQuestionRepository.GetByIdAsync(questionId)
            ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");

        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(entity.QuizId)
            ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

        if (entity == null)
            throw new KeyNotFoundException("Không tìm thấy cau hoi.");


        entity.QuestionText = request.QuestionText;
        entity.Type = request.Type;
        entity.OptionA = request.OptionA;
        entity.OptionB = request.OptionB;
        entity.OptionC = request.OptionC;
        entity.OptionD = request.OptionD;
        entity.CorrectAnswer = request.CorrectAnswer;

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
        var studentIds = await _unitOfWork.ClassEnrollmentRepository
            .FindByCondition(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var totalStudents = studentIds.Count;

        var totalCasesViewed = 0;
        if (studentIds.Count > 0)
        {
            totalCasesViewed = await _unitOfWork.CaseViewLogRepository
                .FindByCondition(v => studentIds.Contains(v.StudentId))
                .CountAsync();
        }

        var totalQuestionsAsked = 0;
        if (studentIds.Count > 0)
        {
            totalQuestionsAsked = await _unitOfWork.StudentQuestionRepository
                .FindByCondition(q => studentIds.Contains(q.StudentId))
                .CountAsync();
        }

        double? avgQuizScore = null;
        if (studentIds.Count > 0)
        {
            var quizIdsInClass = await _unitOfWork.Context.ClassQuizSessions
                .Where(cqs => cqs.ClassId == classId)
                .Select(cqs => cqs.QuizId)
                .Distinct()
                .ToListAsync();

            if (quizIdsInClass.Count > 0)
            {
                var scores = await _unitOfWork.QuizAttemptRepository
                    .FindByCondition(a =>
                        studentIds.Contains(a.StudentId)
                        && quizIdsInClass.Contains(a.QuizId)
                        && a.Score.HasValue
                        && a.CompletedAt.HasValue)
                    .Select(a => a.Score!.Value)
                    .ToListAsync();

                if (scores.Count > 0)
                    avgQuizScore = scores.Average();
            }
        }

        if (avgQuizScore == null)
        {
            var legacyStats = await _unitOfWork.LearningStatisticRepository
                .FindByCondition(s => s.ClassId == classId && s.AvgQuizScore.HasValue)
                .Select(s => s.AvgQuizScore!.Value)
                .ToListAsync();
            if (legacyStats.Count > 0)
                avgQuizScore = legacyStats.Average();
        }

        return new ClassStatsDto
        {
            ClassId = classId,
            TotalStudents = totalStudents,
            TotalCasesViewed = totalCasesViewed,
            TotalQuestionsAsked = totalQuestionsAsked,
            AvgQuizScore = avgQuizScore
        };
    }

    public async Task<IReadOnlyList<CaseDto>> AssignCasesToClassAsync(Guid classId, AssignCasesToClassRequestDto request)
    {
        var classEntity = await _unitOfWork.AcademicClassRepository.GetByIdAsync(classId)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp học.");

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
            .Select(q => new LectStudentQuestionDto
            {
                Id = q.Id,
                StudentId = q.StudentId,
                StudentName = q.Student?.FullName ?? string.Empty,
                StudentEmail = q.Student?.Email ?? string.Empty,
                CaseId = q.CaseId ?? Guid.Empty,
                CaseTitle = q.Case?.Title ?? string.Empty,
                QuestionText = q.QuestionText,
                Language = q.Language,
                CreatedAt = q.CreatedAt,
                AnswerText = q.CaseAnswers?.FirstOrDefault()?.AnswerText,
                AnswerStatus = q.CaseAnswers?.FirstOrDefault()?.Status,
                EscalatedById = q.CaseAnswers?.FirstOrDefault()?.EscalatedById,
                EscalatedAt = q.CaseAnswers?.FirstOrDefault()?.EscalatedAt,
                AiConfidenceScore = q.CaseAnswers?.FirstOrDefault()?.AiConfidenceScore
            })
            .ToList();
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

        if (classIds.Count == 0)
        {
            return new List<ClassQuizDto>();
        }

        // Get all quizzes assigned to these classes
        var classQuizzes = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => classIds.Contains(cqs.ClassId))
            .Include(cqs => cqs.Quiz)
            .Include(cqs => cqs.Class)
            .OrderByDescending(cqs => cqs.CreatedAt)
            .ToListAsync();

        return classQuizzes
            .Select(cq => new ClassQuizDto
            {
                ClassId = cq.ClassId,
                QuizId = cq.QuizId,
                QuizName = cq.Quiz?.Title,
                ClassName = cq.Class?.ClassName,
                AssignedAt = cq.CreatedAt
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
                CreatedAt = cqs.CreatedAt
            })
            .ToList();
    }

    public async Task<QuizDto?> GetQuizByIdAsync(Guid quizId)
    {
        var quiz = await _unitOfWork.QuizRepository.GetByIdAsync(quizId);
        if (quiz == null)
            return null;

        var classLink = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => cqs.QuizId == quizId)
            .FirstOrDefaultAsync();

        return new QuizDto
        {
            Id = quiz.Id,
            ClassId = classLink?.ClassId ?? Guid.Empty,
            Title = quiz.Title,
            OpenTime = quiz.OpenTime,
            CloseTime = quiz.CloseTime,
            TimeLimit = quiz.TimeLimit,
            PassingScore = quiz.PassingScore,
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

        var classByQuiz = await _unitOfWork.Context.ClassQuizSessions
            .Where(cqs => distinct.Contains(cqs.QuizId))
            .ToListAsync();

        return quizzes
            .Select(q =>
            {
                var link = classByQuiz.FirstOrDefault(c => c.QuizId == q.Id);
                return new QuizDto
                {
                    Id = q.Id,
                    ClassId = link?.ClassId ?? Guid.Empty,
                    Title = q.Title,
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
            ?? throw new KeyNotFoundException("Không tìm thấy lớp học.");

        var quiz = await _unitOfWork.QuizRepository
            .GetByIdAsync(quizId)
            ?? throw new KeyNotFoundException("Không tìm thấy quiz.");

        var existing = await _unitOfWork.ClassQuizSessionRepository
            .FirstOrDefaultAsync(cq => cq.ClassId == classId && cq.QuizId == quizId);

        if (existing != null)
            throw new InvalidOperationException("Quiz đã được gán cho lớp này rồi.");

        var classQuiz = new ClassQuizSession
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            QuizId = quizId,
            CreatedAt = DateTime.UtcNow
            // open/close time sẽ được lecturer đặt riêng qua AssignQuizSessionAsync
        };

        await _unitOfWork.ClassQuizSessionRepository.AddAsync(classQuiz);
        await _unitOfWork.SaveAsync();

        return new ClassQuizDto
        {
            ClassId = classQuiz.ClassId,
            ClassName = academicClass.ClassName,
            QuizId = classQuiz.QuizId,
            QuizName = quiz.Title,
            AssignedAt = classQuiz.CreatedAt
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
                        ErrorMessage = "Lớp không tồn tại.",
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
                ErrorMessage = "File Excel không có dữ liệu (chỉ có header).",
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
                item.ErrorMessage = "Email trống.";
                result.Results.Add(item);
                result.FailedCount++;
                continue;
            }

            var emailLower = email.ToLowerInvariant();

            if (!studentUsers.TryGetValue(emailLower, out var user))
            {
                item.Success = false;
                item.ErrorMessage = $"Không tìm thấy sinh viên với email '{email}'.";
                result.Results.Add(item);
                result.NotFoundCount++;
                result.FailedCount++;
                continue;
            }

            if (enrolledSet.Contains(user.Id))
            {
                item.Success = false;
                item.ErrorMessage = $"Sinh viên '{user.FullName}' đã có trong lớp.";
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
}
