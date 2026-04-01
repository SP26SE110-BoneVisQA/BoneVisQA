using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("class_cases")]
public partial class ClassCase
{
    [Column("class_id")]
    public Guid ClassId { get; set; }

    [Column("case_id")]
    public Guid CaseId { get; set; }

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    [Column("is_mandatory")]
    public bool IsMandatory { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("ClassCases")]
    public virtual MedicalCase Case { get; set; } = null!;

    [ForeignKey("ClassId")]
    [InverseProperty("ClassCases")]
    public virtual AcademicClass Class { get; set; } = null!;
}
