using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("case_view_logs")]
[Index("StudentId", "CaseId", Name = "idx_case_view_logs_student_case")]
public partial class CaseViewLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("case_id")]
    public Guid CaseId { get; set; }

    [Column("class_id")]
    public Guid? ClassId { get; set; }

    [Column("viewed_at")]
    public DateTime? ViewedAt { get; set; }

    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    [Column("is_completed")]
    public bool? IsCompleted { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("CaseViewLogs")]
    public virtual MedicalCase Case { get; set; } = null!;

    [ForeignKey("StudentId")]
    [InverseProperty("CaseViewLogs")]
    public virtual User Student { get; set; } = null!;

    [ForeignKey("ClassId")]
    [InverseProperty("CaseViewLogs")]
    public virtual AcademicClass? Class { get; set; }
}
