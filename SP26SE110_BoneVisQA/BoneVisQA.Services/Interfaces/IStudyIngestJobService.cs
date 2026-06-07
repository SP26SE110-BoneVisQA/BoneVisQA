using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Interfaces;

public interface IStudyIngestJobService
{
    Task<Guid> QueueIngestAsync(
        string ingestReferenceUrl,
        string bucket,
        string stagingObjectPath,
        string ingestPurpose,
        Guid? ownerUserId,
        string? diagnosisText,
        StudyIngestJobKind kind,
        CancellationToken cancellationToken = default);

    StudyIngestJobStatusDto? GetJob(Guid jobId);
}
