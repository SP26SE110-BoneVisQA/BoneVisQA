using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("bone_specialties")]
[Index("Code", Name = "idx_bone_specialties_code", IsUnique = true)]
[Index("ParentId", Name = "idx_bone_specialties_parent")]
public partial class BoneSpecialty
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("code")]
    [Required]
    [MaxLength(100)]
    public string Code { get; set; } = null!;

    [Column("name")]
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = null!;

    [Column("parent_id")]
    public Guid? ParentId { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; } = 0;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("ParentId")]
    [InverseProperty("Children")]
    public virtual BoneSpecialty? Parent { get; set; }

    [InverseProperty("Parent")]
    public virtual ICollection<BoneSpecialty> Children { get; set; } = new List<BoneSpecialty>();

    [InverseProperty("BoneSpecialty")]
    public virtual ICollection<PathologyCategory> PathologyCategories { get; set; } = new List<PathologyCategory>();

    [InverseProperty("BoneSpecialty")]
    public virtual ICollection<ExpertSpecialty> ExpertSpecialties { get; set; } = new List<ExpertSpecialty>();

    [InverseProperty("BoneSpecialty")]
    public virtual ICollection<ClassExpertAssignment> ClassExpertAssignments { get; set; } = new List<ClassExpertAssignment>();

    [InverseProperty("BoneSpecialty")]
    public virtual ICollection<MedicalCase> MedicalCases { get; set; } = new List<MedicalCase>();

    [InverseProperty("BoneSpecialty")]
    public virtual ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();

    [InverseProperty("PrimaryBoneSpecialty")]
    public virtual ICollection<User> UsersWithPrimarySpecialty { get; set; } = new List<User>();

    [InverseProperty("ClassSpecialty")]
    public virtual ICollection<AcademicClass> AcademicClasses { get; set; } = new List<AcademicClass>();
}
