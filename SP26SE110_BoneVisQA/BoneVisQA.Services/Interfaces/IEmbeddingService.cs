namespace BoneVisQA.Services.Interfaces;

/// <summary>
/// Generates 768-dimensional embeddings (aligned with sentence-transformers/paraphrase-multilingual-mpnet-base-v2 and document_chunks vector(768)).
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Returns a 768-length float array via the HuggingFace Inference API (see HuggingFace:EmbeddingUrl).
    /// </summary>
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);
}
