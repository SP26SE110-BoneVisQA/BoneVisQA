using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerGradeBookService
{
    Task<GradeBookDto> GetClassGradeBookAsync(Guid classId);
    Task<StudentGradeDto?> GetStudentGradeAsync(Guid classId, Guid studentId);
    Task<IReadOnlyList<StudentGradeDto>> GetAllStudentGradesAsync(Guid classId);
    Task<GradeBookExportDto> ExportGradeBookAsync(Guid classId);
    Task<bool> UpdateStudentGradeAsync(Guid classId, Guid studentId, UpdateStudentGradeRequestDto request);
    Task<IReadOnlyList<GradeSummaryDto>> GetGradeSummaryAsync(Guid classId);
}
