namespace BoneVisQA.Services.Interfaces;

/// <summary>
/// RAG ingestion for a single document: extract text, sliding-window chunk, embed, persist chunks, finalize status.
/// </summary>
public interface IDocumentIndexingProcessor
{
    Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
