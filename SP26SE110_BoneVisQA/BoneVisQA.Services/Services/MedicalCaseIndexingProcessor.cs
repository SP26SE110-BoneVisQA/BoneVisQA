using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

/// <summary>
/// Marks catalog cases as indexed without generating vectors in C# (embeddings live in Python / DB pipelines).
/// </summary>
public sealed class MedicalCaseIndexingProcessor : IMedicalCaseIndexingProcessor
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIndexingExecutionGate _indexingExecutionGate;
    private readonly ILogger<MedicalCaseIndexingProcessor> _logger;

    public MedicalCaseIndexingProcessor(
        IUnitOfWork unitOfWork,
        IIndexingExecutionGate indexingExecutionGate,
        ILogger<MedicalCaseIndexingProcessor> logger)
    {
        _unitOfWork = unitOfWork;
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

            _logger.LogInformation("[MedicalCaseIndexing] Completing case {CaseId} without C# embedding generation.", medicalCaseId);

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
            _logger.LogError(ex, "[MedicalCaseIndexing] Failed for case {CaseId}.", medicalCaseId);
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

    /// <summary>Matches legacy RAG indexing text: title, description, suggested diagnosis, key findings.</summary>
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
