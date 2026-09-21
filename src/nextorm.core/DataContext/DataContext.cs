using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Linq.Expressions;

namespace NextORM.Core;

public abstract class DataContext : IDataContext, IConnectionManager
{
    private bool _disposed;
    private readonly ContextEnvironment _environment;
    private readonly QueryCache _queryCache;
    private readonly DbConnectionManager _connectionManager;
    private readonly QueryExecutor _executor;
    private readonly QueryPlanner _planner;

    public DataContext(DataContextBuilder optionsBuilder)
        : this(null, null, optionsBuilder)
    {
    }

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

    public ILogger? Logger => _environment.Logger;
    public ILogger? CommandLogger => _environment.CommandLogger;
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
    public Dictionary<string, object> Properties => _environment.Properties;
    public Lazy<QueryCommand<bool>>? AnyCommand
    {
        get => _queryCache.AnyCommand;
        set => _queryCache.AnyCommand = value;
    }

    /// <summary>Whether sensitive parameter values may be logged. Kept for provider subclasses.</summary>
    protected internal bool LogSensitiveData => _environment.LogSensitiveData;

    public EntityBuilder From(string table) => new(this, table) { Logger = CommandLogger };

    public event EventHandler? Disposed;

    public void EnsureConnectionOpen() => _connectionManager.EnsureConnectionOpen();

    public Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
        => _connectionManager.EnsureConnectionOpenAsync(cancellationToken);

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

    public DbCommand CreateCommand(string sql)
    {
        var cmd = _connectionManager.GetConnection().CreateCommand();
        cmd.CommandText = sql;
        return cmd;
    }

    // ExtractParams / IsRuntimeParam / MakeSelect live in QueryPlanner (the planning axis).
    // LogParams and the three GetDbCommand overloads live in QueryExecutor (the execution axis).

    public IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
        => _planner.GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, cancellationToken);

    public abstract DbParameter CreateParam(string name, object? value);

    /// <summary>
    /// Maps a projected column to a reader accessor. Providers whose reader does not widen CLR
    /// types (SqlClient throws when a typed getter does not match the field type, for example an
    /// int column projected as long) can override this to read the value and convert it.
    /// </summary>
    public virtual Expression MapColumnExpression(SelectExpression column, Expression param) => RowMapperFactory.MapColumn(column, param);

    public void ResetPreparation(QueryCommand queryCommand)
        => _planner.ResetPreparation(queryCommand);

    public FromExpression? GetFrom(Type srcType, QueryCommand? queryCommand)
        => _planner.GetFrom(srcType, queryCommand);

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

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

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

    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => _executor.CreateAsyncEnumerator(preparedQueryCommand, @params, cancellationToken);

    public async Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.ToListAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.ToList(preparedQueryCommand, @params);

    // ConvertScalar lives in QueryExecutor together with the scalar terminal (the execution axis).
    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull)
        => _executor.ExecuteScalar(preparedQueryCommand, @params, throwIfNull);

    public async Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
        => await _executor.ExecuteScalar(preparedQueryCommand, @params, throwIfNull, cancellationToken).ConfigureAwait(false);

    public IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
        => _executor.CreateEnumerator(preparedQueryCommand, @params);

    public TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.First(preparedQueryCommand, @params);

    public async Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.FirstAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    public TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.FirstOrDefault(preparedQueryCommand, @params);

    public async Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.FirstOrDefaultAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    public TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.Single(preparedQueryCommand, @params);

    public async Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.SingleAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    public TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.SingleOrDefault(preparedQueryCommand, @params);

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.SingleOrDefaultAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    public void PurgeQueryCache() => _queryCache.PurgeQueryCache();
}
