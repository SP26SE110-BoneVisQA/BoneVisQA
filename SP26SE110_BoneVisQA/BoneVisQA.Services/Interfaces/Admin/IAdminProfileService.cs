using BoneVisQA.Services.Models.Admin;

namespace BoneVisQA.Services.Interfaces.Admin;

public interface IAdminProfileService
{
    Task<AdminProfileDto> GetProfileAsync(Guid adminId);
    Task<AdminProfileDto> UpdateProfileAsync(Guid adminId, UpdateAdminProfileRequestDto request);
}
