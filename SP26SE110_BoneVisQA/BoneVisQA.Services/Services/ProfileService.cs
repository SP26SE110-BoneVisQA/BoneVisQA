using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Mapping;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services;

public class ProfileService : IProfileService
{
    private static readonly string[] AllowedAvatarExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISupabaseStorageService _storageService;

    public ProfileService(IUnitOfWork unitOfWork, ISupabaseStorageService storageService)
    {
        _unitOfWork = unitOfWork;
        _storageService = storageService;
    }

    public async Task<StudentProfileDto> GetProfileAsync(Guid userId)
    {
        var user = await _unitOfWork.Context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        return Map(user);
    }

    public async Task<StudentProfileDto> UpdateProfileAsync(Guid userId, UpdateStudentProfileRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("FullName is required.");

        var user = await _unitOfWork.Context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");

        user.FullName = request.FullName.Trim();
        user.SchoolCohort = string.IsNullOrWhiteSpace(request.SchoolCohort) ? null : request.SchoolCohort.Trim();
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

    public async Task<string> UploadAvatarAsync(Guid userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Avatar file is required.");
        if (file.Length > MaxAvatarBytes)
            throw new ArgumentException("Avatar file exceeds the 5MB limit.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedAvatarExtensions.Contains(ext))
            throw new ArgumentException("Only JPG, PNG, and WebP images are allowed.");

        var user = await _unitOfWork.Context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        var avatarUrl = await _storageService.UploadFileAsync(file, "avatars", $"users/{user.Id}", cancellationToken);
        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.UserRepository.UpdateAsync(user);
        await _unitOfWork.SaveAsync();
        return avatarUrl;
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
            EmergencyContact = user.EmergencyContact
        };
    }
}
