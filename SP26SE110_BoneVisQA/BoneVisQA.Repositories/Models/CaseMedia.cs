using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("case_media")]
public partial class CaseMedia
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("case_id")]
    public Guid CaseId { get; set; }

    [Column("media_url")]
    public string MediaUrl { get; set; } = null!;

    [Column("storage_path")]
    public string? StoragePath { get; set; }

    [Column("media_type")]
    public string MediaType { get; set; } = "Image";

    [Column("modality")]
    public string Modality { get; set; } = "Other";

    [Column("anatomy")]
    public string Anatomy { get; set; } = "Other";

    [Column("dicom_metadata", TypeName = "jsonb")]
    public string? DicomMetadata { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("CaseMedia")]
    public virtual MedicalCase Case { get; set; } = null!;
}
