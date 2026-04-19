namespace BoneVisQA.Services.Interfaces;

/// <summary>
/// Generates 768-dimensional embeddings for <c>document_chunks</c> (<c>vector(768)</c>) and RAG queries.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Returns a 768-length embedding vector.
    /// </summary>
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch embedding API. Returns one 768-length vector per input text in the same order.
    /// </summary>
    Task<IReadOnlyList<float[]>> BatchEmbedContentsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}
