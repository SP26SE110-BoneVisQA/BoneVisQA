using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerReportService
{
    Task<LecturerOverallReportDto> GetOverallReportAsync(Guid lecturerId);
    Task<IReadOnlyList<ClassReportDto>> GetClassesReportAsync(Guid lecturerId);
    Task<ClassDetailedReportDto> GetClassDetailedReportAsync(Guid classId);
    Task<StudentReportDto?> GetStudentReportAsync(Guid classId, Guid studentId);
    Task<IReadOnlyList<StudentReportDto>> GetClassStudentsReportAsync(Guid classId);
    Task<QuizReportDto> GetQuizReportAsync(Guid quizSessionId);
    Task<IReadOnlyList<QuizReportDto>> GetClassQuizReportsAsync(Guid classId);
    Task<AIQualityReportDto> GetAIQualityReportAsync(Guid classId);
    Task<ActivityReportDto> GetActivityReportAsync(Guid classId, DateTime? fromDate, DateTime? toDate);
}
