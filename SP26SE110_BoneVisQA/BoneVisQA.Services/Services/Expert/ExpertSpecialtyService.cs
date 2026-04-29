using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BoneVisQA.Services.Services.Expert;

public class ExpertSpecialtyService : IExpertSpecialtyService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExpertSpecialtyService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ExpertSpecialtyDto>> GetMySpecialtiesAsync(Guid expertId)
    {
        var specialties = await _unitOfWork.Context.ExpertSpecialties
            .Include(es => es.BoneSpecialty)
            .Include(es => es.PathologyCategory)
            .Where(es => es.ExpertId == expertId && es.IsActive)
            .OrderByDescending(es => es.IsPrimary)
            .ThenByDescending(es => es.ProficiencyLevel)
            .ToListAsync();

        return specialties.Select(MapToDto).ToList();
    }

    public async Task<ExpertSpecialtyDto?> GetByIdAsync(Guid id)
    {
        var specialty = await _unitOfWork.Context.ExpertSpecialties
            .Include(es => es.Expert)
            .Include(es => es.BoneSpecialty)
            .Include(es => es.PathologyCategory)
            .FirstOrDefaultAsync(es => es.Id == id);

        return specialty == null ? null : MapToDto(specialty);
    }

    public async Task<ExpertSpecialtyDto> CreateAsync(Guid expertId, ExpertSpecialtyCreateDto dto)
    {
        // Validate BoneSpecialty exists
        var boneSpecialty = await _unitOfWork.Context.BoneSpecialties.FindAsync(dto.BoneSpecialtyId)
            ?? throw new KeyNotFoundException("Bone specialty not found.");

        // Validate PathologyCategory if provided
        PathologyCategory? pathologyCategory = null;
        if (dto.PathologyCategoryId.HasValue)
        {
            pathologyCategory = await _unitOfWork.Context.PathologyCategories.FindAsync(dto.PathologyCategoryId.Value)
                ?? throw new KeyNotFoundException("Pathology category not found.");
        }

        // Check duplicate
        var existing = await _unitOfWork.Context.ExpertSpecialties
            .FirstOrDefaultAsync(es => es.ExpertId == expertId 
                && es.BoneSpecialtyId == dto.BoneSpecialtyId 
                && es.PathologyCategoryId == dto.PathologyCategoryId
                && es.IsActive);

        if (existing != null)
            throw new InvalidOperationException("Expert already has this specialty.");

        // If setting as primary, unset other primaries
        if (dto.IsPrimary)
        {
            var currentPrimaries = await _unitOfWork.Context.ExpertSpecialties
                .Where(es => es.ExpertId == expertId && es.IsPrimary && es.IsActive)
                .ToListAsync();

            foreach (var primary in currentPrimaries)
            {
                primary.IsPrimary = false;
            }
        }

        var specialty = new ExpertSpecialty
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            BoneSpecialtyId = dto.BoneSpecialtyId,
            PathologyCategoryId = dto.PathologyCategoryId,
            ProficiencyLevel = dto.ProficiencyLevel,
            YearsExperience = dto.YearsExperience,
            Certifications = dto.Certifications,
            IsPrimary = dto.IsPrimary,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Context.ExpertSpecialties.AddAsync(specialty);
        await _unitOfWork.SaveAsync();

        // Load navigation properties for response
        specialty.Expert = await _unitOfWork.Context.Users.FindAsync(expertId) ?? new User();
        specialty.BoneSpecialty = boneSpecialty;
        specialty.PathologyCategory = pathologyCategory;

        return MapToDto(specialty);
    }

    public async Task<ExpertSpecialtyDto?> UpdateAsync(Guid expertId, ExpertSpecialtyUpdateDto dto)
    {
        var specialty = await _unitOfWork.Context.ExpertSpecialties
            .Include(es => es.BoneSpecialty)
            .Include(es => es.PathologyCategory)
            .FirstOrDefaultAsync(es => es.Id == dto.Id && es.ExpertId == expertId);

        if (specialty == null)
            return null;

        if (dto.BoneSpecialtyId.HasValue)
        {
            var boneSpecialty = await _unitOfWork.Context.BoneSpecialties.FindAsync(dto.BoneSpecialtyId.Value)
                ?? throw new KeyNotFoundException("Bone specialty not found.");
            specialty.BoneSpecialtyId = dto.BoneSpecialtyId.Value;
            specialty.BoneSpecialty = boneSpecialty;
        }

        if (dto.PathologyCategoryId.HasValue)
        {
            var pathologyCategory = await _unitOfWork.Context.PathologyCategories.FindAsync(dto.PathologyCategoryId.Value)
                ?? throw new KeyNotFoundException("Pathology category not found.");
            specialty.PathologyCategoryId = dto.PathologyCategoryId;
            specialty.PathologyCategory = pathologyCategory;
        }

        if (dto.ProficiencyLevel.HasValue)
            specialty.ProficiencyLevel = dto.ProficiencyLevel.Value;

        if (dto.YearsExperience.HasValue)
            specialty.YearsExperience = dto.YearsExperience;

        if (dto.Certifications != null)
            specialty.Certifications = dto.Certifications;

        if (dto.IsPrimary.HasValue && dto.IsPrimary.Value)
        {
            // Unset other primaries
            var currentPrimaries = await _unitOfWork.Context.ExpertSpecialties
                .Where(es => es.ExpertId == expertId && es.IsPrimary && es.IsActive && es.Id != dto.Id)
                .ToListAsync();

            foreach (var primary in currentPrimaries)
            {
                primary.IsPrimary = false;
            }
            specialty.IsPrimary = true;
        }
        else if (dto.IsPrimary.HasValue)
        {
            specialty.IsPrimary = false;
        }

        specialty.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Context.Entry(specialty).State = EntityState.Modified;
        await _unitOfWork.SaveAsync();

        return MapToDto(specialty);
    }

    public async Task<bool> DeleteAsync(Guid expertId, Guid id)
    {
        var specialty = await _unitOfWork.Context.ExpertSpecialties
            .FirstOrDefaultAsync(es => es.Id == id && es.ExpertId == expertId);

        if (specialty == null)
            return false;

        // Soft delete
        specialty.IsActive = false;
        specialty.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Context.Entry(specialty).State = EntityState.Modified;
        await _unitOfWork.SaveAsync();

        return true;
    }

    public async Task<List<ExpertSuggestionDto>> GetSuggestedExpertsAsync(Guid boneSpecialtyId, Guid? pathologyCategoryId = null)
    {
        var query = _unitOfWork.Context.ExpertSpecialties
            .Include(es => es.Expert)
            .Include(es => es.BoneSpecialty)
            .Where(es => es.BoneSpecialtyId == boneSpecialtyId && es.IsActive)
            .OrderByDescending(es => es.IsPrimary)
            .ThenByDescending(es => es.ProficiencyLevel)
            .ThenByDescending(es => es.YearsExperience);

        // Filter by pathology if provided
        if (pathologyCategoryId.HasValue)
        {
            query = (IOrderedQueryable<ExpertSpecialty>)query.Where(es => 
                es.PathologyCategoryId == pathologyCategoryId || es.PathologyCategoryId == null);
        }

        var experts = await query.ToListAsync();

        return experts.Select(e => new ExpertSuggestionDto
        {
            ExpertId = e.ExpertId,
            ExpertName = e.Expert?.FullName,
            ExpertEmail = e.Expert?.Email,
            BoneSpecialtyId = e.BoneSpecialtyId,
            BoneSpecialtyName = e.BoneSpecialty?.Name,
            ProficiencyLevel = e.ProficiencyLevel,
            YearsExperience = e.YearsExperience,
            Certifications = e.Certifications
        }).ToList();
    }

    private static ExpertSpecialtyDto MapToDto(ExpertSpecialty specialty)
    {
        return new ExpertSpecialtyDto
        {
            Id = specialty.Id,
            ExpertId = specialty.ExpertId,
            ExpertName = specialty.Expert?.FullName,
            BoneSpecialtyId = specialty.BoneSpecialtyId,
            BoneSpecialtyName = specialty.BoneSpecialty?.Name,
            PathologyCategoryId = specialty.PathologyCategoryId,
            PathologyCategoryName = specialty.PathologyCategory?.Name,
            ProficiencyLevel = specialty.ProficiencyLevel,
            YearsExperience = specialty.YearsExperience,
            Certifications = specialty.Certifications,
            IsPrimary = specialty.IsPrimary,
            IsActive = specialty.IsActive,
            CreatedAt = specialty.CreatedAt,
            UpdatedAt = specialty.UpdatedAt
        };
    }
}
