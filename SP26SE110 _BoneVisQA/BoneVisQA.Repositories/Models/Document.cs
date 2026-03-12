using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("documents")]
public partial class Document
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("file_path")]
    public string? FilePath { get; set; }

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
   
    [Column("version")]
    public int Version { get; set; } = 1;

    [Column("is_outdated")]
    public bool IsOutdated { get; set; } = false;

    [Column("version")]
    public int Version { get; set; }

    [Column("is_outdated")]
    public bool IsOutdated { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("Documents")]
    public virtual Category? Category { get; set; }

    [InverseProperty("Doc")]
    public virtual ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();

    [InverseProperty("Document")]
    public virtual ICollection<DocumentTag> DocumentTags { get; set; } = new List<DocumentTag>();

}
