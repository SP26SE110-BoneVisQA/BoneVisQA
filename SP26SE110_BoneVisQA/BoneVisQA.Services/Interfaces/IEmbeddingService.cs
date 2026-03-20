namespace BoneVisQA.Services.Interfaces;

/// <summary>
/// Generates 768-dimensional embeddings (aligned with paraphrase-multilingual-MiniLM-L12-v2 / document_chunks vector(768)).
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Returns a 768-length float array. Uses HuggingFace Inference API when configured; otherwise deterministic mock embeddings.
    /// </summary>
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);
}
