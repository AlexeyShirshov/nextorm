using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Ambient context state and configuration: loggers, mapping mode and the user-owned property bag.
/// </summary>
public interface IContextEnvironment
{
    ILogger? Logger { get; }
    ILogger? CommandLogger { get; }
    bool NeedMapping { get; }
    Dictionary<string, object> Properties { get; }
    /// <summary>
    /// The context-wide default for identifier quoting (see
    /// <c>DataContextBuilder.UseQuotedIdentifiers</c>). A command can override it with
    /// <c>WithQuotedIdentifiers</c>. The default implementation returns <c>false</c> so existing
    /// external implementations keep compiling.
    /// </summary>
    bool QuoteIdentifiers => false;

    /// <summary>
    /// The context-wide default naming convention for auto-derived names (see
    /// <c>DataContextBuilder.UseNamingConvention</c>). A command can override it with
    /// <c>WithNamingConvention</c>. The default implementation returns <see langword="null"/> (names
    /// are emitted verbatim) so existing external implementations keep compiling.
    /// </summary>
    INamingConvention? NamingConvention => null;
}
