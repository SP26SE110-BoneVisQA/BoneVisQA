using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Services;

public class LecturerService : ILecturerService
{
    private readonly LecturerRepository _lecturerRepository;

    public LecturerService(LecturerRepository lecturerRepository)
    {
        _lecturerRepository = lecturerRepository;
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

        var created = await _lecturerRepository.CreateClassAsync(entity);

        return new ClassDto
        {
            Id = created.Id,
            ClassName = created.ClassName,
            Semester = created.Semester,
            LecturerId = created.LecturerId,
            CreatedAt = created.CreatedAt
        };
    }

    public async Task<IReadOnlyList<ClassDto>> GetClassesForLecturerAsync(Guid lecturerId)
    {
        var classes = await _lecturerRepository.GetClassesByLecturerAsync(lecturerId);

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
        var existing = await _lecturerRepository.GetEnrollmentAsync(classId, studentId);
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

        await _lecturerRepository.AddEnrollmentAsync(enrollment);
        return true;
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

        var created = await _lecturerRepository.CreateAnnouncementAsync(entity);

        return new AnnouncementDto
        {
            Id = created.Id,
            ClassId = created.ClassId,
            Title = created.Title,
            Content = created.Content,
            CreatedAt = created.CreatedAt
        };
    }

    public async Task<QuizDto> CreateQuizAsync(Guid classId, CreateQuizRequestDto request)
    {
        var now = DateTime.UtcNow;
        var entity = new Quiz
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            Title = request.Title,
            OpenTime = request.OpenTime,
            CloseTime = request.CloseTime,
            TimeLimit = request.TimeLimit,
            PassingScore = request.PassingScore,
            CreatedAt = now
        };

        var created = await _lecturerRepository.CreateQuizAsync(entity);

        return new QuizDto
        {
            Id = created.Id,
            ClassId = created.ClassId,
            Title = created.Title,
            OpenTime = created.OpenTime,
            CloseTime = created.CloseTime,
            TimeLimit = created.TimeLimit,
            PassingScore = created.PassingScore
        };
    }

    public async Task<ClassStatsDto> GetClassStatsAsync(Guid classId)
    {
        var stats = await _lecturerRepository.GetLearningStatisticsByClassAsync(classId);

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
}

