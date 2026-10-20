using Microsoft.Extensions.DependencyInjection;
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
    // One ordered list holds every interceptor registration: either an already-created interceptor
    // instance or a Type to resolve from the captured IServiceProvider when a context is created.
    // Registration order is the invocation order in each event pipeline.
    private readonly List<object> _interceptorEntries = [];
    private readonly IServiceProvider? _serviceProvider;
    // Resolved for the current creation only (cleared by CreateDataContext), so a transient service
    // is resolved once per context and never reused across creations.
    private IQueryInterceptor[]? _resolvedQueryInterceptors;
    private IConnectionInterceptor[]? _resolvedConnectionInterceptors;

    /// <summary>
    /// Creates a standalone builder whose interceptors must be supplied as instances.
    /// </summary>
    public DataContextBuilder()
    {
    }

    /// <summary>
    /// Creates a builder backed by <paramref name="serviceProvider"/>, so interceptor service keys
    /// registered with <see cref="AddInterceptor(Type)"/>/<see cref="AddInterceptor{T}()"/> are
    /// resolved from that provider (its scope, not the root) when a context is created.
    /// </summary>
    /// <param name="serviceProvider">The service provider that owns the interceptor services.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    internal DataContextBuilder(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }
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

    /// <summary>
    /// Whether prepared queries may be stored in and reused from the plan cache. Defaults to
    /// <see langword="true"/>; set through <see cref="UseQueryCache"/>. When disabled, every
    /// preparation is built without caching (the equivalent of <c>storeInCache: false</c>) and the
    /// per-command <c>QueryCommand.Cache</c> flag is never mutated.
    /// </summary>
    public bool QueryCacheEnabled { get; private set; } = true;
    // public bool CacheQueryCommand { get; set; } = true;
    // public bool CacheExpressions { get; set; } = true;

    /// <summary>
    /// The context-wide default eager-load strategy applied to <c>LoadWith</c> collections when a query
    /// declares no explicit mode. Defaults to <see cref="EagerLoadMode.SplitQuery"/> (the historical
    /// behavior); set through <see cref="UseEagerLoadMode"/>. A query override
    /// (<c>LoadWith(..., mode)</c>) always wins over this default, and configuring a context never
    /// changes a static/global value.
    /// </summary>
    public EagerLoadMode EagerLoadMode { get; private set; } = EagerLoadMode.SplitQuery;
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
    /// Enables (<paramref name="enabled"/> is <see langword="true"/>, the default) or disables the
    /// implicit plan cache for every context this builder creates. Disabled means each terminal builds
    /// its plan again instead of looking it up; the shared <c>AnyCommand</c>'s sticky
    /// <c>QueryCommand.Cache</c> flag is not touched.
    /// </summary>
    /// <param name="enabled"><see langword="true"/> to use the plan cache; otherwise <see langword="false"/>.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataContextBuilder UseQueryCache(bool enabled = true)
    {
        QueryCacheEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Sets the context-wide default eager-load strategy used by <c>LoadWith</c> when a query declares
    /// no explicit mode. The default is <see cref="EagerLoadMode.SplitQuery"/> (the historical behavior).
    /// An explicit <c>LoadWith(..., mode)</c> always overrides this setting.
    /// <para>
    /// <see cref="EagerLoadMode.Default"/> is rejected: it is an inheritance sentinel, not a context
    /// setting. <see cref="EagerLoadMode.Correlated"/> is accepted as configuration, but a
    /// <c>LoadWith</c> that resolves to it fails with <see cref="NotSupportedException"/> during
    /// preparation (correlated/per-parent top-N execution is not implemented in this release).
    /// </para>
    /// </summary>
    /// <param name="mode">The context default; one of SplitQuery, SingleQuery, Correlated or Auto.</param>
    /// <returns>This builder, to allow chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="mode"/> is <see cref="EagerLoadMode.Default"/> or an undefined numeric value.
    /// </exception>
    public DataContextBuilder UseEagerLoadMode(EagerLoadMode mode)
    {
        switch (mode)
        {
            case EagerLoadMode.SplitQuery:
            case EagerLoadMode.SingleQuery:
            case EagerLoadMode.Correlated:
            case EagerLoadMode.Auto:
                EagerLoadMode = mode;
                return this;
            case EagerLoadMode.Default:
                throw new ArgumentOutOfRangeException(
                    nameof(mode), mode,
                    "EagerLoadMode.Default is an inheritance sentinel, not a context setting; pass a concrete strategy.");
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mode), mode,
                    $"'{mode}' is not a defined EagerLoadMode value; use SplitQuery, SingleQuery, Correlated or Auto.");
        }
    }

    /// <summary>
    /// Enables sliding expiration for the process-wide caches: an entry that is not read within
    /// <paramref name="ttl"/> is evicted lazily. Pass <see cref="TimeSpan.Zero"/> to disable it (the
    /// default). The setting is process-wide because the caches are static, so it applies to every
    /// context created afterwards as well.
    /// </summary>
    /// <param name="ttl">The sliding window; <see cref="TimeSpan.Zero"/> disables expiration.</param>
    /// <returns>This builder, to allow chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ttl"/> is negative.</exception>
    public DataContextBuilder UseCacheSlidingExpiration(TimeSpan ttl)
    {
        DataContextCache.CacheSlidingExpiration = ttl;
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
    /// <remarks>
    /// An instance that also implements <see cref="IConnectionInterceptor"/> is attached to both
    /// event pipelines. Registration is deduplicated by the instance's runtime type: a later
    /// registration of the same type is ignored (first registration wins).
    /// </remarks>
    public DataContextBuilder AddInterceptor(IQueryInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        AddEntry(interceptor);
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
    /// <remarks>
    /// An instance that also implements <see cref="IQueryInterceptor"/> is attached to both event
    /// pipelines. Registration is deduplicated by the instance's runtime type: a later registration
    /// of the same type is ignored (first registration wins).
    /// </remarks>
    public DataContextBuilder AddInterceptor(IConnectionInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        AddEntry(interceptor);
        return this;
    }

    /// <summary>
    /// Registers an interceptor service key to resolve from the provider captured by this builder when
    /// a context is created. The type is validated immediately; the service is resolved once per
    /// <see cref="CreateDataContext"/> call, honouring the DI lifetime and keeping DI ownership of the
    /// resolved instance.
    /// </summary>
    /// <param name="interceptorType">The closed reference type (concrete class, interface or abstract class) to resolve.</param>
    /// <returns>This builder, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="interceptorType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="interceptorType"/> is an open generic or value type, or is not assignable to
    /// <see cref="IQueryInterceptor"/> or <see cref="IConnectionInterceptor"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A context is created on a builder without a captured provider (a standalone builder).
    /// </exception>
    /// <remarks>
    /// Every registration - an instance or a type - shares one key: the instance's runtime type for an
    /// instance, the supplied <see cref="Type"/> for a type entry. The first registration of a key wins,
    /// including mixed instance/type entries, so a later registration of the same key is ignored.
    /// </remarks>
    public DataContextBuilder AddInterceptor(Type interceptorType)
    {
        ArgumentNullException.ThrowIfNull(interceptorType);

        if (!IsValidInterceptorType(interceptorType))
        {
            throw new ArgumentException(
                $"Type '{interceptorType}' must be a closed reference type assignable to IQueryInterceptor or IConnectionInterceptor.",
                nameof(interceptorType));
        }

        AddEntry(interceptorType);
        return this;
    }

    /// <summary>
    /// Registers an interceptor service key to resolve from the provider captured by this builder when
    /// a context is created; see <see cref="AddInterceptor(Type)"/>.
    /// </summary>
    /// <typeparam name="T">A closed reference type assignable to <see cref="IQueryInterceptor"/> or <see cref="IConnectionInterceptor"/>.</typeparam>
    /// <returns>This builder, to allow chaining.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="T"/> is not a valid interceptor type.</exception>
    /// <exception cref="InvalidOperationException">A context is created on a builder without a captured provider.</exception>
    /// <remarks>
    /// Every registration - an instance or a type - shares one key: the instance's runtime type for an
    /// instance, the supplied <see cref="Type"/> for a type entry. The first registration of a key wins,
    /// including mixed instance/type entries, so a later registration of the same key is ignored.
    /// <para>
    /// <typeparamref name="T"/> is constrained to <see langword="class"/> at compile time; the runtime
    /// check additionally rejects open-generic and non-assignable types immediately.
    /// </para>
    /// </remarks>
    public DataContextBuilder AddInterceptor<T>() where T : class
        => AddInterceptor(typeof(T));

    private void AddEntry(object entry)
    {
        var key = entry is Type type ? type : entry.GetType();
        foreach (var existing in _interceptorEntries)
        {
            var existingKey = existing is Type existingType ? existingType : existing.GetType();
            if (existingKey == key)
                return;
        }

        _interceptorEntries.Add(entry);
    }

    private static bool IsValidInterceptorType(Type interceptorType)
        => !interceptorType.IsValueType
           && !interceptorType.ContainsGenericParameters
           && (typeof(IQueryInterceptor).IsAssignableFrom(interceptorType)
               || typeof(IConnectionInterceptor).IsAssignableFrom(interceptorType));

    /// <summary>
    /// Resolves the interceptor registrations into the two event pipelines, completing the work (and
    /// any missing-service failure) before the context factory runs. The result is cached only for the
    /// duration of one creation; the cached arrays are exposed to the context constructor through
    /// <see cref="QueryInterceptors"/> and <see cref="ConnectionInterceptors"/>.
    /// </summary>
    internal (IQueryInterceptor[] Query, IConnectionInterceptor[] Connection) ResolveInterceptors()
    {
        if (_resolvedQueryInterceptors is not null && _resolvedConnectionInterceptors is not null)
            return (_resolvedQueryInterceptors, _resolvedConnectionInterceptors);

        var query = new List<IQueryInterceptor>();
        var connection = new List<IConnectionInterceptor>();
        foreach (var entry in _interceptorEntries)
        {
            var instance = entry is Type type ? ResolveType(type) : entry;
            if (instance is IQueryInterceptor queryInterceptor)
                query.Add(queryInterceptor);
            if (instance is IConnectionInterceptor connectionInterceptor)
                connection.Add(connectionInterceptor);
        }

        _resolvedQueryInterceptors = [.. query];
        _resolvedConnectionInterceptors = [.. connection];
        return (_resolvedQueryInterceptors, _resolvedConnectionInterceptors);
    }

    private object ResolveType(Type type)
    {
        if (_serviceProvider is null)
            throw new InvalidOperationException($"Interceptor type '{type}' requires a DI-backed DataContextBuilder.");

        return _serviceProvider.GetRequiredService(type);
    }

    internal IQueryInterceptor[] QueryInterceptors => ResolveInterceptors().Query;

    internal IConnectionInterceptor[] ConnectionInterceptors => ResolveInterceptors().Connection;

    /// <summary>
    /// Creates a data context from the current configuration by invoking <see cref="Factory"/>. Any
    /// interceptor service keys are resolved from the captured provider before the factory is invoked,
    /// so a missing registration (or a type entry on a standalone builder) fails before a context is
    /// constructed.
    /// </summary>
    /// <returns>The configured <see cref="IDataContext"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// No factory has been configured on this builder, the provider does not have an interceptor
    /// registration, or a type entry was registered on a builder without a captured provider.
    /// </exception>
    public IDataContext CreateDataContext()
    {
        if (_factory is null)
            throw new InvalidOperationException("Context is not set");

        // Resolution is scoped to this call so a transient interceptor is resolved per creation and
        // never reused across calls; the factory reads the cached arrays through the two properties.
        _resolvedQueryInterceptors = null;
        _resolvedConnectionInterceptors = null;
        ResolveInterceptors();

        return _factory(this);
    }

    // public DataContextOptionsBuilder UseInMemoryClient()
    // {
    //     _dataProvider = new InMemoryDataProvider();
    //     return this;
    // }
}