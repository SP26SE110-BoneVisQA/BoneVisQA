using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Repositories.Models;

namespace BoneVisQA.Repositories.Interfaces;

public interface ILecturerRepository
{
    Task<List<AcademicClass>> GetClassesByLecturerAsync(Guid lecturerId);
    Task<AcademicClass> CreateClassAsync(AcademicClass academicClass);

    Task<ClassEnrollment?> GetEnrollmentAsync(Guid classId, Guid studentId);
    Task<ClassEnrollment> AddEnrollmentAsync(ClassEnrollment enrollment);
    Task<bool> RemoveEnrollmentAsync(Guid classId, Guid studentId);

    Task<List<StudentEnrollmentInfo>> GetEnrollmentsByClassAsync(Guid classId);
    Task<List<StudentEnrollmentInfo>> GetUnenrolledStudentsAsync(Guid classId);

    Task<Announcement> CreateAnnouncementAsync(Announcement announcement);

    Task<List<LearningStatistic>> GetLearningStatisticsByClassAsync(Guid classId);

    Task<Quiz> CreateQuizAsync(Quiz quiz);
}

public class StudentEnrollmentInfo
{
    /// <summary><c>null</c> when listing students not yet enrolled in the class.</summary>
    public Guid? EnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public DateTime? EnrolledAt { get; set; }
}

