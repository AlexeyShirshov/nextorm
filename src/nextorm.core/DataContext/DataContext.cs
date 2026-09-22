using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Base class for database-backed contexts. Owns the connection lifecycle, the shared query-plan
/// cache and the execution/planning collaborators, and exposes the terminal operators over prepared
/// commands. Providers derive from it and supply the SQL dialect, the connection factory and
/// parameter creation.
/// </summary>
public abstract class DataContext : IDataContext, IConnectionManager
{
    private bool _disposed;
    private readonly ContextEnvironment _environment;
    private readonly QueryCache _queryCache;
    private readonly DbConnectionManager _connectionManager;
    private readonly QueryExecutor _executor;
    private readonly QueryPlanner _planner;

    /// <summary>
    /// Creates a context from builder options (logger factory, mapping mode, naming convention and
    /// identifier quoting). The connection is created lazily by the provider on first use.
    /// </summary>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    public DataContext(DataContextBuilder optionsBuilder)
        : this(null, null, optionsBuilder)
    {
    }

    /// <summary>
    /// Creates a context with an optional connection string or a caller-supplied connection. A string
    /// produces a connection the context owns and disposes; a supplied connection is used as-is and
    /// is not disposed by the context (the caller keeps ownership).
    /// </summary>
    /// <param name="connectionString">Connection string used to create a context-owned connection, or <see langword="null"/>.</param>
    /// <param name="providedConnection">An already-created connection owned by the caller, or <see langword="null"/>.</param>
    /// <param name="optionsBuilder">The options collected from <c>DataContextBuilder</c>.</param>
    protected DataContext(string? connectionString, DbConnection? providedConnection, DataContextBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        // Ambient state (loggers, mapping mode, property bag) lives in its own collaborator, shared
        // with the in-memory context; the context keeps only its lifecycle and provider hooks.
        _environment = new ContextEnvironment(
            optionsBuilder.LoggerFactory,
            GetType(),
            needMapping: true,
            optionsBuilder.ShouldLogSensitiveData,
            optionsBuilder.QuoteIdentifiers,
            optionsBuilder.NamingConvention);

        _queryCache = new QueryCache(QueryPlanStore.Clear);

        // The connection axis owns the connection state machine; the provider keeps its two hooks on
        // the context and they are passed in as delegates (bound here, never invoked during
        // construction). `connectionString`/`providedConnection` differ in ownership: the context
        // disposes only the connection it creates from the string.
        _connectionManager = new DbConnectionManager(
            this,
            new ConnectionHooks(CreateDbConnection, OnConnectionCreated),
            connectionString,
            providedConnection,
            new LoggingOptions(_environment.Logger, LogSensitiveData: _environment.LogSensitiveData));

        // Bound once: parameter creation is handed to the execution/planning layers as a delegate
        // instead of passing the context itself, so they no longer depend on the concrete DataContext
        // (F6). Binding the abstract method keeps the provider override on the dispatch path, and a
        // field avoids allocating a delegate per command.
        _createParam = CreateParam;

        // The execution axis receives everything through its constructor (connection role, parameter
        // factory, logging config, disposal state) so it never sees the concrete context.
        _executor = new QueryExecutor(
            _connectionManager,
            _createParam,
            new LoggingOptions(_environment.Logger, LogSensitiveData: _environment.LogSensitiveData, LogParams: _environment.LogParams),
            () => _disposed);

        // The planning axis gets the provider hooks as delegates and invokes them lazily: calling the
        // abstract/virtual members here would run derived code before the derived constructor.
        _planner = new QueryPlanner(
            GetDialect,
            GetType(),
            new ProviderHooks(MapColumn, _createParam, CreateCommand),
            new LoggingOptions(_environment.Logger, _environment.ResultSetEnumeratorLogger, _environment.LogSensitiveData));
    }

    private readonly Func<string, object?, DbParameter> _createParam;

    // Late-bound provider hooks: only the delegates are created in the constructor, never the values.
    private ISqlDialect GetDialect() => Dialect;

