using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BoneVisQA.Repositories.Models;

[Table("citations")]
[Index("AnswerId", Name = "idx_citations_answer")]
public partial class Citation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("answer_id")]
    public Guid AnswerId { get; set; }

    [Column("chunk_id")]
    public Guid ChunkId { get; set; }

    [Column("similarity_score")]
    public double SimilarityScore { get; set; }

    [NotMapped]
    public string? ReferenceUrl { get; set; }

    [NotMapped]
    public int? PageNumber { get; set; }

    [ForeignKey("AnswerId")]
    [InverseProperty("Citations")]
    public virtual CaseAnswer Answer { get; set; } = null!;

    [ForeignKey("ChunkId")]
    [InverseProperty("Citations")]
    public virtual DocumentChunk Chunk { get; set; } = null!;
}
