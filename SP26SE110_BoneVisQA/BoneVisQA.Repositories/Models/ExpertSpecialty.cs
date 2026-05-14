using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("expert_specialties")]
[Index("ExpertId", Name = "idx_expert_specialties_expert")]
[Index("BoneSpecialtyId", Name = "idx_expert_specialties_bone")]
public partial class ExpertSpecialty
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("expert_id")]
    public Guid ExpertId { get; set; }

    [Column("bone_specialty_id")]
    public Guid BoneSpecialtyId { get; set; }

    [Column("pathology_category_id")]
    public Guid? PathologyCategoryId { get; set; }

    [Column("proficiency_level")]
    [Range(1, 5)]
    public int ProficiencyLevel { get; set; } = 1;

    [Column("years_experience")]
    public int? YearsExperience { get; set; }

    [Column("certifications", TypeName = "jsonb")]
    public string? Certifications { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; } = false;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("ExpertId")]
    [InverseProperty("ExpertSpecialties")]
    public virtual User Expert { get; set; } = null!;

    [ForeignKey("BoneSpecialtyId")]
    [InverseProperty("ExpertSpecialties")]
    public virtual BoneSpecialty BoneSpecialty { get; set; } = null!;

    [ForeignKey("PathologyCategoryId")]
    [InverseProperty("ExpertSpecialties")]
    public virtual PathologyCategory? PathologyCategory { get; set; }

    public virtual ICollection<ClassExpertAssignment> ClassExpertAssignments { get; set; } = new List<ClassExpertAssignment>();
}
