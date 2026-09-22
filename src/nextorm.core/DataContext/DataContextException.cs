using System.Runtime.Serialization;

namespace NextORM.Core;

/// <summary>
/// Base type for errors raised by a <see cref="IDataContext"/>.
/// </summary>
/// <remarks>
/// The library's domain exceptions (<see cref="BuildSqlCommandException"/> and
/// <see cref="QueryPreparationException"/>) derive from this type, so callers can catch a single
/// library-wide base.
/// </remarks>
[Serializable]
public class DataContextException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataContextException"/> class.
    /// </summary>
    public DataContextException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataContextException"/> class with a specified
    /// error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DataContextException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataContextException"/> class with a specified
    /// error message and a reference to the inner exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <see langword="null"/> if none.</param>
    public DataContextException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataContextException"/> class from serialized data.
    /// </summary>
    /// <param name="info">The object that holds the serialized object data.</param>
    /// <param name="context">Contextual information about the source or destination of the serialization.</param>
    [Obsolete(DiagnosticId = "SYSLIB0051")]
    protected DataContextException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
