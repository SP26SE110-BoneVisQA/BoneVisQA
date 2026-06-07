using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Interfaces;

public interface IStudyIngestJobService
{
    /// <summary>Queue ingest from a local staged archive (Supabase upload + Python run in background).</summary>
    Task<Guid> QueueLocalArchiveAsync(
        string localArchivePath,
        string ingestPurpose,
        Guid? ownerUserId,
        string? diagnosisText,
        StudyIngestJobKind kind,
        CancellationToken cancellationToken = default);

    StudyIngestJobStatusDto? GetJob(Guid jobId);
}
