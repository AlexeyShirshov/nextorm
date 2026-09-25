using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Implementation of <see cref="IContextEnvironment"/> shared by the SQL contexts and
/// <see cref="InMemoryDataContext"/>. Owns the ambient state — loggers, mapping mode and the user-owned
/// property bag — so that a context is left with its lifecycle and provider hooks instead of also
/// constructing and holding this configuration.
/// </summary>
internal sealed class ContextEnvironment : IContextEnvironment
{
    private readonly Dictionary<string, object> _properties = [];

    internal ContextEnvironment(ILoggerFactory? loggerFactory, Type contextType, bool needMapping, bool logSensitiveData, bool quoteIdentifiers = false, INamingConvention? namingConvention = null, KeywordCase keywordCase = KeywordCase.Lower, bool multilineBatchSql = false)
    {
        if (loggerFactory is not null)
        {
            Logger = loggerFactory.CreateLogger(contextType);
            CommandLogger = loggerFactory.CreateLogger(typeof(QueryCommand));
            ResultSetEnumeratorLogger = loggerFactory.CreateLogger("NextORM.Core.ResultSetEnumerator");
        }

        LogParams = Logger?.IsEnabled(LogLevel.Debug) ?? false;
        NeedMapping = needMapping;
        LogSensitiveData = logSensitiveData;
        QuoteIdentifiers = quoteIdentifiers;
        NamingConvention = namingConvention;
        this.KeywordCase = keywordCase;
        MultilineBatchSql = multilineBatchSql;
    }

    public ILogger? Logger { get; }

    public ILogger? CommandLogger { get; }

    /// <summary>Logger category used by the row-reader path (the result-set enumerator).</summary>
    internal ILogger? ResultSetEnumeratorLogger { get; }

    /// <summary>Whether parameter values are written to the debug log, derived once from the log level.</summary>
    internal bool LogParams { get; }

    internal bool LogSensitiveData { get; }

    public bool NeedMapping { get; }

    public bool QuoteIdentifiers { get; }

    public INamingConvention? NamingConvention { get; }

    public KeywordCase KeywordCase { get; }

    public bool MultilineBatchSql { get; }

    public Dictionary<string, object> Properties => _properties;
}
