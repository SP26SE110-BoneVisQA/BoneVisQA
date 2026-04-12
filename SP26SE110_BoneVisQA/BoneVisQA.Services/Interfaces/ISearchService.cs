using BoneVisQA.Services.Models.Search;

namespace BoneVisQA.Services.Interfaces;

public interface ISearchService
{
    Task<GlobalSearchResponseDto> SearchAsync(Guid userId, IReadOnlyCollection<string> roles, string query);
}
