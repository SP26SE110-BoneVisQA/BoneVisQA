using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace BoneVisQA.Services.Services;

public sealed class MedicalCaseIndexingProcessor : IMedicalCaseIndexingProcessor
{
    private const int EmbeddingBatchSize = 100;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmbeddingService _embeddingService;
    private readonly IIndexingExecutionGate _indexingExecutionGate;
    private readonly ILogger<MedicalCaseIndexingProcessor> _logger;

    public MedicalCaseIndexingProcessor(
        IUnitOfWork unitOfWork,
        IEmbeddingService embeddingService,
        IIndexingExecutionGate indexingExecutionGate,
        ILogger<MedicalCaseIndexingProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _embeddingService = embeddingService;
        _indexingExecutionGate = indexingExecutionGate;
        _logger = logger;
    }

    public async Task ProcessMedicalCaseAsync(Guid medicalCaseId, CancellationToken cancellationToken = default)
    {
        await using var queueLease = await _indexingExecutionGate.AcquireAsync(cancellationToken);
        MedicalCase? mc = null;
        var completed = false;
        try
        {
            mc = await _unitOfWork.Context.MedicalCases
                .FirstOrDefaultAsync(x => x.Id == medicalCaseId, cancellationToken)
                ?? throw new InvalidOperationException($"Medical case {medicalCaseId} not found.");

            var text = BuildIndexingText(mc);
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("[MedicalCaseIndexing] Empty text for case {CaseId}; marking Failed.", medicalCaseId);
                return;
            }

            var estimatedRequests = (int)Math.Ceiling(1d / EmbeddingBatchSize);
            _logger.LogInformation(
                "[Queue] Starting indexing for MedicalCase {Id}. Estimated requests: {Count}",
                medicalCaseId,
                estimatedRequests);

            var embeddings = await _embeddingService.BatchEmbedContentsAsync(new[] { text }, cancellationToken);
            if (embeddings.Count == 0)
                throw new InvalidOperationException("Batch embedding returned no vectors for medical case.");

            mc.Embedding = new Vector(embeddings[0]);
            mc.IndexingStatus = DocumentIndexingStatuses.Completed;
            await _unitOfWork.SaveAsync();
            completed = true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[MedicalCaseIndexing] Processing cancelled for case {CaseId}.", medicalCaseId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MedicalCaseIndexing] Embedding API failed for case {CaseId}.", medicalCaseId);
        }
        finally
        {
            if (!completed)
            {
                try
                {
                    if (mc == null)
                    {
                        mc = await _unitOfWork.Context.MedicalCases
                            .FirstOrDefaultAsync(x => x.Id == medicalCaseId, CancellationToken.None);
                    }

                    if (mc != null)
                    {
                        mc.IndexingStatus = DocumentIndexingStatuses.Failed;
                        await _unitOfWork.SaveAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MedicalCaseIndexing] Could not set Failed for case {CaseId}.", medicalCaseId);
                }
            }
        }
    }

    /// <summary>Matches RAG retrieval text: title, description, suggested diagnosis, key findings.</summary>
    public static string BuildIndexingText(MedicalCase mc)
    {
        var parts = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(mc.Title)) parts.Add(mc.Title.Trim());
        if (!string.IsNullOrWhiteSpace(mc.Description)) parts.Add(mc.Description.Trim());
        if (!string.IsNullOrWhiteSpace(mc.SuggestedDiagnosis)) parts.Add(mc.SuggestedDiagnosis.Trim());
        if (!string.IsNullOrWhiteSpace(mc.KeyFindings)) parts.Add(mc.KeyFindings.Trim());
        return string.Join("\n", parts);
    }
}
