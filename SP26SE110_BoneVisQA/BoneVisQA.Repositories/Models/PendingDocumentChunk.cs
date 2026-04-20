using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoneVisQA.Repositories.Models;

[Table("pending_document_chunks")]
[Index("DocId", "ChunkOrder", Name = "pending_document_chunks_doc_id_chunk_order_key", IsUnique = true)]
public class PendingDocumentChunk
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

    [Column("start_page")]
    public int StartPage { get; set; }

    [Column("end_page")]
    public int EndPage { get; set; }

    [Column("embedding", TypeName = "vector(768)")]
    public Pgvector.Vector? Embedding { get; set; }
}
