using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("student_competencies")]
public class StudentCompetency
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("bone_specialty_id")]
    public Guid? BoneSpecialtyId { get; set; }

    [Column("pathology_category_id")]
    public Guid? PathologyCategoryId { get; set; }

    [Column("score")]
    public decimal Score { get; set; } = 0;

    [Column("total_attempts")]
    public int TotalAttempts { get; set; } = 0;

    [Column("correct_attempts")]
    public int CorrectAttempts { get; set; } = 0;

    [Column("mastery_level")]
    [MaxLength(50)]
    public string MasteryLevel { get; set; } = "Beginner";

    [Column("last_attempt_at")]
    public DateTime? LastAttemptAt { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("StudentId")]
    public virtual User? Student { get; set; }

    [ForeignKey("BoneSpecialtyId")]
    public virtual BoneSpecialty? BoneSpecialty { get; set; }

    [ForeignKey("PathologyCategoryId")]
    public virtual PathologyCategory? PathologyCategory { get; set; }
}

public enum MasteryLevel
{
    Beginner,
    Intermediate,
    Proficient,
    Expert
}
