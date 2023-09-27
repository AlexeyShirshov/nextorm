using System.Runtime.Serialization;

namespace nextorm.core;

[Serializable]
internal class PrepareException : Exception
{
    public PrepareException()
    {
    }

    public PrepareException(string? message) : base(message)
    {
    }

    public PrepareException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    protected PrepareException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
