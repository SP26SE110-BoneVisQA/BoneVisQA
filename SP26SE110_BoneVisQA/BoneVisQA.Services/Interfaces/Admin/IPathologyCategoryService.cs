using BoneVisQA.Services.Models.Admin;

namespace BoneVisQA.Services.Interfaces.Admin;

public interface IPathologyCategoryService
{
    Task<List<PathologyCategoryDto>> GetAllAsync(PathologyCategoryQueryDto? query = null);
    Task<PathologyCategoryDto?> GetByIdAsync(Guid id);
    Task<PathologyCategoryDto> CreateAsync(PathologyCategoryCreateDto dto);
    Task<PathologyCategoryDto?> UpdateAsync(PathologyCategoryUpdateDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ToggleActiveAsync(Guid id, bool isActive);
}
