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
    public BuildSqlCommandException()
    {
    }

    public BuildSqlCommandException(string? message) : base(message)
    {
    }

    public BuildSqlCommandException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [Obsolete(DiagnosticId = "SYSLIB0051")]
    protected BuildSqlCommandException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
