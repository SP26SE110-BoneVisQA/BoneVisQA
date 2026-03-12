using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[PrimaryKey("ClassId", "TagId")]
[Table("class_tags")]
[Index("ClassId", Name = "idx_class_tags_class")]
[Index("TagId", Name = "idx_class_tags_tag")]
public partial class ClassTag
{
    [Key]
    [Column("class_id")]
    public Guid ClassId { get; set; }

    [Key]
    [Column("tag_id")]
    public Guid TagId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("ClassId")]
    [InverseProperty("ClassTags")]
    public virtual AcademicClass Class { get; set; } = null!;

    [ForeignKey("TagId")]
    [InverseProperty("ClassTags")]
    public virtual Tag Tag { get; set; } = null!;
}
