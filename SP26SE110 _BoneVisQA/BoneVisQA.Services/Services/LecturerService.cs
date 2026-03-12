using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services;

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
            CreatedAt = now,
            UpdatedAt = now
        };

        await _unitOfWork.AcademicClassRepository.CreateAsync(entity);
        await _unitOfWork.SaveAsync();

        return new ClassDto
        {
            Id = entity.Id,
            ClassName = entity.ClassName,
            Semester = entity.Semester,
            LecturerId = entity.LecturerId,
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

        var enrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = studentId,
            EnrolledAt = DateTime.UtcNow
        };

        await _unitOfWork.ClassEnrollmentRepository.CreateAsync(enrollment);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task<IReadOnlyList<StudentEnrollmentDto>> EnrollStudentsAsync(Guid classId, EnrollStudentsRequestDto request)
    {
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
                EnrolledAt = DateTime.UtcNow
            };

            await _unitOfWork.ClassEnrollmentRepository.CreateAsync(enrollment);
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

        await _unitOfWork.AnnouncementRepository.CreateAsync(entity);
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

    public async Task<QuizDto> CreateQuizAsync(Guid classId, CreateQuizRequestDto request)
    {
        var now = DateTime.UtcNow;
        var entity = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            OpenTime = request.OpenTime,
            CloseTime = request.CloseTime,
            TimeLimit = request.TimeLimit,
            PassingScore = request.PassingScore,
            CreatedAt = now
        };

        await _unitOfWork.QuizRepository.CreateAsync(entity);
        await _unitOfWork.SaveAsync();

        var classQuiz = new ClassQuiz
        {
            ClassId = classId,
            QuizId = entity.Id,
            AssignedAt = now
        };
        await _unitOfWork.ClassQuizRepository.CreateAsync(classQuiz);
        await _unitOfWork.SaveAsync();

        return new QuizDto
        {
            Id = entity.Id,
            ClassId = classId,
            Title = entity.Title,
            OpenTime = entity.OpenTime,
            CloseTime = entity.CloseTime,
            TimeLimit = entity.TimeLimit,
            PassingScore = entity.PassingScore
        };
    }


    public async Task<ClassStatsDto> GetClassStatsAsync(Guid classId)
    {
        var stats = await _unitOfWork.LearningStatisticRepository
            .FindByCondition(s => s.ClassId == classId)
            .ToListAsync();

        var totalStudents = stats.Count;
        var totalCasesViewed = stats.Sum(s => s.TotalCasesViewed ?? 0);
        var totalQuestionsAsked = stats.Sum(s => s.TotalQuestionsAsked ?? 0);
        double? avgQuizScore = null;
        if (stats.Count > 0)
        {
            avgQuizScore = stats.Average(s => s.AvgQuizScore ?? 0);
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

    public async Task<QuizQuestionDto> AddQuizQuestionAsync(CreateQuizQuestionRequestDto request)
    {
        var entity = new QuizQuestion
        {
            Id = Guid.NewGuid(),
            QuizId = request.QuizId,
            CaseId = request.CaseId,
            QuestionText = request.QuestionText,
            Type = request.Type,
            CorrectAnswer = request.CorrectAnswer
        };

        await _unitOfWork.QuizQuestionRepository.CreateAsync(entity);
        await _unitOfWork.SaveAsync();

        return new QuizQuestionDto
        {
            Id = entity.Id,
            QuizId = entity.QuizId,
            CaseId = entity.CaseId,
            QuestionText = entity.QuestionText,
            Type = entity.Type ?? "multiple_choice",
            CorrectAnswer = entity.CorrectAnswer
        };
    }

    public async Task<IReadOnlyList<QuizQuestionDto>> GetQuizQuestionsAsync(Guid quizId)
    {
        var questions = await _unitOfWork.QuizQuestionRepository
            .FindByCondition(q => q.QuizId == quizId)
            .Include(q => q.Case)
            .ToListAsync();

        return questions
            .Select(q => new QuizQuestionDto
            {
                Id = q.Id,
                QuizId = q.QuizId,
                CaseId = q.CaseId,
                CaseTitle = q.Case?.Title,
                QuestionText = q.QuestionText,
                Type = q.Type ?? "multiple_choice",
                CorrectAnswer = q.CorrectAnswer
            })
            .ToList();
    }

    public async Task<bool> UpdateQuizQuestionAsync(Guid questionId, UpdateQuizQuestionRequestDto request)
    {
        var entity = await _unitOfWork.QuizQuestionRepository
            .FindByCondition(q => q.Id == questionId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            return false;
        }

        entity.QuestionText = request.QuestionText;
        entity.Type = request.Type ?? entity.Type;
        entity.CorrectAnswer = request.CorrectAnswer ?? entity.CorrectAnswer;

        await _unitOfWork.QuizQuestionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveAsync();
        return true;
    }

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
                IsApproved = c.IsApproved,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<CaseDto>> AssignCasesToClassAsync(Guid classId, AssignCasesToClassRequestDto request)
    {
        foreach (var caseId in request.CaseIds)
        {
            var caseTags = await _unitOfWork.CaseTagRepository
                .FindByCondition(ct => ct.CaseId == caseId)
                .ToListAsync();

            foreach (var caseTag in caseTags)
            {
                var existingClassTag = await _unitOfWork.CaseTagRepository
                    .FindByCondition(ct => ct.CaseId == caseTag.CaseId && ct.TagId == caseTag.TagId)
                    .FirstOrDefaultAsync();
            }
        }

        await _unitOfWork.SaveAsync();
        return await GetAllCasesAsync();
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
                AnswerStatus = q.CaseAnswers?.FirstOrDefault()?.Status
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
}
