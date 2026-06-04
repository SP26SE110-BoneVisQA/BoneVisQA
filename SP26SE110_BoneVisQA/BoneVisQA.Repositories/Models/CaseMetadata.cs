using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("case_metadata")]
public partial class CaseMetadata
{
    [Key]
    [Column("case_id")]
    public Guid CaseId { get; set; }

    [Column("modality")]
    public string Modality { get; set; } = "Other";

    [Column("anatomy")]
    public string Anatomy { get; set; } = "Other";

    [Column("pathology_group")]
    public string PathologyGroup { get; set; } = "Other";

    [Column("bone_specialty_id")]
    public Guid? BoneSpecialtyId { get; set; }

    [Column("pathology_category_id")]
    public Guid? PathologyCategoryId { get; set; }

    [Column("suggested_diagnosis")]
    public string? SuggestedDiagnosis { get; set; }

    [Column("clinical_context", TypeName = "jsonb")]
    public string? ClinicalContext { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("anatomy_site")]
    public string? AnatomySite { get; set; }

    [Column("laterality")]
    public string? Laterality { get; set; }

    [Column("view_position")]
    public string? ViewPosition { get; set; }

    [Column("difficulty")]
    public string? Difficulty { get; set; }

    [Column("source_type")]
    public string? SourceType { get; set; }

    [Column("quality_score")]
    public double? QualityScore { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("CaseMetadata")]
    public virtual MedicalCase Case { get; set; } = null!;
}
