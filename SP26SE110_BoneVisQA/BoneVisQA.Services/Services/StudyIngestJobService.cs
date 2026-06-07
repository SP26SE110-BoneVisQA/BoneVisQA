using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

/// <summary>
/// Runs Python DICOM ingest off the HTTP request thread so Render does not 502 on long cold-start model loads.
/// </summary>
public sealed class StudyIngestJobService : IStudyIngestJobService
{
    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(2);
    private const string CachePrefix = "study-ingest-job:";

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StudyIngestJobService> _logger;

    public StudyIngestJobService(
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        ILogger<StudyIngestJobService> logger)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task<Guid> QueueIngestAsync(
        string ingestReferenceUrl,
        string bucket,
        string stagingObjectPath,
        string ingestPurpose,
        Guid? ownerUserId,
        string? diagnosisText,
        StudyIngestJobKind kind,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var jobId = Guid.NewGuid();
        var job = new StudyIngestJobStatusDto
        {
            JobId = jobId,
            Status = "processing",
            Kind = kind,
            IngestOk = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _cache.Set(CacheKey(jobId), job, JobTtl);

        _ = Task.Run(() => RunIngestJobAsync(
            jobId,
            ingestReferenceUrl,
            bucket,
            stagingObjectPath,
            ingestPurpose,
            ownerUserId,
            diagnosisText,
            kind));

        return Task.FromResult(jobId);
    }

    public StudyIngestJobStatusDto? GetJob(Guid jobId) =>
        _cache.TryGetValue(CacheKey(jobId), out StudyIngestJobStatusDto? job) ? job : null;

    private async Task RunIngestJobAsync(
        Guid jobId,
        string ingestReferenceUrl,
        string bucket,
        string stagingObjectPath,
        string ingestPurpose,
        Guid? ownerUserId,
        string? diagnosisText,
        StudyIngestJobKind kind)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var pythonAi = scope.ServiceProvider.GetRequiredService<IPythonAiConnectorService>();
            var storage = scope.ServiceProvider.GetRequiredService<ISupabaseStorageService>();

            var ingest = await pythonAi.TriggerIngestAsync(
                ingestReferenceUrl,
                diagnosis: diagnosisText ?? string.Empty,
                ingestPurpose: ingestPurpose,
                ownerUserId: ownerUserId);

            try
            {
                await storage.DeleteFileAsync(bucket, stagingObjectPath, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Study ingest job {JobId}: staging cleanup failed.", jobId);
            }

            if (!ingest.Success || !ingest.CaseId.HasValue)
            {
                UpdateJob(jobId, job =>
                {
                    job.Status = "failed";
                    job.IngestOk = false;
                    job.IngestError = StudyArchiveIngestHelper.ResolveIngestErrorMessage(ingest);
                    job.CompletedAtUtc = DateTime.UtcNow;
                });
                return;
            }

            Guid? sessionId = null;
            if (kind == StudyIngestJobKind.StudentPersonal && ownerUserId.HasValue)
            {
                var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();
                sessionId = await studentService.CreateOrGetVisualQaSessionAsync(
                    ownerUserId.Value,
                    new VisualQARequestDto
                    {
                        CaseId = ingest.CaseId,
                        ImageUrl = ingest.PreviewImageUrl,
                        ImageId = ingest.CatalogImageId,
                        DicomMetadata = ingest.DicomMetadata,
                    });
            }

            UpdateJob(jobId, job =>
            {
                job.Status = "completed";
                job.IngestOk = true;
                job.CaseId = ingest.CaseId;
                job.SessionId = sessionId;
                job.MediaId = ingest.MediaId;
                job.CatalogImageId = ingest.CatalogImageId;
                job.PreviewImageUrl = ingest.PreviewImageUrl;
                job.DicomMetadata = ingest.DicomMetadata;
                job.CompletedAtUtc = DateTime.UtcNow;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Study ingest job {JobId} failed.", jobId);
            UpdateJob(jobId, job =>
            {
                job.Status = "failed";
                job.IngestOk = false;
                job.IngestError = "DICOM ingest failed. Please try again in a few minutes.";
                job.CompletedAtUtc = DateTime.UtcNow;
            });
        }
    }

    private void UpdateJob(Guid jobId, Action<StudyIngestJobStatusDto> mutate)
    {
        if (!_cache.TryGetValue(CacheKey(jobId), out StudyIngestJobStatusDto? job) || job == null)
            return;

        mutate(job);
        _cache.Set(CacheKey(jobId), job, JobTtl);
    }

    private static string CacheKey(Guid jobId) => $"{CachePrefix}{jobId:N}";
}
