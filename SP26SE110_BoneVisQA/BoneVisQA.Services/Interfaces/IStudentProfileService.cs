using BoneVisQA.Services.Models.Student;

namespace BoneVisQA.Services.Interfaces;

public interface IStudentProfileService
{
    Task<StudentProfileDto> GetProfileAsync(Guid studentId);
    Task<StudentProfileDto> UpdateProfileAsync(Guid studentId, UpdateStudentProfileRequestDto request);
}
