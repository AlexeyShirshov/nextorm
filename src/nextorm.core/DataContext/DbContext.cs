using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public abstract class DbContext : IDataContext, IConnectionManager
{
    private readonly Dictionary<string, object> _properties = [];
    private bool _disposed;
    private readonly DbConnectionManager _connectionManager;
    private readonly QueryExecutor _executor;
    private readonly QueryPlanner _planner;
    public DbContext(DbContextBuilder optionsBuilder)
        : this(null, null, optionsBuilder)
    {
    }

    protected DbContext(string? connectionString, DbConnection? providedConnection, DbContextBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        ILogger? resultSetEnumeratorLogger = null;
        var logParams = false;
        if (optionsBuilder.LoggerFactory is not null)
        {
            Logger = optionsBuilder.LoggerFactory.CreateLogger(GetType());
            CommandLogger = optionsBuilder.LoggerFactory.CreateLogger(typeof(QueryCommand));
            resultSetEnumeratorLogger = optionsBuilder.LoggerFactory.CreateLogger("nextorm.core.ResultSetEnumerator");
            logParams = Logger.IsEnabled(LogLevel.Debug);
        }

        LogSensitiveData = optionsBuilder.ShouldLogSensitiveData;
        // CacheExpressions = optionsBuilder.CacheExpressions;

        // The connection axis owns the connection state machine; the provider keeps its two hooks on
        // the context and they are passed in as delegates (bound here, never invoked during
        // construction). `connectionString`/`providedConnection` differ in ownership: the context
        // disposes only the connection it creates from the string.
        _connectionManager = new DbConnectionManager(
            this,
            CreateDbConnection,
            OnConnectionCreated,
            connectionString,
            providedConnection,
            Logger,
            LogSensitiveData);

        // Bound once: parameter creation is handed to the execution/planning layers as a delegate
        // instead of passing the context itself, so they no longer depend on the concrete DbContext
        // (F6). Binding the abstract method keeps the provider override on the dispatch path, and a
        // field avoids allocating a delegate per command.
        _createParam = CreateParam;

        // The execution axis receives everything through its constructor (connection role, parameter
        // factory, logging config, disposal state) so it never sees the concrete context.
        _executor = new QueryExecutor(_connectionManager, _createParam, Logger, logParams, LogSensitiveData, () => _disposed);

        // The planning axis gets the provider hooks as delegates and invokes them lazily: calling the
        // abstract/virtual members here would run derived code before the derived constructor.
        _planner = new QueryPlanner(
            GetDialect,
            Logger,
            GetType(),
            MapColumn,
            _createParam,
            CreateCommand,
            resultSetEnumeratorLogger,
            LogSensitiveData);
    }
    private readonly Func<string, object?, DbParameter> _createParam;

    // Late-bound provider hooks: only the delegates are created in the constructor, never the values.
    private ISqlDialect GetDialect() => Dialect;

    private Expression MapColumn(SelectExpression column, Expression param) => MapColumnExpression(column, param);
    #region Properties
    protected internal readonly bool LogSensitiveData;
    /// <summary>
    /// SQL dialect used to render queries. Supplied by the provider; SQL generation depends on this
    /// interface rather than on the context itself.
    /// </summary>
    public abstract ISqlDialect Dialect { get; }
    public ILogger? Logger { get; }
    public ILogger? CommandLogger { get; }
    public bool NeedMapping => true;
    public Dictionary<string, object> Properties => _properties;
    public Lazy<QueryCommand<bool>>? AnyCommand { get; set; }
    // public bool CacheExpressions { get; set; }

    public EntityBuilder From(string table) => new(this, table) { Logger = CommandLogger };
    #endregion
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
    // public DbCommand GetCommand(string sql)
    // {
    //     if (_cmd is null)
    //         _cmd=CreateCommand(sql);

    //     return _cmd;
    // }
    // public void ReturnCommand(DbCommand dbCommand)
    // {
    //     _cmd.CommandText=string.Empty;
    //     _cmd.Parameters.Clear();
    // }
    public DbCommand CreateCommand(string sql)
    {
        var cmd = _connectionManager.GetConnection().CreateCommand();
        cmd.CommandText = sql;
        return cmd;
    }

    // ExtractParams / IsRuntimeParam / MakeSelect moved to QueryPlanner (the planning axis).

    // LogParams and the three GetDbCommand overloads moved to QueryExecutor (the execution axis).
    // private DbCompiledQuery<TResult> GetCompiledQuery<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, out DbConnection conn, out DbCommand sqlCommand)
    // {
    //     var compiledQuery = queryCommand._compiledQuery as DbCompiledQuery<TResult> ?? CreateCompiledQuery(queryCommand, false, true);

    //     // #if DEBUG
    //     //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
    //     // #endif
    //     sqlCommand = compiledQuery.DbCommand;
    //     if (@params is not null)
    //     {
    //         var parameters = sqlCommand.Parameters;
    //         for (var i = 0; i < @params.Length; i++)
    //         {
    //             //var paramName = i < 5 ? _params[i] : string.Format("norm_p{0}", i);
    //             parameters[0].Value = @params[i];
    //             //sqlCommand.Parameters[paramName].Value = @params[i];
    //             //var added = false;
    //             // var idx = parameters.IndexOf(paramName);
    //             // if (idx >= 0)
    //             //     parameters[idx].Value = @params[i];
    //             // else
    //             //     parameters.Add(CreateParam(paramName, @params[i]));
    //             // for (var j = 0; j < parameters.Count; j++)
    //             // {
    //             //     var p = sqlCommand.Parameters[j];
    //             //     if (p.ParameterName == paramName)
    //             //     {
    //             //         p.Value = @params[i];
    //             //         added = true;
    //             //         break;
    //             //     }
    //             // }
    //             // if (!added)
    //             //     sqlCommand.Parameters.Add(CreateParam(paramName, @params[i]));
    //         }
    //     }
    //     conn = null;
    //     //conn = sqlCommand.Connection!;
    //     // conn = GetConnection();
    //     // sqlCommand.Connection = conn;
    //     return compiledQuery;
    // }

    public IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
        => _planner.GetPreparedQueryCommand(queryCommand, createEnumerator, storeInCache, cancellationToken);
    // public DatabaseCompiledQuery<TResult> GetCompiledQuery<TResult>(QueryCommand<TResult> cmd)
    // {
    //     var compiled = cmd.Compiled as DatabaseCompiledQuery<TResult>;
    //     if (compiled is not null)
    //         return compiled;

    //     if (!cmd.Cache || !_cmdIdx.TryGetValue(cmd, out var cacheEntry))
    //     {
    //         cacheEntry = new SqlCacheEntry(CreateCompiledQuery(cmd));

    //         if (cmd.Cache)
    //             _cmdIdx[cmd] = cacheEntry;
    //     }

    //     return (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery;
    // }
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

    // public IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(string sql, object? @params, QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken)
    // {
    //     // if (!queryCommand.IsPrepared)
    //     //     queryCommand.PrepareCommand(!storeInCache, cancellationToken);

    //     return GetPreparedQueryCommand(queryCommand, !nonStreamUsing, storeInCache, cancellationToken, sql, () =>
    //     {
    //         List<Param> ps = new();
    //         if (@params is not null)
    //         {
    //             var t = @params.GetType();
    //             foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
    //             {
    //                 ps.Add(new Param(prop.Name, prop.GetValue(@params)));
    //             }
    //         }
    //         return ps;
    //     });
    // }
    // public void Compile<TResult>(QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken)
    // {
    //     if (!queryCommand.IsPrepared)
    //         queryCommand.PrepareCommand(!storeInCache, cancellationToken);

    //     queryCommand._compiledQuery = GetPreparedQueryCommand(queryCommand, !nonStreamUsing, storeInCache);
    // }
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
    // public async Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    // {
    //     //ArgumentNullException.ThrowIfNull(queryCommand);

    //     var compiledQuery = queryCommand._compiledQuery as DbCompiledQuery<TResult> ?? CreateCompiledQuery(queryCommand, true, true);

    //     var sqlEnumerator = compiledQuery.Enumerator!;
    //     await sqlEnumerator.InitReaderAsync(@params, cancellationToken).ConfigureAwait(false);
    //     return sqlEnumerator;
    // }
    public async Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => await _executor.ToListAsync(preparedQueryCommand, @params, cancellationToken).ConfigureAwait(false);

    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
        => _executor.ToList(preparedQueryCommand, @params);
    // private (DbDataReader, DbCompiledQuery<TResult>) CreateReader<TResult>(QueryCommand<TResult> queryCommand, object[]? @params)
    // {
    //     var compiledQuery = GetCompiledQuery(queryCommand, @params, out var conn, out var sqlCommand);

    //     //if (conn.State == ConnectionState.Closed)
    //     if (!_connOpen)
    //     {
    //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
    //         conn.Open();
    //         _connOpen = true;
    //     }

    //     //_logParams?.Invoke(sqlCommand);

    //     return (sqlCommand.ExecuteReader(compiledQuery.Behavior), compiledQuery);
    // }
    // ConvertScalar moved to QueryExecutor together with the scalar terminal (the execution axis).
    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull)
        => _executor.ExecuteScalar(preparedQueryCommand, @params, throwIfNull);

    public async Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
        => await _executor.ExecuteScalar(preparedQueryCommand, @params, throwIfNull, cancellationToken).ConfigureAwait(false);

    // private async Task<(DbDataReader, DbCompiledQuery<TResult>)> ExecuteReader<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    // {
    //     var compiledQuery = GetCompiledQuery(queryCommand, @params, out var conn, out var sqlCommand);

    //     //if (conn.State == ConnectionState.Closed)
    //     if (!_connOpen)
    //     {
    //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
    //         await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
    //         _connOpen = true;
    //     }

    //     _logParams?.Invoke(sqlCommand);

    //     return (await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false), compiledQuery);
    // }
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

    public void PurgeQueryCache()
    {
        QueryPlanStore.Clear();
    }
    // class EmptyEnumerator<TResult> : IAsyncEnumerator<TResult>, IEnumerator<TResult>
    // {
    //     public TResult Current => default;

    //     object IEnumerator.Current => default;

    //     public void Dispose()
    //     {

    //     }

    //     public ValueTask DisposeAsync()
    //     {
    //         //throw new NotImplementedException();
    //         return ValueTask.CompletedTask;
    //     }

    //     public bool MoveNext()
    //     {
    //         return false;
    //     }

    //     public ValueTask<bool> MoveNextAsync()
    //     {
    //         return ValueTask.FromResult(false);
    //     }

    //     public void Reset()
    //     {

    //     }
    // }
}
