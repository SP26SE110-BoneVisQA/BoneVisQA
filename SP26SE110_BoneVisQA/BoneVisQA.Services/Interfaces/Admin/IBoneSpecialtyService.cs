using BoneVisQA.Services.Models.Admin;

namespace BoneVisQA.Services.Interfaces.Admin;

public interface IBoneSpecialtyService
{
    Task<List<BoneSpecialtyDto>> GetAllAsync(BoneSpecialtyQueryDto? query = null);
    Task<List<BoneSpecialtyDto>> GetTreeAsync();
    Task<BoneSpecialtyDto?> GetByIdAsync(Guid id);
    Task<BoneSpecialtyDto> CreateAsync(BoneSpecialtyCreateDto dto);
    Task<BoneSpecialtyDto?> UpdateAsync(BoneSpecialtyUpdateDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ToggleActiveAsync(Guid id, bool isActive);
}
