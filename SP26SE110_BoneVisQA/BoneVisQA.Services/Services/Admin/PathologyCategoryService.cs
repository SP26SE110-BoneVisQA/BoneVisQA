using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Models.Admin;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Admin;

public class PathologyCategoryService : IPathologyCategoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public PathologyCategoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<PathologyCategoryDto>> GetAllAsync(PathologyCategoryQueryDto? query = null)
    {
        query ??= new PathologyCategoryQueryDto();

        var queryable = _unitOfWork.Context.PathologyCategories
            .AsQueryable();

        if (query.BoneSpecialtyId.HasValue)
            queryable = queryable.Where(pc => pc.BoneSpecialtyId == query.BoneSpecialtyId);

        if (query.IsActive.HasValue)
            queryable = queryable.Where(pc => pc.IsActive == query.IsActive);

        var categories = await queryable
            .Include(pc => pc.BoneSpecialty)
            .OrderBy(pc => pc.DisplayOrder)
            .ThenBy(pc => pc.Name)
            .ToListAsync();

        return categories.Select(MapToDto).ToList();
    }

    public async Task<PathologyCategoryDto?> GetByIdAsync(Guid id)
    {
        var category = await _unitOfWork.Context.PathologyCategories
            .Include(pc => pc.BoneSpecialty)
            .FirstOrDefaultAsync(pc => pc.Id == id);

        return category == null ? null : MapToDto(category);
    }

    public async Task<PathologyCategoryDto> CreateAsync(PathologyCategoryCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            throw new ArgumentException("Code is required.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Name is required.");

        var existingCode = await _unitOfWork.Context.PathologyCategories
            .AnyAsync(pc => pc.Code == dto.Code);

        if (existingCode)
            throw new InvalidOperationException($"PathologyCategory with code '{dto.Code}' already exists.");

        if (dto.BoneSpecialtyId.HasValue)
        {
            var boneSpecialty = await _unitOfWork.Context.BoneSpecialties
                .FirstOrDefaultAsync(bs => bs.Id == dto.BoneSpecialtyId.Value);

            if (boneSpecialty == null)
                throw new KeyNotFoundException("BoneSpecialty not found.");
        }

        var category = new PathologyCategory
        {
            Id = Guid.NewGuid(),
            Code = dto.Code,
            Name = dto.Name,
            BoneSpecialtyId = dto.BoneSpecialtyId,
            Description = dto.Description,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _unitOfWork.Context.PathologyCategories.Add(category);
        await _unitOfWork.Context.SaveChangesAsync();

        return (await GetByIdAsync(category.Id))!;
    }

    public async Task<PathologyCategoryDto?> UpdateAsync(PathologyCategoryUpdateDto dto)
    {
        if (dto.Id == Guid.Empty)
            throw new ArgumentException("Id is required.");

        var category = await _unitOfWork.Context.PathologyCategories
            .FirstOrDefaultAsync(pc => pc.Id == dto.Id);

        if (category == null)
            return null;

        var existingCode = await _unitOfWork.Context.PathologyCategories
            .AnyAsync(pc => pc.Code == dto.Code && pc.Id != dto.Id);

        if (existingCode)
            throw new InvalidOperationException($"PathologyCategory with code '{dto.Code}' already exists.");

        if (dto.BoneSpecialtyId.HasValue)
        {
            var boneSpecialty = await _unitOfWork.Context.BoneSpecialties
                .FirstOrDefaultAsync(bs => bs.Id == dto.BoneSpecialtyId.Value);

            if (boneSpecialty == null)
                throw new KeyNotFoundException("BoneSpecialty not found.");
        }

        category.Code = dto.Code;
        category.Name = dto.Name;
        category.BoneSpecialtyId = dto.BoneSpecialtyId;
        category.Description = dto.Description;
        category.DisplayOrder = dto.DisplayOrder;
        category.IsActive = dto.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Context.SaveChangesAsync();

        return (await GetByIdAsync(category.Id))!;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var category = await _unitOfWork.Context.PathologyCategories
            .FirstOrDefaultAsync(pc => pc.Id == id);

        if (category == null)
            return false;

        _unitOfWork.Context.PathologyCategories.Remove(category);
        await _unitOfWork.Context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ToggleActiveAsync(Guid id, bool isActive)
    {
        var category = await _unitOfWork.Context.PathologyCategories
            .FirstOrDefaultAsync(pc => pc.Id == id);

        if (category == null)
            return false;

        category.IsActive = isActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Context.SaveChangesAsync();

        return true;
    }

    private static PathologyCategoryDto MapToDto(PathologyCategory pc)
    {
        return new PathologyCategoryDto
        {
            Id = pc.Id,
            Code = pc.Code,
            Name = pc.Name,
            BoneSpecialtyId = pc.BoneSpecialtyId,
            BoneSpecialtyName = pc.BoneSpecialty?.Name,
            Description = pc.Description,
            DisplayOrder = pc.DisplayOrder,
            IsActive = pc.IsActive,
            CreatedAt = pc.CreatedAt,
            UpdatedAt = pc.UpdatedAt
        };
    }
}
