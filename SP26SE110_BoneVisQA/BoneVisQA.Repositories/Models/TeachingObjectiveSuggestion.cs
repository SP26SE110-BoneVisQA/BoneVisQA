using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("teaching_objective_suggestions")]
[Index("ClassId", Name = "idx_teaching_objective_suggestions_class")]
[Index("ExpertId", Name = "idx_teaching_objective_suggestions_expert")]
[Index("Status", Name = "idx_teaching_objective_suggestions_status")]
public class TeachingObjectiveSuggestion
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("class_id")]
    public Guid ClassId { get; set; }

    [Column("expert_id")]
    public Guid ExpertId { get; set; }

    [Column("topic")]
    [MaxLength(200)]
    public string Topic { get; set; } = string.Empty;

    [Column("objective")]
    [MaxLength(500)]
    public string Objective { get; set; } = string.Empty;

    [Column("level")]
    [MaxLength(50)]
    public string Level { get; set; } = "Basic";

    [Column("order_index")]
    public int OrderIndex { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [Column("rejection_reason")]
    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    [Column("reviewed_by")]
    public Guid? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("ClassId")]
    [InverseProperty("TeachingObjectiveSuggestions")]
    public virtual AcademicClass Class { get; set; } = null!;

    [ForeignKey("ExpertId")]
    [InverseProperty("TeachingObjectiveSuggestions")]
    public virtual User Expert { get; set; } = null!;

    [ForeignKey("ReviewedBy")]
    [InverseProperty("ReviewedSuggestions")]
    public virtual User? Reviewer { get; set; }
}
