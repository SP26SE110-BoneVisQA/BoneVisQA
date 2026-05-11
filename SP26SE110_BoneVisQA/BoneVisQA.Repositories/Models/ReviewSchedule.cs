using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("review_schedules")]
public class ReviewSchedule
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("case_id")]
    public Guid? CaseId { get; set; }

    [Column("quiz_id")]
    public Guid? QuizId { get; set; }

    [Column("question_id")]
    public Guid? QuestionId { get; set; }

    [Column("next_review_date")]
    public DateOnly NextReviewDate { get; set; }

    [Column("ease_factor")]
    public decimal EaseFactor { get; set; } = 2.5m;

    [Column("interval_days")]
    public int IntervalDays { get; set; } = 1;

    [Column("repetition_count")]
    public int RepetitionCount { get; set; } = 0;

    [Column("last_review_date")]
    public DateTime? LastReviewDate { get; set; }

    [Column("last_quality")]
    public int LastQuality { get; set; } = -1;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("StudentId")]
    public virtual User? Student { get; set; }

    [ForeignKey("CaseId")]
    public virtual MedicalCase? Case { get; set; }

    [ForeignKey("QuizId")]
    public virtual Quiz? Quiz { get; set; }

    [ForeignKey("QuestionId")]
    public virtual QuizQuestion? Question { get; set; }
}
