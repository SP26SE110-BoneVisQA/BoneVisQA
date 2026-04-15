namespace BoneVisQA.Services.Interfaces;

/// <summary>
/// Generates 768-dimensional embeddings for <c>document_chunks</c> (<c>vector(768)</c>) and RAG queries.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Returns a 768-length float array (Google Gemini <c>text-embedding-004</c>).
    /// </summary>
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);
}
