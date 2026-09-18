using System.Runtime.Serialization;

namespace nextorm.core;

/// <summary>
/// Thrown when the SQL text for a command cannot be generated.
/// </summary>
/// <remarks>
/// Derives directly from <see cref="Exception"/> rather than <see cref="DataContextException"/>,
/// which prevents callers from catching a common library base.
/// See <c>API-NAMING-REVIEW.md</c> finding P0-5.
/// </remarks>
[Serializable]
public class BuildSqlCommandException : Exception
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
