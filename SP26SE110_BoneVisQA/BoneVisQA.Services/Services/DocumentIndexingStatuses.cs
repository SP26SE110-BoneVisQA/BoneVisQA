namespace BoneVisQA.Services.Services;

/// <summary>Stored in <c>documents.indexing_status</c>.</summary>
public static class DocumentIndexingStatuses
{
    public const string Pending = "Pending";
    public const string Reindexing = "Reindexing";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
