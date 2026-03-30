namespace BoneVisQA.Services.Exceptions;

/// <summary>
/// Thrown when the embedding provider cannot return a valid 768-dimensional vector.
/// Ingestion must abort rather than persisting mock or malformed vectors.
/// </summary>
public sealed class EmbeddingFailedException : Exception
{
    public EmbeddingFailedException(string message)
        : base(message)
    {
    }

    public EmbeddingFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
