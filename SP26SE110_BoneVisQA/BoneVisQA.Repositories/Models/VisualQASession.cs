using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("visual_qa_sessions")]
[Index("StudentId", Name = "idx_visual_qa_sessions_student")]
[Index("CaseId", Name = "idx_visual_qa_sessions_case")]
[Index("Status", Name = "idx_visual_qa_sessions_status")]
public partial class VisualQASession
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("case_id")]
    public Guid? CaseId { get; set; }

    [Column("image_id")]
    public Guid? ImageId { get; set; }

    [Column("custom_image_url")]
    public string? CustomImageUrl { get; set; }

    [Column("status")]
    [MaxLength(40)]
    public string Status { get; set; } = "Active";

    [Column("lecturer_id")]
    public Guid? LecturerId { get; set; }

    [Column("expert_id")]
    public Guid? ExpertId { get; set; }

    [Column("promoted_case_id")]
    public Guid? PromotedCaseId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("VisualQASessions")]
    public virtual User Student { get; set; } = null!;

    [ForeignKey("CaseId")]
    [InverseProperty("VisualQASessions")]
    public virtual MedicalCase? Case { get; set; }

    [ForeignKey("ImageId")]
    [InverseProperty("VisualQASessions")]
    public virtual MedicalImage? Image { get; set; }

    [InverseProperty("Session")]
    public virtual ICollection<QAMessage> Messages { get; set; } = new List<QAMessage>();

    [InverseProperty("Session")]
    public virtual ICollection<ExpertReview> ExpertReviews { get; set; } = new List<ExpertReview>();
}
