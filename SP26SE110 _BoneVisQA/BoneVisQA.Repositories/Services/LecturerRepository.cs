using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Services;

public class LecturerRepository : ILecturerRepository
{
    private readonly BoneVisQADbContext _context;

    public LecturerRepository(BoneVisQADbContext context)
    {
        _context = context;
    }

    public async Task<List<AcademicClass>> GetClassesByLecturerAsync(Guid lecturerId)
    {
        return await _context.AcademicClasses
            .AsNoTracking()
            .Where(c => c.LecturerId == lecturerId)
            .ToListAsync();
    }

    public async Task<AcademicClass> CreateClassAsync(AcademicClass academicClass)
    {
        _context.AcademicClasses.Add(academicClass);
        await _context.SaveChangesAsync();
        return academicClass;
    }

    public async Task<ClassEnrollment?> GetEnrollmentAsync(Guid classId, Guid studentId)
    {
        return await _context.ClassEnrollments
            .FirstOrDefaultAsync(e => e.ClassId == classId && e.StudentId == studentId);
    }

    public async Task<ClassEnrollment> AddEnrollmentAsync(ClassEnrollment enrollment)
    {
        _context.ClassEnrollments.Add(enrollment);
        await _context.SaveChangesAsync();
        return enrollment;
    }

    public async Task<Announcement> CreateAnnouncementAsync(Announcement announcement)
    {
        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();
        return announcement;
    }

    public async Task<List<LearningStatistic>> GetLearningStatisticsByClassAsync(Guid classId)
    {
        return await _context.LearningStatistics
            .AsNoTracking()
            .Where(s => s.ClassId == classId)
            .ToListAsync();
    }

    public async Task<Quiz> CreateQuizAsync(Quiz quiz)
    {
        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync();
        return quiz;
    }
}

