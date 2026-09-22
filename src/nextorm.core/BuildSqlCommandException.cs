using System.Runtime.Serialization;

namespace NextORM.Core;

/// <summary>
/// Thrown when the SQL text for a command cannot be generated.
/// </summary>
/// <remarks>
/// Derives from <see cref="DataContextException"/> so callers can catch a single library-wide base.
/// </remarks>
[Serializable]
public class BuildSqlCommandException : DataContextException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuildSqlCommandException"/> class.
    /// </summary>
    public BuildSqlCommandException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildSqlCommandException"/> class with a specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BuildSqlCommandException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildSqlCommandException"/> class with a specified
    /// error message and a reference to the inner exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <see langword="null"/> if none.</param>
    public BuildSqlCommandException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildSqlCommandException"/> class from serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">Contextual information about the source or destination of the serialization.</param>
    [Obsolete(DiagnosticId = "SYSLIB0051")]
    protected BuildSqlCommandException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
