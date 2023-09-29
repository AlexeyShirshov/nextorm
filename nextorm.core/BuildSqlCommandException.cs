using System.Runtime.Serialization;

namespace nextorm.core;

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

    protected BuildSqlCommandException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
