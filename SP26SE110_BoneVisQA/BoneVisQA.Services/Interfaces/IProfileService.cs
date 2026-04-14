using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Http;

namespace BoneVisQA.Services.Interfaces;

public interface IProfileService
{
    Task<StudentProfileDto> GetProfileAsync(Guid userId);
    Task<StudentProfileDto> UpdateProfileAsync(Guid userId, UpdateStudentProfileRequestDto request);
    Task<string> UploadAvatarAsync(Guid userId, IFormFile file, CancellationToken cancellationToken = default);
}
