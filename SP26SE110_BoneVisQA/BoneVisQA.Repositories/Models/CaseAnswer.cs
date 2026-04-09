using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("case_answers")]
[Index("QuestionId", Name = "idx_case_answers_question")]
public partial class CaseAnswer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("question_id")]
    public Guid QuestionId { get; set; }

    [Column("answer_text")]
    public string? AnswerText { get; set; }

    [Column("structured_diagnosis")]
    public string? StructuredDiagnosis { get; set; }

    [Column("differential_diagnoses")]
    public string? DifferentialDiagnoses { get; set; }

    /// <summary>Key imaging signs / findings (SEPS structured learning output).</summary>
    [Column("key_imaging_findings")]
    public string? KeyImagingFindings { get; set; }

    /// <summary>Reflective questions for self-assessment (SEPS).</summary>
    [Column("reflective_questions")]
    public string? ReflectiveQuestions { get; set; }

    [Column("status")]
    public string Status { get; set; } = "RequiresLecturerReview";

    [Column("reviewed_by_id")]
    public Guid? ReviewedById { get; set; }

    [Column("escalated_by_id")]
    public Guid? EscalatedById { get; set; }

    [Column("generated_at")]
    public DateTime? GeneratedAt { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("escalated_at")]
    public DateTime? EscalatedAt { get; set; }

    [Column("ai_confidence_score")]
    public double? AiConfidenceScore { get; set; }

    [InverseProperty("Answer")]
    public virtual ICollection<Citation> Citations { get; set; } = new List<Citation>();

    [InverseProperty("Answer")]
    public virtual ICollection<ExpertReview> ExpertReviews { get; set; } = new List<ExpertReview>();

    [ForeignKey("QuestionId")]
    [InverseProperty("CaseAnswers")]
    public virtual StudentQuestion Question { get; set; } = null!;

    [ForeignKey("EscalatedById")]
    [InverseProperty("EscalatedCaseAnswers")]
    public virtual User? EscalatedBy { get; set; }

    [ForeignKey("ReviewedById")]
    [InverseProperty("CaseAnswers")]
    public virtual User? ReviewedBy { get; set; }
}
