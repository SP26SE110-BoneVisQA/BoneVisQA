using System;

namespace BoneVisQA.Services.Exceptions;

public class AiResponseFormatException : Exception
{
    public AiResponseFormatException(string message)
        : base(message)
    {
    }

    public AiResponseFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
