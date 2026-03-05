using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("case_answers")]
public partial class CaseAnswer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("question_id")]
    public Guid QuestionId { get; set; }

    [Column("answer_text")]
    public string AnswerText { get; set; } = null!;

    [Column("structured_diagnosis")]
    public string? StructuredDiagnosis { get; set; }

    [Column("differential_diagnoses")]
    public string? DifferentialDiagnoses { get; set; }

    [Column("reviewed_by_id")]
    public Guid? ReviewedById { get; set; }

    [Column("generated_at")]
    public DateTime? GeneratedAt { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [InverseProperty("Answer")]
    public virtual ICollection<Citation> Citations { get; set; } = new List<Citation>();

    [InverseProperty("Answer")]
    public virtual ICollection<ExpertReview> ExpertReviews { get; set; } = new List<ExpertReview>();

    [ForeignKey("QuestionId")]
    [InverseProperty("CaseAnswers")]
    public virtual StudentQuestion Question { get; set; } = null!;

    [ForeignKey("ReviewedById")]
    [InverseProperty("CaseAnswers")]
    public virtual UserProfile? ReviewedBy { get; set; }
}
