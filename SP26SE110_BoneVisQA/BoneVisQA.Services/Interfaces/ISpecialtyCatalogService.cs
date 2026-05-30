using BoneVisQA.Services.Models.Shared;

namespace BoneVisQA.Services.Interfaces;

public interface ISpecialtyCatalogService
{
    Task<IReadOnlyList<BoneSpecialtyDto>> GetBoneSpecialtiesAsync(CancellationToken cancellationToken = default);
}