    private Expression MapColumn(SelectExpression column, Expression param) => MapColumnExpression(column, param);

    /// <summary>
    /// SQL dialect used to render queries. Supplied by the provider; SQL generation depends on this
    /// interface rather than on the context itself.
    /// </summary>
    public abstract ISqlDialect Dialect { get; }

    /// <summary>Logger for the context's own diagnostic messages, or <see langword="null"/> when no logger factory was configured.</summary>
    public ILogger? Logger => _environment.Logger;
    /// <summary>Logger category used to trace executed commands and their parameters.</summary>
    public ILogger? CommandLogger => _environment.CommandLogger;
    /// <summary>Whether projected rows must be materialised into CLR objects.</summary>
    public bool NeedMapping => _environment.NeedMapping;
    /// <summary>
    /// Whether this context quotes physical identifiers by default (set with
    /// <c>DataContextBuilder.UseQuotedIdentifiers</c>). A command can override it with
    /// <c>WithQuotedIdentifiers</c>.
    /// </summary>
    public bool QuoteIdentifiers => _environment.QuoteIdentifiers;
    /// <summary>
    /// Convention applied to auto-derived table and column names by default (set with
    /// <c>DataContextBuilder.UseNamingConvention</c>), or <see langword="null"/> to emit CLR names
    /// verbatim. A command can override it with <c>WithNamingConvention</c>.
    /// </summary>
    public INamingConvention? NamingConvention => _environment.NamingConvention;
    /// <summary>User-owned bag of arbitrary state attached to this context.</summary>
    public Dictionary<string, object> Properties => _environment.Properties;
    /// <summary>
    /// Lazily created, cached <c>Any</c> query for this context, or <see langword="null"/> until it is
    /// first needed. Caching it lets repeated existence checks reuse one prepared command.
    /// </summary>
    public Lazy<QueryCommand<bool>>? AnyCommand
    {
        get => _queryCache.AnyCommand;
        set => _queryCache.AnyCommand = value;
    }

    /// <summary>Whether sensitive parameter values may be logged. Kept for provider subclasses.</summary>
    protected internal bool LogSensitiveData => _environment.LogSensitiveData;

    /// <summary>Starts a query over the raw physical table named <paramref name="table"/>.</summary>
    /// <param name="table">The physical table name to select from.</param>
    /// <returns>A builder over the table.</returns>
    public EntityBuilder From(string table) => new(this, table) { Logger = CommandLogger };

    /// <summary>Raised after the context has released its connection during disposal.</summary>
    public event EventHandler? Disposed;

    /// <summary>Opens the connection if it is not already open.</summary>
    public void EnsureConnectionOpen() => _connectionManager.EnsureConnectionOpen();

    /// <summary>Opens the connection if it is not already open, honouring <paramref name="cancellationToken"/> while opening.</summary>
    /// <param name="cancellationToken">Token used to cancel a slow connection open.</param>
    /// <returns>A task that completes once the connection is open.</returns>
    public Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
        => _connectionManager.EnsureConnectionOpenAsync(cancellationToken);

    /// <summary>Returns the connection in use, creating it on first access (which may throw if the connection string is missing).</summary>
    /// <returns>The underlying database connection.</returns>
    public DbConnection GetConnection() => _connectionManager.GetConnection();

    /// <summary>Creates the provider-specific connection. Called only when no connection was supplied.</summary>
    protected abstract DbConnection CreateDbConnection(string? connectionString);

    /// <summary>
    /// Called for every connection the context starts using (created or supplied) so a provider can
    /// run provider-specific setup, e.g. registering custom functions.
    /// </summary>
    protected virtual void OnConnectionCreated(DbConnection connection)
    {
    }

    /// <summary>The connection string of the supplied connection, or the one the context was built with.</summary>
    public string ConnectionString => _connectionManager.ConnectionString;

