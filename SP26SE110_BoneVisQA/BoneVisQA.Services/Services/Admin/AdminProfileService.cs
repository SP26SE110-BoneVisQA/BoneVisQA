using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Mapping;
using BoneVisQA.Services.Models.Admin;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Admin;

public class AdminProfileService : IAdminProfileService
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminProfileService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminProfileDto> GetProfileAsync(Guid adminId)
    {
        var user = await LoadAdminWithRolesAsync(adminId);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay ho so admin.");

        return ToDto(user);
    }

    public async Task<AdminProfileDto> UpdateProfileAsync(Guid adminId, UpdateAdminProfileRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("FullName la bat buoc.");

        var user = await LoadAdminWithRolesAsync(adminId);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay ho so admin.");

        user.FullName = request.FullName.Trim();
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

        return ToDto(user);
    }

    private Task<User?> LoadAdminWithRolesAsync(Guid adminId)
    {
        return _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == adminId);
    }

    private static AdminProfileDto ToDto(User user)
    {
        return new AdminProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
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
            EmergencyContact = user.EmergencyContact
        };
    }
}
