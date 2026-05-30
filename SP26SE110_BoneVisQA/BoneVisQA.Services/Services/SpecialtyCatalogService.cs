using System.Text;
using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Services.Caching;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BoneVisQA.Services.Services;

public sealed class SpecialtyCatalogService : ISpecialtyCatalogService
{
    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6),
        SlidingExpiration = TimeSpan.FromHours(1)
    };

    private readonly BoneVisQADbContext _dbContext;
    private readonly IMemoryCache _cache;

    public SpecialtyCatalogService(BoneVisQADbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<IReadOnlyList<BoneSpecialtyDto>> GetBoneSpecialtiesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(SpecialtyCacheKeys.AllSpecialties, out IReadOnlyList<BoneSpecialtyDto>? cached)
            && cached != null)
        {
            return cached;
        }

        var items = await _dbContext.BoneSpecialties
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new BoneSpecialtyDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = BuildSpecialtyCode(x.Name)
            })
            .ToListAsync(cancellationToken);

        _cache.Set(SpecialtyCacheKeys.AllSpecialties, items, CacheOptions);
        return items;
    }

    private static string BuildSpecialtyCode(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var sb = new StringBuilder(name.Length);
        foreach (var ch in name.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToUpperInvariant(ch));
            }
            else if ((char.IsWhiteSpace(ch) || ch is '-' or '/' or '&') && sb.Length > 0 && sb[^1] != '_')
            {
                sb.Append('_');
            }
        }

        return sb.ToString().Trim('_');
    }
}
