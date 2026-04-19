using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("document_chunks")]
[Index("DocId", "ChunkOrder", Name = "document_chunks_doc_id_chunk_order_key", IsUnique = true)]
public partial class DocumentChunk
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("doc_id")]
    public Guid DocId { get; set; }

    [Column("content")]
    public string Content { get; set; } = null!;

    [Column("chunk_order")]
    public int ChunkOrder { get; set; }

    [Column("embedding", TypeName = "vector(768)")]
    public Pgvector.Vector? Embedding { get; set; }

    [Column("is_flagged")]
    public bool IsFlagged { get; set; } = false;

    [Column("flagged_by_expert_id")]
    public Guid? FlaggedByExpertId { get; set; }

    [Column("flag_reason")]
    public string? FlagReason { get; set; }

    [Column("flagged_at")]
    public DateTime? FlaggedAt { get; set; }

    [Column("start_page")]
    public int? StartPage { get; set; } = 0;

    [Column("end_page")]
    public int? EndPage { get; set; } = 0;

    [InverseProperty("Chunk")]
    public virtual ICollection<Citation> Citations { get; set; } = new List<Citation>();

    [ForeignKey("DocId")]
    [InverseProperty("DocumentChunks")]
    public virtual Document Doc { get; set; } = null!;

    [ForeignKey("FlaggedByExpertId")]
    [InverseProperty("FlaggedDocumentChunks")]
    public virtual User? FlaggedByExpert { get; set; }
}
