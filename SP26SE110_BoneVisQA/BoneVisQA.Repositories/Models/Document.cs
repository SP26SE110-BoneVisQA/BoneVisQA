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

    [Column("pending_reindex_path")]
    public string? PendingReindexPath { get; set; }

    [Column("pending_reindex_hash")]
    [StringLength(64)]
    public string? PendingReindexHash { get; set; }

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Semantic version Major.Minor.Patch (e.g. 1.2.0).</summary>
    [Column("version")]
    [StringLength(32)]
    public string Version { get; set; } = "1.0.0";

    /// <summary>Version string to apply when indexing completes successfully (cleared after swap).</summary>
    [Column("pending_target_version")]
    [StringLength(32)]
    public string? PendingTargetVersion { get; set; }

    [Column("total_pages")]
    public int TotalPages { get; set; } = 0;

    [Column("current_page_indexing")]
    public int CurrentPageIndexing { get; set; }

    [Column("total_chunks")]
    public int TotalChunks { get; set; }

    [Column("is_outdated")]
    public bool IsOutdated { get; set; } = false;

    [Column("indexing_status")]
    public string IndexingStatus { get; set; } = "Pending";

    [Column("indexing_progress")]
    public int IndexingProgress { get; set; } = 0;

    [Column("content_hash")]
    [StringLength(64)]
    public string? ContentHash { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("Documents")]
    public virtual Category? Category { get; set; }

    [InverseProperty("Doc")]
    public virtual ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();

    [InverseProperty("Document")]
    public virtual ICollection<DocumentTag> DocumentTags { get; set; } = new List<DocumentTag>();

}
