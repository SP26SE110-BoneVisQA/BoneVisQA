using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Student;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Student;

public class StudentProfileService : IStudentProfileService
{
    private readonly IUnitOfWork _unitOfWork;

    public StudentProfileService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<StudentProfileDto> GetProfileAsync(Guid studentId)
    {
        var user = await _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == studentId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ sinh viên.");

        return Map(user);
    }

    public async Task<StudentProfileDto> UpdateProfileAsync(Guid studentId, UpdateStudentProfileRequestDto request)
    {
        var user = await _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == studentId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ sinh viên.");

        user.FullName = request.FullName.Trim();
        user.SchoolCohort = string.IsNullOrWhiteSpace(request.SchoolCohort) ? null : request.SchoolCohort.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.UserRepository.UpdateAsync(user);
        await _unitOfWork.SaveAsync();

        return Map(user);
    }

    private static StudentProfileDto Map(BoneVisQA.Repositories.Models.User user)
    {
        return new StudentProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            SchoolCohort = user.SchoolCohort,
            AvatarUrl = user.AvatarUrl,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLogin = user.LastLogin,
            Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
        };
    }
}
