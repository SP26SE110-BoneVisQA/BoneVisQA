using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[PrimaryKey("CaseId", "TagId")]
[Table("case_tags")]
[Index("CaseId", Name = "idx_case_tags_case")]
[Index("TagId", Name = "idx_case_tags_tag")]
public partial class CaseTag
{
    [Key]
    [Column("case_id")]
    public Guid CaseId { get; set; }

    [Key]
    [Column("tag_id")]
    public Guid TagId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("CaseTags")]
    public virtual MedicalCase Case { get; set; } = null!;

    [ForeignKey("TagId")]
    [InverseProperty("CaseTags")]
    public virtual Tag Tag { get; set; } = null!;
}