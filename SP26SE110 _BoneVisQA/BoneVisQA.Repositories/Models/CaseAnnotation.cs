using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("case_annotations")]
public partial class CaseAnnotation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("image_id")]
    public Guid ImageId { get; set; }

    [Column("coordinates", TypeName = "jsonb")]
    public string Coordinates { get; set; } = null!;

    [Column("label")]
    public string Label { get; set; } = null!;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("ImageId")]
    [InverseProperty("CaseAnnotations")]
    public virtual MedicalImage Image { get; set; } = null!;

    [InverseProperty("Annotation")]
    public virtual ICollection<StudentQuestion> StudentQuestions { get; set; } = new List<StudentQuestion>();
}
