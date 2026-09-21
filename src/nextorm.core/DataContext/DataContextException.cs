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
