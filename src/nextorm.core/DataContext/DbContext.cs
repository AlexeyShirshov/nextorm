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
    //private readonly static ConcurrentDictionary<Expression, List<SelectExpression>> _selectListExpCache = new(ExpressionEqualityComparer.Instance);
    //private static readonly AsyncLocal<Dictionary<QueryPlan, IDbCommandHolder>> _queryPlanCache = new() { Value = [] };    
    [ThreadStatic]
    private static Dictionary<QueryPlanCacheKey, IDbCommandHolder>? _queryPlanCache;

    // Compiled commands are cached per thread but shared by every context running on that thread,
    // so the provider (context type) has to be part of the key: the same query produces different
    // SQL and different DbCommand implementations for SQLite, PostgreSQL, SQL Server, etc.
    private readonly record struct QueryPlanCacheKey(Type ContextType, QueryPlan Plan);
    private readonly Dictionary<string, object> _properties = [];
    private DbConnection? _conn;
    protected bool _connWasCreatedByMe;
    private bool _disposed;
    internal bool _connOpen;
    private readonly bool _logParams;
    // Connection source supplied by the provider: either a connection string (the context owns and
    // disposes the connection it creates from it) or an already-built connection (the caller owns it).
    private readonly string? _connectionString;
    private readonly DbConnection? _providedConnection;
    public DbContext(DbContextBuilder optionsBuilder)
        : this(null, null, optionsBuilder)
    {
    }

    protected DbContext(string? connectionString, DbConnection? providedConnection, DbContextBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        _connectionString = connectionString;
        _providedConnection = providedConnection;

        if (optionsBuilder.LoggerFactory is not null)
        {
            Logger = optionsBuilder.LoggerFactory.CreateLogger(GetType());
            CommandLogger = optionsBuilder.LoggerFactory.CreateLogger(typeof(QueryCommand));
            ResultSetEnumeratorLogger = optionsBuilder.LoggerFactory.CreateLogger("nextorm.core.ResultSetEnumerator");
            _logParams = Logger.IsEnabled(LogLevel.Debug);
        }

        LogSensitiveData = optionsBuilder.ShouldLogSensitiveData;
        // CacheExpressions = optionsBuilder.CacheExpressions;

        // Bound once: parameter creation is handed to the execution path as a delegate instead of
        // passing the context itself, so the command/enumerator layer no longer depends on the
        // concrete DbContext (F6). Binding the abstract method keeps the provider override on the
        // dispatch path, and a field avoids allocating a delegate per command.
        _createParam = CreateParam;
    }
    private readonly Func<string, object?, DbParameter> _createParam;
    #region Properties
    protected internal readonly bool LogSensitiveData;
    /// <summary>
    /// SQL dialect used to render queries. Supplied by the provider; SQL generation depends on this
    /// interface rather than on the context itself.
    /// </summary>
    public abstract ISqlDialect Dialect { get; }
    public ILogger? Logger { get; }
    public ILogger? CommandLogger { get; }
    internal readonly ILogger? ResultSetEnumeratorLogger;
    public bool NeedMapping => true;
    private static Dictionary<QueryPlanCacheKey, IDbCommandHolder> QueryPlanCache => _queryPlanCache ??= [];
    public Dictionary<string, object> Properties => _properties;
    public Lazy<QueryCommand<bool>>? AnyCommand { get; set; }
    // public bool CacheExpressions { get; set; }

    public Entity From(string table) => new(this, table) { Logger = CommandLogger };
    #endregion
    public event EventHandler? Disposed;
    public void EnsureConnectionOpen()
    {
        var conn = GetConnection();
        if (conn.State == ConnectionState.Closed)
        {

            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
            conn.Open();
        }

        _connOpen = true;
    }
    public async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
    {
        var conn = GetConnection();
        if (conn.State == ConnectionState.Closed)
        {
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
            await conn.OpenAsync(cancellationToken);
        }

        _connOpen = true;
    }
    public DbConnection GetConnection()
    {
        if (_conn is null)
        {
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
            _connWasCreatedByMe = true;
            _conn = CreateConnection();
            _conn.StateChange += OnStateChanged;
            if (!_connWasCreatedByMe)
                _conn.Disposed += ConnDisposed;
        }

        return _conn;
    }
    private void OnStateChanged(object sender, StateChangeEventArgs args) => _connOpen = args.CurrentState == ConnectionState.Open;
    private void ConnDisposed(object? sender, EventArgs e)
    {
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Connection disposed");
        if (sender is DbConnection conn)
        {
            conn.Disposed -= ConnDisposed;
            conn.StateChange -= OnStateChanged;

            foreach (var cached in QueryPlanCache.Values)
            {
                cached.ResetConnection(conn, this);
            }
        }
    }
    /// <summary>
    /// Resolves the connection to use, creating it on first access. The shared skeleton — logging,
    /// the caller-supplied connection, event wiring and ownership tracking — lives here; a provider
    /// only supplies the concrete connection via <see cref="CreateDbConnection"/> and, if it needs to,
    /// initialises it in <see cref="OnConnectionCreated"/>.
    /// </summary>
    public virtual DbConnection CreateConnection()
    {
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            if (LogSensitiveData)
                Logger.LogDebug("Creating connection with {connStr}", _connectionString);
            else
                Logger.LogDebug("Creating connection");
        }

        if (_providedConnection is not null)
        {
            // Caller-owned: the context must not dispose it at shutdown.
            _connWasCreatedByMe = false;
            OnConnectionCreated(_providedConnection);
            return _providedConnection;
        }

        var conn = CreateDbConnection(_connectionString);
        OnConnectionCreated(conn);
        return conn;
    }

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
    public string ConnectionString => string.IsNullOrEmpty(_connectionString)
        ? _providedConnection!.ConnectionString
        : _connectionString!;
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
        var cmd = GetConnection().CreateCommand();
        cmd.CommandText = sql;
        return cmd;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<Param> ExtractParams(QueryCommand queryCommand)
    {
        var @params = new List<Param>();
        MakeSelect(queryCommand, true, @params, queryCommand, null);
        return @params;
    }

    // NORM.Param placeholders carry no value - the runtime values are applied by
    // DbPreparedQueryCommand.GetDbCommand. Only computed parameters (captured variables,
    // closures) need to be re-extracted on every cached execution.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRuntimeParam(string name) => NormParam.IsName(name);

    private string? MakeSelect(QueryCommand queryCommand, bool paramMode, List<Param> @params, IQueryProvider queryProvider, IAliasProvider? aliasProvider)
    {
        var sqlBuilder = new SqlBuilder(Dialect, paramMode, @params, new DefaultColumnsProvider(), queryProvider, new DefaultParamProvider(), aliasProvider, Logger!);
        return sqlBuilder.MakeSelect(queryCommand);
    }

    private void LogParams(DbCommand sqlCommand)
    {
        Logger!.LogDebug("Executing query: {sql}", sqlCommand.CommandText);

        if (LogSensitiveData)
        {
            foreach (DbParameter p in sqlCommand.Parameters)
            {
                Logger!.LogDebug("param {name} is {value}", p.ParameterName, p.Value);
            }
        }
        else if (sqlCommand.Parameters?.Count > 0)
        {
            Logger!.LogDebug("Use {method} to see param values", nameof(LogSensitiveData));
        }
    }
    private DbCommand GetDbCommand<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, object[]? @params)
        => GetDbCommand(compiledQuery, @params is null ? ReadOnlySpan<object?>.Empty : @params);

    private DbCommand GetDbCommand<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, ReadOnlySpan<object?> @params)
    {
        // #if DEBUG
        //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
        // #endif

        // The open-connection block used to be duplicated here; the role now owns it.
        EnsureConnectionOpen();
        var conn = GetConnection();

        var cmd = compiledQuery.GetDbCommand(@params, _createParam, conn);

        if (_logParams) LogParams(cmd);

        return cmd;
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "A pooled connection may be open or closed depending on prior use, so the connection-state check is reachable on both sides; the analyzer cannot assume state across the pool.")]
    private async ValueTask<DbCommand> GetDbCommand<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, object[]? @params, CancellationToken cancellationToken)
    {
        //var compiledQuery = queryCommand._compiledQuery as DbCompiledQuery<TResult> ?? CreateCompiledQuery(queryCommand, false, true);

        // #if DEBUG
        //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
        // #endif

        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
        var conn = GetConnection();

        var cmd = compiledQuery.GetDbCommand(@params, _createParam, conn);
        // var sqlCommand = compiledQuery.DbCommand;
        // sqlCommand.Connection = conn;
        if (_logParams) LogParams(cmd);

        return cmd;
    }
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
    {
        QueryPlan? queryPlan = null;
        IDbCommandHolder? planCache = null;

        if (!queryCommand.IsPrepared) queryCommand.PrepareCommand(!storeInCache, cancellationToken);
        else queryCommand.RefreshInValuesShape();

        var ext = queryCommand.CustomData as DbQueryCommandExtension;

        if (queryCommand.Cache && storeInCache)
        {
            queryPlan = new QueryPlan(queryCommand, ext?.ManualSql);
            QueryPlanCache.TryGetValue(new QueryPlanCacheKey(GetType(), queryPlan), out planCache);
        }

        if (planCache is null)
        {
            if ((Logger?.IsEnabled(LogLevel.Debug) ?? false) && storeInCache && queryCommand.Cache)
                Logger.LogDebug("Query plan cache miss with hash: {hash}", queryPlan!.GetHashCode());

            var (sql, @params) = ext is null
                ? MakeSelectInternal()
                : (ext.ManualSql, ext.MakeParams?.Invoke());

            // var (sql, @params) = MakeSelect(queryCommand, true);
            // var cacheEntryX = new SqlCacheEntry(null) { Enumerator = new EmptyEnumerator<TResult>() };
            // if (queryCommand.Cache)
            //     _queryCache[queryCommand] = cacheEntryX;
            // return cacheEntryX;

            Func<IDataRecord, TResult>? map = queryCommand.SingleRow && queryCommand.OneColumn
                ? null
                : GetMapCached(queryCommand, sql);

            var noParams = !(@params?.Count > 0);
            var needsParamRefresh = false;
            if (!noParams)
            {
                var parameterList = @params!;
                for (var i = 0; i < parameterList.Count; i++)
                {
                    if (!IsRuntimeParam(parameterList[i].Name))
                    {
                        needsParamRefresh = true;
                        break;
                    }
                }
            }

            var dbCommand = CreateCommand(sql!);
            if (!noParams)
            {
                var parameterList = @params!;
                for (var i = 0; i < parameterList.Count; i++)
                {
                    var p = parameterList[i];
                    dbCommand.Parameters.Add(CreateParam(p.Name, p.Value));
                }
            }

            // dbCommand.Connection = GetConnection();

            //dbCommand.Prepare();
            //return new SqlCacheEntry(null) { Enumerator = new EmptyEnumerator<TResult>() };

            var compiledQuery = new DbPreparedQueryCommand<TResult>(dbCommand, map, queryCommand.SingleRow, ext is null ? sql! : null, noParams, needsParamRefresh);

            if (createEnumerator)
            {
                var enumerator = CreateResultSetEnumerator(compiledQuery!);
                compiledQuery.Enumerator = enumerator;
            }

            if (storeInCache && queryCommand.Cache)
            {
                QueryPlanCache[new QueryPlanCacheKey(GetType(), queryPlan!.GetCacheVersion())] = compiledQuery;
            }

            return compiledQuery;
        }
        else //if (planCache.CompiledQuery is DatabaseCompiledPlan<TResult> plan)
        {
#if DEBUG
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query plan cache hit");
#endif

            var compiledQuery = (DbPreparedQueryCommand<TResult>)planCache;

            if (queryCommand.CustomData is DbQueryCommandExtension)
            {
                Debug.Assert(string.IsNullOrEmpty(compiledQuery.SqlStmt), "SqlStmt must be null");

                // var (sql, @params) = (manualSql, makeParams());

                // var dbCommand = compiledQuery?.DbCommand;
                // if (dbCommand is not null && dbCommand.CommandText == sql && compiledQuery!.DbCommandParams.Count == @params?.Count)
                // {
                //     // TODO: thread safety!
                //     for (int i = 0; i < @params.Count; i++)
                //     {
                //         dbCommand.Parameters[i].Value = @params[i].Value;
                //         Debug.Assert(dbCommand.Parameters[i].ParameterName == @params[i].Name, $"ParameterName {dbCommand.Parameters[i].ParameterName} not equals {@params[i].Name}");
                //     }
                // }
                // else
                // {
                //     dbCommand = CreateCommand(sql!);

                //     if (@params?.Count > 0)
                //         dbCommand.Parameters.AddRange(@params.Select(it => CreateParam(it.Name, it.Value)).ToArray());

                //     compiledQuery = new DbCompiledQuery<TResult>(dbCommand, plan.MapDelegate, queryCommand.SingleRow);
                // }
            }
            else if (string.IsNullOrEmpty(compiledQuery.SqlStmt))
            {
                Debug.Fail("SqlStmt must be not null");
                // var (sql, @params) = MakeSelect(queryCommand, false);

                // var dbCommand = CreateCommand(sql!);

                // if (@params?.Count > 0)
                //     dbCommand.Parameters.AddRange(@params.Select(it => CreateParam(it.Name, it.Value)).ToArray());

                // compiledQuery = new DbCompiledQuery<TResult>(dbCommand, plan.MapDelegate, queryCommand.SingleRow);
            }
            else
            {
                if (!compiledQuery.NoParams && compiledQuery.NeedsParamRefresh/* || compiledQuery.DbCommand is null*/)
                {
                    var dbCommandParams = compiledQuery.DbCommandParams;// ?? CreateCommand(compiledQuery.SqlStmt);

                    // if (!compiledQuery.NoParams)
                    // {
                    // if (compiledQuery?.DbCommand is not null)
                    //     dbCommand.Parameters.Clear();

                    // dbCommand.Parameters.AddRange(ExtractParams(queryCommand).Select(it => CreateParam(it.Name, it.Value)).ToArray());
                    var pp = ExtractParams(queryCommand);
                    for (int i = 0; i < pp.Count; i++)
                    {
                        // Normalize null to DBNull for providers that reject null parameter values.
                        dbCommandParams[i].Value = pp[i].Value ?? DBNull.Value;
                        Debug.Assert(dbCommandParams[i].ParameterName == pp[i].Name, $"ParameterName {dbCommandParams[i].ParameterName} not equals {pp[i].Name}");
                    }
                    //}

                    //compiledQuery ??= new DbCompiledQuery<TResult>(dbCommand, plan.MapDelegate, queryCommand.SingleRow);
                }
            }

            // A plan can be stored by a buffered terminal (createEnumerator: false) and later requested
            // by a streaming one, so the enumerator has to be created on demand. The plan cache is
            // [ThreadStatic], so this entry is only ever touched by the current thread.
            if (createEnumerator && compiledQuery.Enumerator is null)
                compiledQuery.Enumerator = CreateResultSetEnumerator(compiledQuery);

            return compiledQuery;
        }
        (string?, List<Param>) MakeSelectInternal()
        {
            var @params = new List<Param>();
            var aliasProvider = new DefaultAliasProvider();
            return (MakeSelect(queryCommand, false, @params, queryCommand, aliasProvider), @params);
        }
    }
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

    private static string GetAliasFromProjection(Type entityType, Type declaringType, int from)
    {
        int idx = 0;
        foreach (var prop in entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (++idx > from && prop.PropertyType == declaringType)
                return prop.Name;
        }

        throw new BuildSqlCommandException($"Cannot find alias of type {declaringType} in {entityType}");
    }
    /// <summary>
    /// Maps a projected column to a reader accessor. Providers whose reader does not widen CLR
    /// types (SqlClient throws when a typed getter does not match the field type, for example an
    /// int column projected as long) can override this to read the value and convert it.
    /// </summary>
    public virtual Expression MapColumnExpression(SelectExpression column, Expression param) => RowMapperFactory.MapColumn(column, param);
    public void ResetPreparation(QueryCommand queryCommand)
    {
        //_clearCache = true;
    }
    // A table source is immutable, so one instance can be shared by every query (and cached plan)
    // that selects from the entity. This removes a small allocation and a metadata lookup per join.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, FromExpression> _fromCache = new();

    public FromExpression GetFrom(Type t)
    {
        if (_fromCache.TryGetValue(t, out var cached))
            return cached;

        if (DataContextCache.Metadata.TryGetValue(t, out var entity) && !string.IsNullOrEmpty(entity.TableName))
        {
            var from = new FromExpression(entity.TableName);
            _fromCache.TryAdd(t, from);
            return from;
        }

        // Table names come from the entity metadata, registered the first time the entity is
        // materialized (Create<T>()/Entity<T>). Reaching this point means the type was never
        // registered, so a table name cannot be produced.
        throw new BuildSqlCommandException(
            $"Table name is not registered for type {t}. Materialize the entity first (for example with {nameof(DataContextExtensions.Create)}<{t.Name}>()) so its metadata is registered.");
    }

    public FromExpression? GetFrom(Type srcType, QueryCommand? queryCommand)
    {
        if (srcType != typeof(TableAlias))
        {
            if (queryCommand?.Joins?.Length > 0 && srcType.IsAssignableTo(typeof(IProjection)))
            {
                var prop_t1 = srcType.GetProperty("t1") ?? throw new BuildSqlCommandException($"Projection {srcType} must have t1 property");

                var f = GetFrom(prop_t1.PropertyType);

                // f.TableAlias = "t1";

                return f;
            }
            else
                return GetFrom(srcType);
        }
        else //throw new BuildSqlCommandException($"From must be specified for {nameof(TableAlias)} as source type");
            return null;
    }

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
        if (_conn is not null)
        {
            _conn.StateChange -= OnStateChanged;

            if (_connWasCreatedByMe)
            {
                foreach (var cached in QueryPlanCache.Values)
                {
                    cached.ResetConnection(_conn, this);
                }

                if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Dispose connection");
                _conn.Dispose();
                _conn = null;
            }
        }
        Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Single entry guard for the public execution overloads: a context only executes the command
    /// representation it produces itself (mixing storage backends is not supported). Foreign
    /// implementations are rejected here, once, instead of in every overload.
    /// </summary>
    private static DbPreparedQueryCommand<TResult> AsDbCommand<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand)
        => preparedQueryCommand as DbPreparedQueryCommand<TResult>
           ?? throw new ArgumentException($"Expected {nameof(DbPreparedQueryCommand<TResult>)}, got {preparedQueryCommand.GetType().Name}", nameof(preparedQueryCommand));

    /// <summary>
    /// Resolves the enumerator a streaming terminal needs. A command prepared with
    /// <c>nonStreamUsing: true</c> (the <see cref="QueryCommand{TResult}.Prepare"/> default) is optimised for
    /// buffered/scalar results and never gets one, so fail with an actionable message instead of a
    /// NullReferenceException.
    /// </summary>
    private static ResultSetEnumerator<TResult> RequireEnumerator<TResult>(DbPreparedQueryCommand<TResult> compiledQuery)
        => compiledQuery.Enumerator
           ?? throw new InvalidOperationException(
               "This prepared command has no enumerator because it was created with nonStreamUsing: true, "
               + "which is optimised for buffered and scalar results. Use Prepare(nonStreamUsing: false) "
               + "to stream (ToAsyncEnumerable/ToEnumerable/CreateEnumerator).");

    /// <summary>
    /// Creates the streaming enumerator and applies the context's logging configuration to it. The
    /// enumerator receives the logger directly rather than pulling it from the context through a
    /// property, which is what kept the enumerator tied to the concrete <c>DbContext</c>.
    /// </summary>
    private ResultSetEnumerator<TResult> CreateResultSetEnumerator<TResult>(DbPreparedQueryCommand<TResult> compiledQuery)
    {
        var enumerator = new ResultSetEnumerator<TResult>(compiledQuery);
        enumerator.InitEnvironment(ResultSetEnumeratorLogger, LogSensitiveData);
        return enumerator;
    }

    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlEnumerator = RequireEnumerator(compiledQuery);
        sqlEnumerator.InitEnumerator(this, _createParam, @params, cancellationToken);
        return sqlEnumerator;
    }
    // public async Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    // {
    //     //ArgumentNullException.ThrowIfNull(queryCommand);

    //     var compiledQuery = queryCommand._compiledQuery as DbCompiledQuery<TResult> ?? CreateCompiledQuery(queryCommand, true, true);

    //     var sqlEnumerator = compiledQuery.Enumerator!;
    //     await sqlEnumerator.InitReaderAsync(@params, cancellationToken).ConfigureAwait(false);
    //     return sqlEnumerator;
    // }
    public async Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            var l = new List<TResult>(compiledQuery.LastRowCount);
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                l.Add(mapper(reader));
            }

            compiledQuery.LastRowCount = l.Count;
            return l;
        }
    }
    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);
        using (reader)
        {
            var l = new List<TResult>(compiledQuery.LastRowCount);
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                l.Add(mapper(reader));
            }

            compiledQuery.LastRowCount = l.Count;
            return l;
        }
    }
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
    /// <summary>
    /// Converts a raw scalar value to <typeparamref name="TResult"/>. SQLite has no bool type
    /// (comparisons/EXISTS come back as INTEGER/<see cref="long"/>), so typed fast paths are used
    /// to avoid boxing and <see cref="IConvertible"/> dispatch for the common scalar types.
    /// </summary>
    private static TResult ConvertScalar<TResult>(object value)
    {
        // Already the wanted runtime type (bool/int/long/double/decimal/string/...): unbox, no
        // IConvertible dispatch.
        if (value is TResult result) return result;

        var type = typeof(TResult);

        if (type == typeof(bool))
        {
            // SQLite has no bool type: comparisons/EXISTS come back as INTEGER (long).
            var v = value switch
            {
                long l => l != 0,
                int i => i != 0,
                short s => s != 0,
                byte b => b != 0,
                _ => Convert.ToBoolean(value),
            };
            return Unsafe.As<bool, TResult>(ref v);
        }
        if (type == typeof(int))
        {
            var v = value switch
            {
                // Convert.ToInt32(long) is checked; a narrowing cast must throw on overflow too.
                long l => checked((int)l),
                _ => Convert.ToInt32(value),
            };
            return Unsafe.As<int, TResult>(ref v);
        }
        if (type == typeof(long))
        {
            var v = value switch
            {
                int i => i,
                _ => Convert.ToInt64(value),
            };
            return Unsafe.As<long, TResult>(ref v);
        }
        if (type == typeof(double))
        {
            var v = value switch
            {
                // Widening float->double; Convert.ToDouble(double) would round-trip IConvertible.
                float f => f,
                _ => Convert.ToDouble(value),
            };
            return Unsafe.As<double, TResult>(ref v);
        }

        return (TResult)Convert.ChangeType(value, type);
    }

    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = GetDbCommand(compiledQuery, @params);

        var r = sqlCommand.ExecuteScalar();

        if (r is TResult res) return res;
        if (r is null or DBNull)
        {
            if (throwIfNull) throw new InvalidOperationException();
            return default;
        }

        return ConvertScalar<TResult>(r);
    }
    public async Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
    {
        CheckDisposed();
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);

        var r = await sqlCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        if (r is TResult res) return res;
        if (r is null or DBNull)
        {
            if (throwIfNull) throw new InvalidOperationException();
            return default;
        }

        return ConvertScalar<TResult>(r);
    }
    [Conditional("DEBUG")]
    private void CheckDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(DbContext));
    }

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
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlEnumerator = RequireEnumerator(compiledQuery);
        sqlEnumerator.InitEnumerator(this, _createParam, @params, CancellationToken.None);
        sqlEnumerator.InitReader(@params);

        return sqlEnumerator;
    }
    private Func<IDataRecord, TResult> GetMapCached<TResult>(QueryCommand<TResult> queryCommand, string? sql)
    {
        return RowMapperFactory.GetOrBuild(queryCommand, sql, GetType(), Logger, MapColumnExpression);
    }

    public TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return ExecuteScalar<TResult>(preparedQueryCommand, @params, true)!;

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            throw new InvalidOperationException();
        }
    }

    public async Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return (await ExecuteScalar<TResult>(preparedQueryCommand, @params, true, cancellationToken))!;

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            throw new InvalidOperationException();
        }
    }
    public TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return ExecuteScalar<TResult>(preparedQueryCommand, @params, false);

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            return default;
        }
    }

    public async Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        // Scalar mode: no row mapper was compiled, read the value directly.
        if (compiledQuery.MapDelegate is null)
            return await ExecuteScalar<TResult>(preparedQueryCommand, @params, false, cancellationToken);

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        var mapper = compiledQuery.MapDelegate!;
        using (reader)
        {
            if (reader.Read())
                return mapper(reader);

            return default;
        }
    }

    public TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);
        using (reader)
        {
            TResult r = default!;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            if (!hasResult)
                throw new InvalidOperationException();

            return r!;
        }
    }

    public async Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            TResult r = default!;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            if (!hasResult)
                throw new InvalidOperationException();

            return r!;
        }
    }

    public TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = GetDbCommand(compiledQuery, @params);
        var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);

        using (reader)
        {
            TResult? r = default;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            return r;
        }
    }

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = AsDbCommand(preparedQueryCommand);

        var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
        var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            TResult? r = default;
            var hasResult = false;
            var mapper = compiledQuery.MapDelegate!;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = mapper(reader);
                hasResult = true;
            }

            return r;
        }
    }

    public void PurgeQueryCache()
    {
        QueryPlanCache.Clear();
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
