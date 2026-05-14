namespace BoneVisQA.Services.Models.Lecturer;

public class ClassExpertAssignmentDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public Guid ExpertId { get; set; }
    public string? ExpertName { get; set; }
    public Guid BoneSpecialtyId { get; set; }
    public string? BoneSpecialtyName { get; set; }
    public string RoleInClass { get; set; } = "Supporting";
    public DateTime? AssignedAt { get; set; }
    public bool IsActive { get; set; }
}

public class ClassExpertAssignmentCreateDto
{
    public Guid ClassId { get; set; }
    public Guid ExpertId { get; set; }
    public Guid BoneSpecialtyId { get; set; }
    public string? RoleInClass { get; set; } = "Supporting";
}

public class ClassExpertAssignmentUpdateDto
{
    public Guid Id { get; set; }
    public string? RoleInClass { get; set; }
    public bool? IsActive { get; set; }
}
