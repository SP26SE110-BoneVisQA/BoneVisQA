using System.Threading;
using BoneVisQA.Services.Models.Admin;

namespace BoneVisQA.Services.Interfaces.Admin;

public interface IDocumentQualityService
{
    Task<List<DocumentQualityDTO>> GetMostReferencedDocumentsAsync(int top = 10);
    Task<List<DocumentQualityDTO>> GetDocumentsWithNegativeExpertReviewsAsync();
    Task<List<DocumentQualityDTO>> GetOutdatedDocumentsAsync(int yearsThreshold = 2);
    Task<List<DocumentQualityDTO>> GetDocumentsFlaggedForReviewAsync();

    /// <summary>Chunks flagged by experts during Visual QA review (low-quality retrieval).</summary>
    Task<IReadOnlyList<AdminFlaggedChunkListItemDto>> GetFlaggedDocumentChunksAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Clears expert flag on a chunk after admin review (<paramref name="resolved"/> must be true).</summary>
    Task ResolveDocumentChunkFlagAsync(Guid chunkId, bool resolved, CancellationToken cancellationToken = default);
}
