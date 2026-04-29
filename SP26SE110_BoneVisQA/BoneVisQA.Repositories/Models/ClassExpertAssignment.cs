using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("class_expert_assignments")]
[Index("ClassId", Name = "idx_class_expert_assignments_class")]
[Index("ExpertId", Name = "idx_class_expert_assignments_expert")]
public partial class ClassExpertAssignment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("class_id")]
    public Guid ClassId { get; set; }

    [Column("expert_id")]
    public Guid ExpertId { get; set; }

    [Column("bone_specialty_id")]
    public Guid BoneSpecialtyId { get; set; }

    [Column("role_in_class")]
    [MaxLength(50)]
    public string RoleInClass { get; set; } = "Supporting";

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [ForeignKey("ClassId")]
    [InverseProperty("ClassExpertAssignments")]
    public virtual AcademicClass Class { get; set; } = null!;

    [ForeignKey("ExpertId")]
    [InverseProperty("ExpertClassAssignments")]
    public virtual User Expert { get; set; } = null!;

    [ForeignKey("BoneSpecialtyId")]
    [InverseProperty("ClassExpertAssignments")]
    public virtual BoneSpecialty BoneSpecialty { get; set; } = null!;
}
