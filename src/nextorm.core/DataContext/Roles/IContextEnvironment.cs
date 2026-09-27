using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Ambient context state and configuration: loggers, mapping mode and the user-owned property bag.
/// </summary>
public interface IContextEnvironment
{
    /// <summary>
    /// The general-purpose logger for the context, or <see langword="null"/> when logging is disabled.
    /// </summary>
    ILogger? Logger { get; }
    /// <summary>
    /// The logger that traces executed SQL commands, or <see langword="null"/> when command logging is disabled.
    /// </summary>
    ILogger? CommandLogger { get; }
    /// <summary>
    /// Whether rows returned by the provider must be mapped to results, as opposed to using a scalar
    /// or in-memory fast path.
    /// </summary>
    bool NeedMapping { get; }
    /// <summary>
    /// A user-owned bag of arbitrary context properties, not interpreted by the engine.
    /// </summary>
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

    /// <summary>
    /// The context-wide default case for SQL keywords (see <c>DataContextBuilder.UseKeywordCase</c>).
    /// A command can override it with <c>WithKeywordCase</c>. The default implementation returns
    /// <see cref="NextORM.Core.KeywordCase.Lower"/> so existing external implementations keep compiling.
    /// </summary>
    KeywordCase KeywordCase => NextORM.Core.KeywordCase.Lower;

    /// <summary>
    /// Whether rendered batch SQL places each statement on its own line (see
    /// <c>DataContextBuilder.UseMultilineBatchSql</c>). The default implementation returns
    /// <see langword="false"/> so existing external implementations keep compiling and batch SQL stays
    /// on a single line.
    /// </summary>
    bool MultilineBatchSql => false;

    /// <summary>
    /// The context-wide default command timeout in seconds (see
    /// <c>DataContextBuilder.UseCommandTimeout</c>), or <see langword="null"/> when no timeout is
    /// configured and the provider default applies. A command can override it with
    /// <c>WithCommandTimeout</c>. The default implementation returns <see langword="null"/> so existing
    /// external implementations keep compiling.
    /// </summary>
    int? CommandTimeout => null;
}
