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
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPreparationException"/> class.
    /// </summary>
    public QueryPreparationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPreparationException"/> class with a
    /// specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public QueryPreparationException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPreparationException"/> class with a
    /// specified error message and a reference to the inner exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <see langword="null"/> if none.</param>
    public QueryPreparationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPreparationException"/> class from serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">Contextual information about the source or destination of the serialization.</param>
    [Obsolete(DiagnosticId = "SYSLIB0051")]
    protected QueryPreparationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
