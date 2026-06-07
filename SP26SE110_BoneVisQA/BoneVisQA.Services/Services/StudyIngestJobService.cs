using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

/// <summary>
/// Runs Supabase staging + Python DICOM ingest off the HTTP request thread (Render 30s limit).
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

    public Task<Guid> QueueLocalArchiveAsync(
        string localArchivePath,
        string ingestPurpose,
        Guid? ownerUserId,
        string? diagnosisText,
        StudyIngestJobKind kind,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(localArchivePath) || !File.Exists(localArchivePath))
            throw new FileNotFoundException("Staged DICOM archive not found.", localArchivePath);

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

        _logger.LogInformation(
            "Queued study ingest job {JobId} ({Kind}) from {Path}.",
            jobId,
            kind,
            localArchivePath);

        _ = Task.Run(() => RunIngestJobAsync(
            jobId,
            localArchivePath,
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
        string localArchivePath,
        string ingestPurpose,
        Guid? ownerUserId,
        string? diagnosisText,
        StudyIngestJobKind kind)
    {
        string? stagingBucket = null;
        string? stagingObjectPath = null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var pythonAi = scope.ServiceProvider.GetRequiredService<IPythonAiConnectorService>();
            var storage = scope.ServiceProvider.GetRequiredService<ISupabaseStorageService>();

            _logger.LogInformation("Study ingest job {JobId}: uploading archive to Supabase.", jobId);

            var staged = await StudyArchiveIngestHelper.UploadStagedArchiveAsync(
                storage,
                localArchivePath,
                ingestPurpose,
                ownerUserId,
                CancellationToken.None);
            stagingBucket = staged.Bucket;
            stagingObjectPath = staged.ObjectPath;

            _logger.LogInformation("Study ingest job {JobId}: calling Python /ingest.", jobId);

            var ingest = await pythonAi.TriggerIngestAsync(
                staged.IngestReferenceUrl,
                diagnosis: diagnosisText ?? string.Empty,
                ingestPurpose: ingestPurpose,
                ownerUserId: ownerUserId);

            if (!ingest.Success || !ingest.CaseId.HasValue)
            {
                var message = StudyArchiveIngestHelper.ResolveIngestErrorMessage(ingest);
                _logger.LogWarning(
                    "Study ingest job {JobId} failed: HTTP {Status} — {Message}",
                    jobId,
                    ingest.StatusCode,
                    message);

                UpdateJob(jobId, job =>
                {
                    job.Status = "failed";
                    job.IngestOk = false;
                    job.IngestError = message;
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

            _logger.LogInformation(
                "Study ingest job {JobId} completed. CaseId={CaseId} SessionId={SessionId}",
                jobId,
                ingest.CaseId,
                sessionId);

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
            _logger.LogError(ex, "Study ingest job {JobId} failed with exception.", jobId);
            UpdateJob(jobId, job =>
            {
                job.Status = "failed";
                job.IngestOk = false;
                job.IngestError = "DICOM ingest failed. Please try again in a few minutes.";
                job.CompletedAtUtc = DateTime.UtcNow;
            });
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(stagingBucket) && !string.IsNullOrWhiteSpace(stagingObjectPath))
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var storage = scope.ServiceProvider.GetRequiredService<ISupabaseStorageService>();
                    await storage.DeleteFileAsync(stagingBucket, stagingObjectPath, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Study ingest job {JobId}: staging object cleanup failed.", jobId);
                }
            }

            StudyArchiveIngestHelper.TryDeleteStagedFile(localArchivePath);
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
