using System.Runtime.Serialization;

namespace nextorm.core;

[Serializable]
public class PrepareException : Exception
{
    public PrepareException()
    {
    }

    public PrepareException(string? message) : base(message)
    {
    }

    public PrepareException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [Obsolete(DiagnosticId = "SYSLIB0051")]
    protected PrepareException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
