using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerService
{
    Task<ClassDto> CreateClassAsync(CreateClassRequestDto request);
    Task<IReadOnlyList<ClassDto>> GetClassesForLecturerAsync(Guid lecturerId);

    Task<bool> EnrollStudentAsync(Guid classId, Guid studentId);

    Task<AnnouncementDto> CreateAnnouncementAsync(Guid classId, CreateAnnouncementRequestDto request);

    Task<QuizDto> CreateQuizAsync(Guid classId, CreateQuizRequestDto request);

    Task<ClassStatsDto> GetClassStatsAsync(Guid classId);
}

