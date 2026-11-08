using System.Runtime.Serialization;

namespace NextORM.Core;

/// <summary>
/// Thrown when a data-modifying statement would write a row that violates one of the entity type's
/// active global query filters. Unlike a <c>WHERE</c> clause, the filters are never injected into the
/// target of an <c>INSERT</c>/<c>MERGE</c>; instead the values being written (or the rows read by an
/// <c>INSERT ... SELECT</c>/query-sourced <c>MERGE</c> source) are validated against them before the
/// statement executes.
/// </summary>
/// <remarks>
/// Derives from <see cref="DataContextException"/> so callers can catch a single library-wide base.
/// Validation is best effort: an <c>INSERT ... SELECT</c>/query-sourced <c>MERGE</c> pre-check is a
/// separate read, so a concurrent writer can still change the rows between the check and the write
/// (a TOCTOU race). The exception is not thrown for a partial batch after earlier rows were already
/// written; callers must not assume atomicity.
/// </remarks>
[Serializable]
public class QueryFilterException : DataContextException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFilterException"/> class.
    /// </summary>
    public QueryFilterException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFilterException"/> class with a specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public QueryFilterException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFilterException"/> class with a specified
    /// error message and a reference to the inner exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <see langword="null"/> if none.</param>
    public QueryFilterException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryFilterException"/> class from serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">Contextual information about the source or destination of the serialization.</param>
    [Obsolete(DiagnosticId = "SYSLIB0051")]
    protected QueryFilterException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
