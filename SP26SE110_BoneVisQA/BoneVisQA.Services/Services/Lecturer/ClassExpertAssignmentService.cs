using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.Lecturer;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Services.Services.Lecturer;

public class ClassExpertAssignmentService : IClassExpertAssignmentService
{
    private readonly IUnitOfWork _unitOfWork;

    public ClassExpertAssignmentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ClassExpertAssignmentDto>> GetByClassAsync(Guid classId)
    {
        var assignments = await _unitOfWork.Context.ClassExpertAssignments
            .Include(cea => cea.Class)
            .Include(cea => cea.Expert)
            .Include(cea => cea.BoneSpecialty)
            .Where(cea => cea.ClassId == classId && cea.IsActive)
            .OrderByDescending(cea => cea.AssignedAt)
            .ToListAsync();

        return assignments.Select(MapToDto).ToList();
    }

    public async Task<List<ClassExpertAssignmentDto>> GetByExpertAsync(Guid expertId)
    {
        var assignments = await _unitOfWork.Context.ClassExpertAssignments
            .Include(cea => cea.Class)
            .Include(cea => cea.Expert)
            .Include(cea => cea.BoneSpecialty)
            .Where(cea => cea.ExpertId == expertId && cea.IsActive)
            .OrderByDescending(cea => cea.AssignedAt)
            .ToListAsync();

        return assignments.Select(MapToDto).ToList();
    }

    public async Task<ClassExpertAssignmentDto?> GetByIdAsync(Guid id)
    {
        var assignment = await _unitOfWork.Context.ClassExpertAssignments
            .Include(cea => cea.Class)
            .Include(cea => cea.Expert)
            .Include(cea => cea.BoneSpecialty)
            .FirstOrDefaultAsync(cea => cea.Id == id);

        return assignment == null ? null : MapToDto(assignment);
    }

    public async Task<ClassExpertAssignmentDto> CreateAsync(ClassExpertAssignmentCreateDto dto)
    {
        // Validate Class exists
        var academicClass = await _unitOfWork.Context.AcademicClasses.FindAsync(dto.ClassId)
            ?? throw new KeyNotFoundException("Class not found.");

        // Validate Expert exists
        var expert = await _unitOfWork.Context.Users.FindAsync(dto.ExpertId)
            ?? throw new KeyNotFoundException("Expert not found.");

        // Validate BoneSpecialty exists
        var boneSpecialty = await _unitOfWork.Context.BoneSpecialties.FindAsync(dto.BoneSpecialtyId)
            ?? throw new KeyNotFoundException("Bone specialty not found.");

        // Check duplicate
        var existing = await _unitOfWork.Context.ClassExpertAssignments
            .FirstOrDefaultAsync(cea => cea.ClassId == dto.ClassId 
                && cea.ExpertId == dto.ExpertId 
                && cea.BoneSpecialtyId == dto.BoneSpecialtyId
                && cea.IsActive);

        if (existing != null)
            throw new InvalidOperationException("Expert is already assigned to this class with this specialty.");

        var assignment = new ClassExpertAssignment
        {
            Id = Guid.NewGuid(),
            ClassId = dto.ClassId,
            ExpertId = dto.ExpertId,
            BoneSpecialtyId = dto.BoneSpecialtyId,
            RoleInClass = dto.RoleInClass ?? "Supporting",
            AssignedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _unitOfWork.Context.ClassExpertAssignments.AddAsync(assignment);
        await _unitOfWork.SaveAsync();

        // Load navigation properties for response
        assignment.Class = academicClass;
        assignment.Expert = expert;
        assignment.BoneSpecialty = boneSpecialty;

        return MapToDto(assignment);
    }

    public async Task<ClassExpertAssignmentDto?> UpdateAsync(ClassExpertAssignmentUpdateDto dto)
    {
        var assignment = await _unitOfWork.Context.ClassExpertAssignments
            .Include(cea => cea.Class)
            .Include(cea => cea.Expert)
            .Include(cea => cea.BoneSpecialty)
            .FirstOrDefaultAsync(cea => cea.Id == dto.Id);

        if (assignment == null)
            return null;

        if (!string.IsNullOrEmpty(dto.RoleInClass))
            assignment.RoleInClass = dto.RoleInClass;

        if (dto.IsActive.HasValue)
            assignment.IsActive = dto.IsActive.Value;

        _unitOfWork.Context.Entry(assignment).State = EntityState.Modified;
        await _unitOfWork.SaveAsync();

        return MapToDto(assignment);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var assignment = await _unitOfWork.Context.ClassExpertAssignments
            .FirstOrDefaultAsync(cea => cea.Id == id);

        if (assignment == null)
            return false;

        // Soft delete
        assignment.IsActive = false;

        _unitOfWork.Context.Entry(assignment).State = EntityState.Modified;
        await _unitOfWork.SaveAsync();

        return true;
    }

    private static ClassExpertAssignmentDto MapToDto(ClassExpertAssignment assignment)
    {
        return new ClassExpertAssignmentDto
        {
            Id = assignment.Id,
            ClassId = assignment.ClassId,
            ClassName = assignment.Class?.ClassName,
            ExpertId = assignment.ExpertId,
            ExpertName = assignment.Expert?.FullName,
            BoneSpecialtyId = assignment.BoneSpecialtyId,
            BoneSpecialtyName = assignment.BoneSpecialty?.Name,
            RoleInClass = assignment.RoleInClass,
            AssignedAt = assignment.AssignedAt,
            IsActive = assignment.IsActive
        };
    }
}
