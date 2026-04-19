using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Mapping;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Lecturer;

public class LecturerProfileService : ILecturerProfileService
{
    private readonly IUnitOfWork _unitOfWork;

    public LecturerProfileService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<LecturerProfileDto> GetProfileAsync(Guid lecturerId)
    {
        var user = await _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == lecturerId)
            ?? throw new KeyNotFoundException("Khong tim thay ho so giang vien.");

        return Map(user);
    }

    public async Task<LecturerProfileDto> UpdateProfileAsync(Guid lecturerId, UpdateLecturerProfileRequestDto request)
    {
        var user = await _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == lecturerId)
            ?? throw new KeyNotFoundException("Khong tim thay ho so giang vien.");

        user.FullName = request.FullName.Trim();
        user.Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();
        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
            user.AvatarUrl = request.AvatarUrl.Trim();
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

    private static LecturerProfileDto Map(BoneVisQA.Repositories.Models.User user)
    {
        return new LecturerProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Department = user.Department,
            AvatarUrl = user.AvatarUrl,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLogin = user.LastLogin,
            Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
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
