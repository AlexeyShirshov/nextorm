using System.Runtime.Serialization;

namespace NextORM.Core;

/// <summary>
/// Thrown when a query cannot be prepared (compiled) for execution.
/// </summary>
/// <remarks>
/// Derives from <see cref="DataContextException"/> so callers can catch a single library-wide base.
/// </remarks>
[Serializable]
public class QueryPreparationException : DataContextException
{
    public QueryPreparationException()
    {
    }

    public QueryPreparationException(string? message) : base(message)
    {
    }

    public QueryPreparationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [Obsolete(DiagnosticId = "SYSLIB0051")]
    protected QueryPreparationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
