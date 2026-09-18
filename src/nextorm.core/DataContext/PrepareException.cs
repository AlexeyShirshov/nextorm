using System.Runtime.Serialization;

namespace nextorm.core;

/// <summary>
/// Thrown when a query cannot be prepared (compiled) for execution.
/// </summary>
/// <remarks>
/// The name is intentionally generic in the current API but should be narrowed to
/// <c>QueryPreparationException</c>, and the type should derive from
/// <see cref="DataContextException"/> so callers can catch a single library-wide base.
/// See <c>API-NAMING-REVIEW.md</c> finding P0-5.
/// </remarks>
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
