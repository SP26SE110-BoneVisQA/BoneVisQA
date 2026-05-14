namespace BoneVisQA.Services.Models.Expert;

public class ExpertSpecialtyDto
{
    public Guid Id { get; set; }
    public Guid ExpertId { get; set; }
    public string? ExpertName { get; set; }
    public string? ExpertEmail { get; set; }
    public Guid BoneSpecialtyId { get; set; }
    public string? BoneSpecialtyName { get; set; }
    public string? BoneSpecialtyCode { get; set; }
    public Guid? PathologyCategoryId { get; set; }
    public string? PathologyCategoryName { get; set; }
    public int ProficiencyLevel { get; set; }
    public int? YearsExperience { get; set; }
    public string? Certifications { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ExpertSpecialtyCreateDto
{
    public Guid BoneSpecialtyId { get; set; }
    public Guid? PathologyCategoryId { get; set; }
    public int ProficiencyLevel { get; set; } = 1;
    public int? YearsExperience { get; set; }
    public string? Certifications { get; set; }
    public bool IsPrimary { get; set; } = false;
}

public class ExpertSpecialtyUpdateDto
{
    public Guid Id { get; set; }
    public Guid? BoneSpecialtyId { get; set; }
    public Guid? PathologyCategoryId { get; set; }
    public int? ProficiencyLevel { get; set; }
    public int? YearsExperience { get; set; }
    public string? Certifications { get; set; }
    public bool? IsPrimary { get; set; }
}

public class ExpertSuggestionDto
{
    public Guid ExpertId { get; set; }
    public string? ExpertName { get; set; }
    public string? ExpertEmail { get; set; }
    public Guid BoneSpecialtyId { get; set; }
    public string? BoneSpecialtyName { get; set; }
    public int ProficiencyLevel { get; set; }
    public int? YearsExperience { get; set; }
    public string? Certifications { get; set; }
}
