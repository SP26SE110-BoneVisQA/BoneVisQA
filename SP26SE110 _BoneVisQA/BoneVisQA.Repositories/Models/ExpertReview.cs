using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("expert_reviews")]
[Index("ExpertId", "AnswerId", Name = "expert_reviews_expert_id_answer_id_key", IsUnique = true)]
public partial class ExpertReview
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("expert_id")]
    public Guid ExpertId { get; set; }

    [Column("answer_id")]
    public Guid AnswerId { get; set; }

    [Column("review_note")]
    public string? ReviewNote { get; set; }

    [Column("action")]
    public string? Action { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("AnswerId")]
    [InverseProperty("ExpertReviews")]
    public virtual CaseAnswer Answer { get; set; } = null!;

    [ForeignKey("ExpertId")]
    [InverseProperty("ExpertReviews")]
    public virtual User Expert { get; set; } = null!;
}
