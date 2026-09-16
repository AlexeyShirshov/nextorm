using Microsoft.Extensions.Logging;

namespace nextorm.core;

/// <summary>
/// Ambient context state and configuration: loggers, mapping mode and the user-owned property bag.
/// </summary>
public interface IContextEnvironment
{
    ILogger? Logger { get; }
    ILogger? CommandLogger { get; }
    bool NeedMapping { get; }
    Dictionary<string, object> Properties { get; }
}
