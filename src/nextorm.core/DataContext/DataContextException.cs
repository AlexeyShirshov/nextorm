using System.Runtime.Serialization;

namespace nextorm.core;

/// <summary>
/// Base type for errors raised by a <see cref="IDataContext"/>.
/// </summary>
/// <remarks>
/// Not every library exception derives from this type: <see cref="BuildSqlCommandException"/> and
/// <see cref="PrepareException"/> derive directly from <see cref="Exception"/>, so callers cannot
/// catch a single library-wide base. See <c>API-NAMING-REVIEW.md</c> finding P0-5.
/// </remarks>
[Serializable]
public class DataContextException : Exception
{
    public DataContextException()
    {
    }

    public DataContextException(string? message) : base(message)
    {
    }

    public DataContextException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [Obsolete(DiagnosticId = "SYSLIB0051")]
    protected DataContextException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