    /// <summary>Creates a command bound to the current connection with <paramref name="sql"/> as its text. The caller owns the returned command.</summary>
    /// <param name="sql">The SQL text to execute.</param>
    /// <returns>A new command on the current connection.</returns>
    public DbCommand CreateCommand(string sql)
    {
        var cmd = _connectionManager.GetConnection().CreateCommand();
        cmd.CommandText = sql;
        return cmd;
    }

    // ExtractParams / IsRuntimeParam / MakeSelect live in QueryPlanner (the planning axis).
    // LogParams and the three GetDbCommand overloads live in QueryExecutor (the execution axis).

    /// <summary>
    /// Prepares <paramref name="queryCommand"/> against the current dialect, optionally building a
    /// streaming enumerator and storing the plan in the cache. When <paramref name="storeInCache"/> is
    /// set, later calls return the cached plan instead of re-planning.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to prepare.</param>
    /// <param name="createEnumerator">When <see langword="true"/>, compiles a streaming row enumerator as part of preparation.</param>
    /// <param name="storeInCache">When <see langword="true"/>, stores the prepared command in the plan cache.</param>
    /// <param name="cancellationToken">Token used to cancel preparation.</param>
    /// <returns>The prepared command, ready to execute.</returns>
    public IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
        => _planner.GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, cancellationToken);

    /// <summary>Creates a provider-specific parameter with the given name and value.</summary>
    /// <param name="name">The parameter name, without the provider's prefix.</param>
    /// <param name="value">The parameter value, or <see langword="null"/>.</param>
    /// <returns>A new database parameter.</returns>
    public abstract DbParameter CreateParam(string name, object? value);

    /// <summary>
    /// Maps a projected column to a reader accessor. Providers whose reader does not widen CLR
    /// types (SqlClient throws when a typed getter does not match the field type, for example an
    /// int column projected as long) can override this to read the value and convert it.
    /// </summary>
    public virtual Expression MapColumnExpression(SelectExpression column, Expression param) => RowMapperFactory.MapColumn(column, param);

    /// <summary>Resets the cached execution plan of <paramref name="queryCommand"/> so it is rebuilt on next use.</summary>
    /// <param name="queryCommand">The command whose plan should be discarded.</param>
    public void ResetPreparation(QueryCommand queryCommand)
        => _planner.ResetPreparation(queryCommand);

    /// <summary>Resolves the FROM source for <paramref name="srcType"/>, reusing the source carried by <paramref name="queryCommand"/> when present.</summary>
    /// <param name="srcType">The entity or source type to resolve.</param>
    /// <param name="queryCommand">The derived command whose source should be used, or <see langword="null"/>.</param>
    /// <returns>The resolved FROM expression, or <see langword="null"/> when the type is not mapped.</returns>
    public FromExpression? GetFrom(Type srcType, QueryCommand? queryCommand)
        => _planner.GetFrom(srcType, queryCommand);

    /// <summary>
    /// Releases the connection owned by the context and raises <see cref="Disposed"/>. Called from
    /// <see cref="Dispose()"/>; <paramref name="disposing"/> is <see langword="true"/> for managed
    /// cleanup.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> when invoked from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                DisposeStaff();
            }

            _disposed = true;
        }
    }

    /// <summary>Closes the context-owned connection and raises <see cref="Disposed"/>. Safe to call more than once.</summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Asynchronously releases the context. Delegates to <see cref="Dispose()"/> so cleanup runs exactly once.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        // Route through Dispose() so the _disposed guard is honored and cleanup runs
        // exactly once, matching the synchronous disposal path (CA1816).
        Dispose();

        return ValueTask.CompletedTask;
    }

    private void DisposeStaff()
    {
        _connectionManager.DisposeConnection();
        Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Creates an async enumerator that streams the prepared command's rows.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to enumerate.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel enumeration.</param>
    /// <returns>An async enumerator over the result rows.</returns>
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => _executor.CreateAsyncEnumerator(preparedQueryCommand, @params, cancellationToken);

    /// <summary>Executes the command and buffers all rows into a list.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The materialised rows; empty when the query yields none.</returns>
    public async Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.ToListAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    /// <summary>Executes the command and buffers all rows into a list.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <returns>The materialised rows; empty when the query yields none.</returns>
    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.ToList(preparedQueryCommand, @params);

    // ConvertScalar lives in QueryExecutor together with the scalar terminal (the execution axis).
    /// <summary>Executes the command and converts the first column of the first row to <typeparamref name="TResult"/>.</summary>
    /// <typeparam name="TResult">The scalar type to convert the value to.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <param name="throwIfNull">When <see langword="true"/>, throws <see cref="InvalidOperationException"/> when the result is null or <c>DBNull</c>; otherwise returns <see langword="default"/>.</param>
    /// <returns>The scalar value, or <see langword="default"/> when the result is null and <paramref name="throwIfNull"/> is <see langword="false"/>.</returns>
    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull)
        => _executor.ExecuteScalar(preparedQueryCommand, @params, throwIfNull);

    /// <summary>Executes the command and converts the first column of the first row to <typeparamref name="TResult"/>.</summary>
    /// <typeparam name="TResult">The scalar type to convert the value to.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="throwIfNull">When <see langword="true"/>, throws <see cref="InvalidOperationException"/> when the result is null or <c>DBNull</c>; otherwise returns <see langword="default"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The scalar value, or <see langword="default"/> when the result is null and <paramref name="throwIfNull"/> is <see langword="false"/>.</returns>
    public async Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
        => await _executor.ExecuteScalar(preparedQueryCommand, @params, throwIfNull, cancellationToken).ConfigureAwait(false);

    /// <summary>Creates a synchronous enumerator over the prepared command's rows.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to enumerate.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <returns>A synchronous enumerator over the result rows.</returns>
    public IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
        => _executor.CreateEnumerator(preparedQueryCommand, @params);

    /// <summary>Returns the first row of the result.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <returns>The first projected row.</returns>
    /// <exception cref="InvalidOperationException">The result set is empty.</exception>
    public TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.First(preparedQueryCommand, @params);

    /// <summary>Asynchronously returns the first row of the result.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The first projected row.</returns>
    /// <exception cref="InvalidOperationException">The result set is empty.</exception>
    public async Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.FirstAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    /// <summary>Returns the first row of the result, or <see langword="default"/> when the result set is empty.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <returns>The first projected row, or <see langword="default"/> when there is none.</returns>
    public TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.FirstOrDefault(preparedQueryCommand, @params);

    /// <summary>Asynchronously returns the first row of the result, or <see langword="default"/> when the result set is empty.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The first projected row, or <see langword="default"/> when there is none.</returns>
    public async Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.FirstOrDefaultAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    /// <summary>Returns the only row of the result.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <returns>The single projected row.</returns>
    /// <exception cref="InvalidOperationException">The result set is empty or contains more than one row.</exception>
    public TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.Single(preparedQueryCommand, @params);

    /// <summary>Asynchronously returns the only row of the result.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The single projected row.</returns>
    /// <exception cref="InvalidOperationException">The result set is empty or contains more than one row.</exception>
    public async Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.SingleAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    /// <summary>Returns the only row of the result, or <see langword="default"/> when the result set is empty.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command.</param>
    /// <returns>The single projected row, or <see langword="default"/> when there is none.</returns>
    /// <exception cref="InvalidOperationException">The result set contains more than one row.</exception>
    public TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.SingleOrDefault(preparedQueryCommand, @params);

    /// <summary>Asynchronously returns the only row of the result, or <see langword="default"/> when the result set is empty.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="preparedQueryCommand">The prepared command to execute.</param>
    /// <param name="params">Positional parameters bound to the command, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The single projected row, or <see langword="default"/> when there is none.</returns>
    /// <exception cref="InvalidOperationException">The result set contains more than one row.</exception>
    public async Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.SingleOrDefaultAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    /// <summary>Clears all cached query plans held by this context.</summary>
    public void PurgeQueryCache() => _queryCache.PurgeQueryCache();
}
