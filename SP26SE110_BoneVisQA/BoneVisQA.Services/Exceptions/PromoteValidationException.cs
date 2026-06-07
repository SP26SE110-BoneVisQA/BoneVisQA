namespace BoneVisQA.Services.Exceptions;

public class PromoteValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public PromoteValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Validation failed.")
    {
        Errors = errors;
    }
}
