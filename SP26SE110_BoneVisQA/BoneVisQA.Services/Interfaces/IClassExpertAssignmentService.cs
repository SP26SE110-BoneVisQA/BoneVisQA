using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Interfaces;

public interface IClassExpertAssignmentService
{
    Task<List<ClassExpertAssignmentDto>> GetByClassAsync(Guid classId);
    Task<List<ClassExpertAssignmentDto>> GetByExpertAsync(Guid expertId);
    Task<ClassExpertAssignmentDto?> GetByIdAsync(Guid id);
    Task<ClassExpertAssignmentDto> CreateAsync(ClassExpertAssignmentCreateDto dto);
    Task<ClassExpertAssignmentDto?> UpdateAsync(ClassExpertAssignmentUpdateDto dto);
    Task<bool> DeleteAsync(Guid id);
}
