using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace BoneVisQA.Services.Services;

public sealed class MedicalCaseIndexingProcessor : IMedicalCaseIndexingProcessor
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<MedicalCaseIndexingProcessor> _logger;

    public MedicalCaseIndexingProcessor(
        IUnitOfWork unitOfWork,
        IEmbeddingService embeddingService,
        ILogger<MedicalCaseIndexingProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task ProcessMedicalCaseAsync(Guid medicalCaseId, CancellationToken cancellationToken = default)
    {
        var mc = await _unitOfWork.Context.MedicalCases
            .FirstOrDefaultAsync(x => x.Id == medicalCaseId, cancellationToken)
            ?? throw new InvalidOperationException($"Medical case {medicalCaseId} not found.");

        var text = BuildIndexingText(mc);
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("[MedicalCaseIndexing] Empty text for case {CaseId}; marking Failed.", medicalCaseId);
            mc.IndexingStatus = DocumentIndexingStatuses.Failed;
            await _unitOfWork.SaveAsync();
            return;
        }

        float[] embedding;
        try
        {
            embedding = await _embeddingService.EmbedTextAsync(text, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MedicalCaseIndexing] Embedding API failed for case {CaseId}.", medicalCaseId);
            mc.IndexingStatus = DocumentIndexingStatuses.Failed;
            await _unitOfWork.SaveAsync();
            return;
        }

        mc.Embedding = new Vector(embedding);
        mc.IndexingStatus = DocumentIndexingStatuses.Completed;
        await _unitOfWork.SaveAsync();
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
