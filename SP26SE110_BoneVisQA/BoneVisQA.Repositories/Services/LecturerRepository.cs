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

    public async Task<bool> RemoveEnrollmentAsync(Guid classId, Guid studentId)
    {
        var enrollment = await _context.ClassEnrollments
            .FirstOrDefaultAsync(e => e.ClassId == classId && e.StudentId == studentId);

        if (enrollment == null)
        {
            return false;
        }

        _context.ClassEnrollments.Remove(enrollment);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<StudentEnrollmentInfo>> GetEnrollmentsByClassAsync(Guid classId)
    {
        return await _context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.ClassId == classId)
            .Join(_context.Users, e => e.StudentId, u => u.Id, (e, u) => new StudentEnrollmentInfo
            {
                EnrollmentId = e.Id,
                StudentId = e.StudentId,
                StudentName = u.FullName,
                StudentEmail = u.Email,
                StudentCode = u.SchoolCohort,
                EnrolledAt = e.EnrolledAt
            })
            .ToListAsync();
    }

    public async Task<List<StudentEnrollmentInfo>> GetUnenrolledStudentsAsync(Guid classId)
    {
        var enrolledStudentIds = await _context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.ClassId == classId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var studentRole = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == "Student");

        if (studentRole == null)
        {
            return new List<StudentEnrollmentInfo>();
        }

        var studentUserIds = await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == studentRole.Id)
            .Select(ur => ur.UserId)
            .ToListAsync();

        var unenrolledStudents = await _context.Users
            .AsNoTracking()
            .Where(u => studentUserIds.Contains(u.Id) && !enrolledStudentIds.Contains(u.Id))
            .Select(u => new StudentEnrollmentInfo
            {
                EnrollmentId = Guid.Empty,
                StudentId = u.Id,
                StudentName = u.FullName,
                StudentEmail = u.Email,
                StudentCode = u.SchoolCohort,
                EnrolledAt = null
            })
            .ToListAsync();

        return unenrolledStudents;
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

