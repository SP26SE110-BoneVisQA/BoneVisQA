using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Mapping;
using BoneVisQA.Services.Models.Expert;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Expert;

public class ExpertProfileService : IExpertProfileService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExpertProfileService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ExpertProfileDto> GetProfileAsync(Guid expertId)
    {
        var user = await _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == expertId)
            ?? throw new KeyNotFoundException("Khong tim thay ho so chuyen gia.");

        return Map(user);
    }

    public async Task<ExpertProfileDto> UpdateProfileAsync(Guid expertId, UpdateExpertProfileRequestDto request)
    {
        var user = await _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == expertId)
            ?? throw new KeyNotFoundException("Khong tim thay ho so chuyen gia.");

        user.FullName = request.FullName.Trim();
        user.Specialty = string.IsNullOrWhiteSpace(request.Specialty) ? null : request.Specialty.Trim();
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

    private static ExpertProfileDto Map(BoneVisQA.Repositories.Models.User user)
    {
        return new ExpertProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Specialty = user.Specialty,
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
