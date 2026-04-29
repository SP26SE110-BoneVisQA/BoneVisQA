using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Interfaces.Expert;

public interface IExpertSpecialtyService
{
    Task<List<ExpertSpecialtyDto>> GetMySpecialtiesAsync(Guid expertId);
    Task<ExpertSpecialtyDto?> GetByIdAsync(Guid id);
    Task<ExpertSpecialtyDto> CreateAsync(Guid expertId, ExpertSpecialtyCreateDto dto);
    Task<ExpertSpecialtyDto?> UpdateAsync(Guid expertId, ExpertSpecialtyUpdateDto dto);
    Task<bool> DeleteAsync(Guid expertId, Guid id);
    Task<List<ExpertSuggestionDto>> GetSuggestedExpertsAsync(Guid boneSpecialtyId, Guid? pathologyCategoryId = null);
}
