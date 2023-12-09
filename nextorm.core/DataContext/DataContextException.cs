using System.Runtime.Serialization;

namespace nextorm.core;

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

    protected DataContextException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
