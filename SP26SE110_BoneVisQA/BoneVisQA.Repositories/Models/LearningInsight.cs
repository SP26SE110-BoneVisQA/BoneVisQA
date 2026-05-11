using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("learning_insights")]
public class LearningInsight
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("insight_type")]
    [MaxLength(50)]
    public string InsightType { get; set; } = string.Empty;

    [Column("title")]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("confidence")]
    public decimal Confidence { get; set; } = 0.5m;

    [Column("related_bone_specialty_id")]
    public Guid? RelatedBoneSpecialtyId { get; set; }

    [Column("related_pathology_id")]
    public Guid? RelatedPathologyId { get; set; }

    [Column("recommended_action")]
    public string? RecommendedAction { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; } = false;

    [Column("is_action_taken")]
    public bool IsActionTaken { get; set; } = false;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("StudentId")]
    public virtual User? Student { get; set; }

    [ForeignKey("RelatedBoneSpecialtyId")]
    public virtual BoneSpecialty? RelatedBoneSpecialty { get; set; }

    [ForeignKey("RelatedPathologyId")]
    public virtual PathologyCategory? RelatedPathology { get; set; }
}

public enum InsightType
{
    WeakTopic,
    Improvement,
    RecommendedAction,
    ErrorPattern
}
