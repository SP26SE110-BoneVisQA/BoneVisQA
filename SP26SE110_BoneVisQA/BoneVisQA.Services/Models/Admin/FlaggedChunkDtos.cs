namespace BoneVisQA.Services.Models.Admin;

/// <summary>List item for expert-flagged RAG chunks (admin triage).</summary>
public class AdminFlaggedChunkListItemDto
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    /// <summary>Short excerpt for tables (full chunk text may be long).</summary>
    public string? Preview { get; set; }
    /// <summary>Same as <see cref="Preview"/> (FE alias).</summary>
    public string? Snippet { get; set; }
    public string? Reason { get; set; }
    public DateTime? FlaggedAt { get; set; }
    public Guid? FlaggedByExpertId { get; set; }
    /// <summary>Expert display name (FE: <c>flaggedBy</c>).</summary>
    public string? FlaggedBy { get; set; }
}
