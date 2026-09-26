using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Configures and creates an <see cref="IDataContext"/>.
/// </summary>
/// <remarks>
/// The <c>DataContext</c> prefix is inconsistent with the rest of the library, which otherwise uses
/// <c>DataContext</c> (for example <c>DataContextOptionsBuilderExtensions</c>). Recommended name:
/// <c>DataContextBuilder</c>. See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P2-22.
/// </remarks>
public class DataContextBuilder
{
    //private readonly Dictionary<string, object> _props = new();
    private ILoggerFactory? _loggerFactory;
    //private IDataProvider? _dataProvider;
    private bool _logSensitiveData;
    private int? _commandTimeout;
    private Func<DataContextBuilder, IDataContext>? _factory;
    private readonly List<IQueryInterceptor> _queryInterceptors = [];
    private readonly List<IConnectionInterceptor> _connectionInterceptors = [];
    /// <summary>
    /// Whether command parameter values are written to the configured logger. Defaults to
    /// <see langword="false"/>; enable it only for local diagnostics, since parameter values may contain
    /// sensitive data. Set through <see cref="LogSensitiveData"/>.
    /// </summary>
    public bool ShouldLogSensitiveData => _logSensitiveData;
    /// <summary>
    /// Whether physical identifiers (table and column names) are quoted with the provider's delimiter
    /// in generated SQL. Defaults to <c>false</c> (names are emitted verbatim); can be overridden per
    /// command with <c>WithQuotedIdentifiers</c>.
    /// </summary>
    public bool QuoteIdentifiers { get; private set; }

    /// <summary>
    /// Convention applied by default to table and column names that were not declared explicitly
    /// (no attribute/fluent mapping). <see langword="null"/> (the default) emits the CLR names
    /// verbatim; can be overridden per command with <c>WithNamingConvention</c>.
    /// </summary>
    public INamingConvention? NamingConvention { get; private set; }

    /// <summary>
    /// The letter case in which SQL keywords are emitted by default. Defaults to
    /// <see cref="NextORM.Core.KeywordCase.Lower"/> (unchanged historical output); can be overridden per
    /// command with <c>WithKeywordCase</c>. Set through <see cref="UseKeywordCase"/>.
    /// </summary>
    public KeywordCase KeywordCase { get; private set; }

    /// <summary>
    /// Whether a rendered batch places each statement on its own line. Defaults to
    /// <see langword="false"/> (statements joined on one line with <c>"; "</c>). Set through
    /// <see cref="UseMultilineBatchSql"/>; affects the batch SQL produced by <c>BatchQuery.ToSql()</c>
    /// and the joined command text only, not the per-command <c>DbBatch</c> form.
    /// </summary>
    public bool MultilineBatchSql { get; private set; }

    /// <summary>
    /// The command timeout in seconds applied to every command of the contexts this builder creates, or
    /// <see langword="null"/> when no timeout is configured and the provider default applies. Set through
    /// <see cref="UseCommandTimeout"/>; a command can override it with <c>WithCommandTimeout</c>.
    /// </summary>
    public int? CommandTimeout => _commandTimeout;
    //internal IDataProvider? DataProvider => _dataProvider;
    internal ILoggerFactory? LoggerFactory => _loggerFactory;
    // public bool CacheQueryCommand { get; set; } = true;
    // public bool CacheExpressions { get; set; } = true;
    /// <summary>
    /// Factory invoked by <see cref="CreateDataContext"/> to materialize the configured context.
    /// <see langword="null"/> until one of the provider extensions (for example
    /// <c>UseInMemoryContext</c>) assigns it; <see cref="CreateDataContext"/> throws
    /// <see cref="InvalidOperationException"/> in that state.
    /// </summary>
    public Func<DataContextBuilder, IDataContext>? Factory { get => _factory; set => _factory = value; }

