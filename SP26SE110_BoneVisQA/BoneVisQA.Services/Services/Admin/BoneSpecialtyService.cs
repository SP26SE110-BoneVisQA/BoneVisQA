using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Admin;

public class BoneSpecialtyService : IBoneSpecialtyService
{
    private readonly IUnitOfWork _unitOfWork;

    public BoneSpecialtyService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<BoneSpecialtyDto>> GetAllAsync(BoneSpecialtyQueryDto? query = null)
    {
        query ??= new BoneSpecialtyQueryDto();

        var queryable = _unitOfWork.Context.BoneSpecialties
            .AsQueryable();

        if (query.ParentId.HasValue)
            queryable = queryable.Where(bs => bs.ParentId == query.ParentId);
        else if (query.ParentId == null && query.FlatList == false)
            queryable = queryable.Where(bs => bs.ParentId == null);

        if (query.IsActive.HasValue)
            queryable = queryable.Where(bs => bs.IsActive == query.IsActive);

        var specialties = await queryable
            .Include(bs => bs.Parent)
            .OrderBy(bs => bs.DisplayOrder)
            .ThenBy(bs => bs.Name)
            .ToListAsync();

        if (query.FlatList)
        {
            return specialties.Select(MapToDto).ToList();
        }
        else
        {
            return BuildHierarchy(specialties);
        }
    }

    public async Task<List<BoneSpecialtyDto>> GetTreeAsync()
    {
        var all = await _unitOfWork.Context.BoneSpecialties
            .OrderBy(bs => bs.DisplayOrder)
            .ThenBy(bs => bs.Name)
            .ToListAsync();

        return BuildHierarchy(all);
    }

    public async Task<BoneSpecialtyDto?> GetByIdAsync(Guid id)
    {
        var specialty = await _unitOfWork.Context.BoneSpecialties
            .Include(bs => bs.Parent)
            .Include(bs => bs.Children)
            .FirstOrDefaultAsync(bs => bs.Id == id);

        return specialty == null ? null : MapToDto(specialty);
    }

