namespace BoneVisQA.Services.Exceptions;

/// <summary>
/// Thrown when a request conflicts with the current state of a resource.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
