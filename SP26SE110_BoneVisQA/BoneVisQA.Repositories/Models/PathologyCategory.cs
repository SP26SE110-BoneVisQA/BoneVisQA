using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("pathology_categories")]
[Index("Code", Name = "idx_pathology_categories_code", IsUnique = true)]
[Index("BoneSpecialtyId", Name = "idx_pathology_categories_bone")]
public partial class PathologyCategory
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

    [Column("bone_specialty_id")]
    public Guid? BoneSpecialtyId { get; set; }

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

    [ForeignKey("BoneSpecialtyId")]
    [InverseProperty("PathologyCategories")]
    public virtual BoneSpecialty? BoneSpecialty { get; set; }

    [InverseProperty("PathologyCategory")]
    public virtual ICollection<ExpertSpecialty> ExpertSpecialties { get; set; } = new List<ExpertSpecialty>();

    [InverseProperty("PathologyCategory")]
    public virtual ICollection<MedicalCase> MedicalCases { get; set; } = new List<MedicalCase>();

    [InverseProperty("PathologyCategory")]
    public virtual ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
}
