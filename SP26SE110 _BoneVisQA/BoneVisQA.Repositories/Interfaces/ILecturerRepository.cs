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

    Task<Announcement> CreateAnnouncementAsync(Announcement announcement);

    Task<List<LearningStatistic>> GetLearningStatisticsByClassAsync(Guid classId);

    Task<Quiz> CreateQuizAsync(Quiz quiz);
}