    public async Task<BoneSpecialtyDto> CreateAsync(BoneSpecialtyCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            throw new ArgumentException("Code is required.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Name is required.");

        var existingCode = await _unitOfWork.Context.BoneSpecialties
            .AnyAsync(bs => bs.Code == dto.Code);

        if (existingCode)
            throw new InvalidOperationException($"BoneSpecialty with code '{dto.Code}' already exists.");

        if (dto.ParentId.HasValue)
        {
            var parent = await _unitOfWork.Context.BoneSpecialties
                .FirstOrDefaultAsync(bs => bs.Id == dto.ParentId.Value);

            if (parent == null)
                throw new KeyNotFoundException("Parent specialty not found.");
        }

        var specialty = new BoneSpecialty
        {
            Id = Guid.NewGuid(),
            Code = dto.Code,
            Name = dto.Name,
            ParentId = dto.ParentId,
            Description = dto.Description,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _unitOfWork.Context.BoneSpecialties.Add(specialty);
        await _unitOfWork.Context.SaveChangesAsync();

        return (await GetByIdAsync(specialty.Id))!;
    }

    public async Task<BoneSpecialtyDto?> UpdateAsync(BoneSpecialtyUpdateDto dto)
    {
        if (dto.Id == Guid.Empty)
            throw new ArgumentException("Id is required.");

        var specialty = await _unitOfWork.Context.BoneSpecialties
            .FirstOrDefaultAsync(bs => bs.Id == dto.Id);

        if (specialty == null)
            return null;

        var existingCode = await _unitOfWork.Context.BoneSpecialties
            .AnyAsync(bs => bs.Code == dto.Code && bs.Id != dto.Id);

        if (existingCode)
            throw new InvalidOperationException($"BoneSpecialty with code '{dto.Code}' already exists.");

        if (dto.ParentId.HasValue)
        {
            if (dto.ParentId.Value == dto.Id)
                throw new InvalidOperationException("A specialty cannot be its own parent.");

            var parent = await _unitOfWork.Context.BoneSpecialties
                .FirstOrDefaultAsync(bs => bs.Id == dto.ParentId.Value);

            if (parent == null)
                throw new KeyNotFoundException("Parent specialty not found.");
        }

        specialty.Code = dto.Code;
        specialty.Name = dto.Name;
        specialty.ParentId = dto.ParentId;
        specialty.Description = dto.Description;
        specialty.DisplayOrder = dto.DisplayOrder;
        specialty.IsActive = dto.IsActive;
        specialty.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Context.SaveChangesAsync();

        return (await GetByIdAsync(specialty.Id))!;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var specialty = await _unitOfWork.Context.BoneSpecialties
            .Include(bs => bs.Children)
            .FirstOrDefaultAsync(bs => bs.Id == id);

        if (specialty == null)
            return false;

        if (specialty.Children.Any())
            throw new InvalidOperationException("Cannot delete a specialty that has children. Delete children first.");

        _unitOfWork.Context.BoneSpecialties.Remove(specialty);
        await _unitOfWork.Context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ToggleActiveAsync(Guid id, bool isActive)
    {
        var specialty = await _unitOfWork.Context.BoneSpecialties
            .FirstOrDefaultAsync(bs => bs.Id == id);

        if (specialty == null)
            return false;

        specialty.IsActive = isActive;
        specialty.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Context.SaveChangesAsync();

        return true;
    }

    private List<BoneSpecialtyDto> BuildHierarchy(List<BoneSpecialty> all)
    {
        var lookup = all.ToLookup(bs => bs.ParentId);
        var allIds = all.Select(bs => bs.Id).ToHashSet();
        var levelCache = new Dictionary<Guid, int>();

        int GetLevel(Guid? parentId, int parentLevel)
        {
            if (!parentId.HasValue) return 0;
            if (levelCache.TryGetValue(parentId.Value, out var cached)) return cached;
            var level = parentLevel + 1;
            levelCache[parentId.Value] = level;
            return level;
        }

        List<BoneSpecialtyDto> BuildChildren(Guid? parentId, int level)
        {
            return lookup[parentId]
                .Select(bs =>
                {
                    var dto = MapToDto(bs);
                    dto.Level = level;
                    dto.Children = BuildChildren(bs.Id, level + 1);
                    return dto;
                })
                .OrderBy(dto => dto.DisplayOrder)
                .ThenBy(dto => dto.Name)
                .ToList();
        }

        var rootItems = BuildChildren(null, 0);

        // Handle orphaned items (ParentId points to non-existent parent)
        var orphanedItems = all
            .Where(bs => bs.ParentId.HasValue && !allIds.Contains(bs.ParentId.Value))
            .Select(bs =>
            {
                var dto = MapToDto(bs);
                dto.Level = 0;
                dto.ParentId = null; // Treat as root
                dto.ParentName = "(Orphan - Parent Missing)";
                dto.Children = BuildChildren(bs.Id, 1);
                return dto;
            })
            .OrderBy(dto => dto.DisplayOrder)
            .ThenBy(dto => dto.Name)
            .ToList();

        if (orphanedItems.Any())
        {
            rootItems.AddRange(orphanedItems);
        }

        return rootItems;
    }

    private static BoneSpecialtyDto MapToDto(BoneSpecialty bs)
    {
        return new BoneSpecialtyDto
        {
            Id = bs.Id,
            Code = bs.Code,
            Name = bs.Name,
            ParentId = bs.ParentId,
            ParentName = bs.Parent?.Name,
            Description = bs.Description,
            DisplayOrder = bs.DisplayOrder,
            IsActive = bs.IsActive,
            CreatedAt = bs.CreatedAt,
            UpdatedAt = bs.UpdatedAt
        };
    }
}
