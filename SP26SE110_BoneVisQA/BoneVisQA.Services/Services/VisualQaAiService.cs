using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace BoneVisQA.Services.Services;

public class VisualQaAiService : IVisualQaAiService
{
    private readonly BoneVisQADbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IOpenRouterService _openRouterService;

    public VisualQaAiService(
        BoneVisQADbContext dbContext,
        IEmbeddingService embeddingService,
        IImageProcessingService imageProcessingService,
        IOpenRouterService openRouterService)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _imageProcessingService = imageProcessingService;
        _openRouterService = openRouterService;
    }

    public async Task<VisualQAResponseDto> RunPipelineAsync(VisualQARequestDto request, CancellationToken cancellationToken = default)
    {
        var embedding = await _embeddingService.EmbedTextAsync(request.QuestionText, cancellationToken);
        var queryVector = new Vector(embedding);

        Guid? caseCategoryId = null;
        if (request.CaseId.HasValue && request.CaseId.Value != Guid.Empty)
        {
            caseCategoryId = await _dbContext.MedicalCases
                .AsNoTracking()
                .Where(mc => mc.Id == request.CaseId.Value)
                .Select(mc => mc.CategoryId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Filter RAG context to the same document category as the provided CaseId (if available).
        // Place WHERE before ORDERBY(CosineDistance) to improve performance and accuracy.
        var topChunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Include(c => c.Doc)
            .Where(c => c.Embedding != null && (caseCategoryId == null || c.Doc.CategoryId == caseCategoryId))
            .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
            .Take(5)
            .ToListAsync(cancellationToken);

        string? imageB64 = null;
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            imageB64 = await _imageProcessingService.DrawBoundingBoxAsBase64JpegAsync(
                request.ImageUrl,
                request.Coordinates,
                cancellationToken);
        }

        return await _openRouterService.GenerateDiagnosticAnswerAsync(
            request.QuestionText,
            imageB64,
            topChunks,
            request.Coordinates,
            cancellationToken);
    }
}
