using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[PrimaryKey("DocumentId", "TagId")]
[Table("document_tags")]
[Index("DocumentId", Name = "idx_document_tags_document")]
[Index("TagId", Name = "idx_document_tags_tag")]
public partial class DocumentTag
{
    [Key]
    [Column("document_id")]
    public Guid DocumentId { get; set; }

    [Key]
    [Column("tag_id")]
    public Guid TagId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("DocumentId")]
    [InverseProperty("DocumentTags")]
    public virtual Document Document { get; set; } = null!;

    [ForeignKey("TagId")]
    [InverseProperty("DocumentTags")]
    public virtual Tag Tag { get; set; } = null!;
}
