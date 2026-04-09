using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Mapping;
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
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        return Map(user);
    }

    public async Task<StudentProfileDto> UpdateProfileAsync(Guid studentId, UpdateStudentProfileRequestDto request)
    {
        var user = await _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == studentId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        user.FullName = request.FullName.Trim();
        user.SchoolCohort = string.IsNullOrWhiteSpace(request.SchoolCohort) ? null : request.SchoolCohort.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        UserPersonalFieldsHelper.Apply(
            user,
            request.DateOfBirth,
            request.PhoneNumber,
            request.Gender,
            request.StudentSchoolId,
            request.ClassCode,
            request.Address,
            request.Bio,
            request.EmergencyContact);
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.UserRepository.UpdateAsync(user);
        await _unitOfWork.SaveAsync();

        return Map(user);
    }

    private static StudentProfileDto Map(BoneVisQA.Repositories.Models.User user)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        return new StudentProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = roles.FirstOrDefault(),
            SchoolCohort = user.SchoolCohort,
            AvatarUrl = user.AvatarUrl,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLogin = user.LastLogin,
            Roles = roles,
            DateOfBirth = user.DateOfBirth,
            PhoneNumber = user.PhoneNumber,
            Gender = user.Gender,
            StudentSchoolId = user.StudentSchoolId,
            ClassCode = user.ClassCode,
            Address = user.Address,
            Bio = user.Bio,
            EmergencyContact = user.EmergencyContact,
        };
    }
}
