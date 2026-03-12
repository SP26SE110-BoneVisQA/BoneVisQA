using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

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

    [InverseProperty("Chunk")]
    public virtual ICollection<Citation> Citations { get; set; } = new List<Citation>();

    [ForeignKey("DocId")]
    [InverseProperty("DocumentChunks")]
    public virtual Document Doc { get; set; } = null!;
}
