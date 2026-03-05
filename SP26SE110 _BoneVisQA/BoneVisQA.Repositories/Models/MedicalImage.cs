using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("medical_images")]
public partial class MedicalImage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("case_id")]
    public Guid CaseId { get; set; }

    [Column("image_url")]
    public string ImageUrl { get; set; } = null!;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("CaseId")]
    [InverseProperty("MedicalImages")]
    public virtual MedicalCase Case { get; set; } = null!;

    [InverseProperty("Image")]
    public virtual ICollection<CaseAnnotation> CaseAnnotations { get; set; } = new List<CaseAnnotation>();
}