    //public Dictionary<string, object> Property => _props;
    /// <summary>
    /// Sets the logger factory used for diagnostic logging (SQL text and, when enabled, parameter
    /// values).
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use; must not be <see langword="null"/>.</param>
    /// <returns>This builder, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="loggerFactory"/> is <see langword="null"/>.</exception>
    public DataContextBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _loggerFactory = loggerFactory;
        return this;
    }
    // public DataContextOptionsBuilder UseSqlClient(SqlDataProvider dataProvider)
    // {
    //     ArgumentNullException.ThrowIfNull(dataProvider);

    //     _dataProvider = dataProvider;        
    //     return this;
    // }
    /// <summary>
    /// Enables or disables logging of command parameter values. Disabled by default; only enable it for
    /// local diagnostics, as parameter values may contain sensitive data.
    /// </summary>
    /// <param name="logSensitiveData"><see langword="true"/> to log parameter values; otherwise <see langword="false"/>.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataContextBuilder LogSensitiveData(bool logSensitiveData)
    {
        _logSensitiveData = logSensitiveData;
        return this;
    }

    /// <summary>
    /// Enables quoting of physical identifiers (table and column names) in generated SQL, using the
    /// provider's delimiter (<c>"id"</c> on PostgreSQL/SQLite, <c>[id]</c> on SQL Server, `` `id` `` on
    /// MySQL/MariaDB/ClickHouse). A single command can override the setting with
    /// <c>WithQuotedIdentifiers</c>.
    /// </summary>
    public DataContextBuilder UseQuotedIdentifiers(bool value = true)
    {
        QuoteIdentifiers = value;
        return this;
    }

    /// <summary>
    /// Applies a naming convention to auto-derived table and column names; for example
    /// <see cref="SnakeCaseNamingConvention.Instance"/> maps <c>SimpleEntity</c> to
    /// <c>simple_entity</c> and <c>FirstName</c> to <c>first_name</c>. Names declared with
    /// <c>[SqlTable]</c>/<c>[Column]</c> or a fluent mapping are never translated. Pass
    /// <see langword="null"/> to emit CLR names verbatim (the default). A single command can override
    /// the setting with <c>WithNamingConvention</c>.
    /// </summary>
    public DataContextBuilder UseNamingConvention(INamingConvention? convention)
    {
        NamingConvention = convention;
        return this;
    }

    /// <summary>
    /// Sets the letter case in which SQL keywords are emitted. <see cref="KeywordCase.Lower"/> (the
    /// default) emits <c>select ... from ...</c>; <see cref="KeywordCase.Upper"/> emits
    /// <c>SELECT ... FROM ...</c>. Identifiers, string literals, function names, type names and raw SQL
    /// are never affected. A single command can override the setting with <c>WithKeywordCase</c>.
    /// </summary>
    /// <param name="keywordCase">The keyword case to apply.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataContextBuilder UseKeywordCase(KeywordCase keywordCase = KeywordCase.Upper)
    {
        KeywordCase = keywordCase;
        return this;
    }

    /// <summary>
    /// Convenience form of <see cref="UseKeywordCase"/>: enables (<paramref name="value"/> is
    /// <see langword="true"/>) or disables upper-case SQL keywords.
    /// </summary>
    /// <param name="value"><see langword="true"/> to emit keywords in upper case; otherwise lower case.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataContextBuilder UseUppercaseKeywords(bool value = true)
        => UseKeywordCase(value ? KeywordCase.Upper : KeywordCase.Lower);

    /// <summary>
    /// Enables rendering of batch SQL with one statement per line: each statement is terminated with
    /// <c>;</c> followed by a newline instead of a space. Intended for readable logs and inspection;
    /// the executed SQL is unaffected (providers using <c>DbBatch</c> never run the joined text, and
    /// SQLite/SQL Server accept the newline).
    /// </summary>
    /// <param name="value"><see langword="true"/> to place each batch statement on its own line; otherwise <see langword="false"/>.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataContextBuilder UseMultilineBatchSql(bool value = true)
    {
        MultilineBatchSql = value;
        return this;
    }

    /// <summary>
    /// Sets the default command timeout in seconds for every command of the contexts this builder
    /// creates (the ADO.NET <c>DbCommand.CommandTimeout</c>). A value of zero or less means the
    /// provider default (nothing is changed, the zero-cost path); the same applies before the call. A
    /// single command can override the value with <c>WithCommandTimeout</c>. The resolved timeout is
    /// part of the plan-cache key, so contexts with different defaults do not share a cached command.
    /// The in-memory context has no command and ignores it.
    /// </summary>
    /// <param name="seconds">The default command timeout in seconds; zero or less uses the provider default.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataContextBuilder UseCommandTimeout(int seconds)
    {
        _commandTimeout = seconds > 0 ? seconds : null;
        return this;
    }

    /// <summary>
    /// Registers a query interceptor that observes the command execution lifecycle of every context
    /// this builder creates. Interceptors are invoked in registration order, after the ones already
    /// registered.
    /// </summary>
    /// <param name="interceptor">The interceptor to register; must not be <see langword="null"/>.</param>
    /// <returns>This builder, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="interceptor"/> is <see langword="null"/>.</exception>
    public DataContextBuilder AddInterceptor(IQueryInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        _queryInterceptors.Add(interceptor);
        return this;
    }

    /// <summary>
    /// Registers a connection interceptor that observes the connection lifecycle of every context this
    /// builder creates. Interceptors are invoked in registration order, after the ones already
    /// registered.
    /// </summary>
    /// <param name="interceptor">The interceptor to register; must not be <see langword="null"/>.</param>
    /// <returns>This builder, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="interceptor"/> is <see langword="null"/>.</exception>
    public DataContextBuilder AddInterceptor(IConnectionInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        _connectionInterceptors.Add(interceptor);
        return this;
    }

    internal IQueryInterceptor[] QueryInterceptors => [.. _queryInterceptors];

    internal IConnectionInterceptor[] ConnectionInterceptors => [.. _connectionInterceptors];

    /// <summary>
    /// Creates a data context from the current configuration by invoking <see cref="Factory"/>.
    /// </summary>
    /// <returns>The configured <see cref="IDataContext"/>.</returns>
    /// <exception cref="InvalidOperationException">No factory has been configured on this builder.</exception>
    public IDataContext CreateDataContext()
    {
        if (_factory is null)
            throw new InvalidOperationException("Context is not set");

        return _factory(this);
    }

    // public DataContextOptionsBuilder UseInMemoryClient()
    // {
    //     _dataProvider = new InMemoryDataProvider();
    //     return this;
    // }
}