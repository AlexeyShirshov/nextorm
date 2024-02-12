using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace nextorm.core;

public class DbContext : IDataContext
{
    internal readonly static string[] _params = ["norm_p0", "norm_p1", "norm_p2", "norm_p3", "norm_p4"];
    private readonly static MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    //private readonly static ConcurrentDictionary<Expression, List<SelectExpression>> _selectListExpCache = new(ExpressionEqualityComparer.Instance);
    internal protected readonly static ObjectPool<StringBuilder> _sbPool = new DefaultObjectPoolProvider().Create(new StringBuilderPooledObjectPolicy());
    //private static readonly AsyncLocal<Dictionary<QueryPlan, IDbCommandHolder>> _queryPlanCache = new() { Value = [] };    
    [ThreadStatic]
    private static Dictionary<QueryPlan, IDbCommandHolder>? _queryPlanCache;
    private readonly Dictionary<string, object> _properties = [];
    private DbConnection? _conn;
    protected bool _connWasCreatedByMe;
    private bool _disposed;
    internal bool _connOpen;
    private readonly bool _logParams;
    public DbContext(DbContextBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (optionsBuilder.LoggerFactory is not null)
        {
            Logger = optionsBuilder.LoggerFactory.CreateLogger(GetType());
            CommandLogger = optionsBuilder.LoggerFactory.CreateLogger(typeof(QueryCommand));
            ResultSetEnumeratorLogger = optionsBuilder.LoggerFactory.CreateLogger("nextorm.core.ResultSetEnumerator");
            _logParams = Logger.IsEnabled(LogLevel.Debug);
        }

        LogSensitiveData = optionsBuilder.ShouldLogSensitiveData;
        // CacheExpressions = optionsBuilder.CacheExpressions;
    }
    #region Properties
    protected internal readonly bool LogSensitiveData;
    public virtual string ConcatStringOperator => "+";
    public virtual string EmptyString => "''";
    public ILogger? Logger { get; }
    public ILogger? CommandLogger { get; }
    internal readonly ILogger? ResultSetEnumeratorLogger;
    public bool NeedMapping => true;
    private static Dictionary<QueryPlan, IDbCommandHolder> QueryPlanCache => _queryPlanCache ??= [];
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
    public async Task EnsureConnectionOpenAsync()
    {
        var conn = GetConnection();
        if (conn.State == ConnectionState.Closed)
        {
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
            await conn.OpenAsync();
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
    public virtual DbConnection CreateConnection()
    {
        throw new NotImplementedException();
    }
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

    public virtual string GetTableName(Type type)
    {
        throw new NotImplementedException(type.ToString());
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<Param> ExtractParams(QueryCommand queryCommand)
    {
        var @params = new List<Param>();
        MakeSelect(queryCommand, true, @params, queryCommand, null);
        return @params;
    }

    private string? MakeSelect(QueryCommand queryCommand, bool paramMode, List<Param> @params, IQueryContext queryContext, IAliasProvider? aliasProvider)
    {
        var sqlBuilder = new SqlBuilder(this, paramMode, @params, new DefaultColumnsProvider(), queryContext, new DefaultParamProvider(), aliasProvider, Logger);
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
    {
        // #if DEBUG
        //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
        // #endif

        var conn = GetConnection();

        if (!_connOpen)
        {
            if (conn.State == ConnectionState.Closed)
            {
                if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
                conn.Open();
            }

            _connOpen = true;
        }

        var cmd = compiledQuery.GetDbCommand(@params, this, conn);

        if (_logParams) LogParams(cmd);

        return cmd;
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "<Pending>")]
    private async ValueTask<DbCommand> GetDbCommand<TResult>(DbPreparedQueryCommand<TResult> compiledQuery, object[]? @params, CancellationToken cancellationToken)
    {
        //var compiledQuery = queryCommand._compiledQuery as DbCompiledQuery<TResult> ?? CreateCompiledQuery(queryCommand, false, true);

        // #if DEBUG
        //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
        // #endif

        var conn = GetConnection();

        if (!_connOpen)
        {
            if (conn.State == ConnectionState.Closed)
            {
                if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
                await conn.OpenAsync(cancellationToken);
            }

            _connOpen = true;
        }

        var cmd = compiledQuery.GetDbCommand(@params, this, conn);
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

        var ext = queryCommand.CustomData as DbQueryCommandExtension;

        if (queryCommand.Cache && storeInCache)
        {
            queryPlan = new QueryPlan(queryCommand, ext?.ManualSql);
            QueryPlanCache.TryGetValue(queryPlan, out planCache);
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
                : GetMap(queryCommand)();

            var noParams = !(@params?.Count > 0);

            var dbCommand = CreateCommand(sql!);
            if (!noParams)
                dbCommand.Parameters.AddRange(@params!.Select(it => CreateParam(it.Name, it.Value)).ToArray());

            // dbCommand.Connection = GetConnection();

            //dbCommand.Prepare();
            //return new SqlCacheEntry(null) { Enumerator = new EmptyEnumerator<TResult>() };

            var compiledQuery = new DbPreparedQueryCommand<TResult>(dbCommand, map, queryCommand.SingleRow, queryCommand.OneColumn, ext is null ? sql! : null, noParams);

            if (createEnumerator)
            {
                var enumerator = new ResultSetEnumerator<TResult>(compiledQuery!);
                compiledQuery.Enumerator = enumerator;
            }

            if (storeInCache && queryCommand.Cache)
            {
                QueryPlanCache[queryPlan!.GetCacheVersion()] = compiledQuery;
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
                if (!compiledQuery.NoParams/* || compiledQuery.DbCommand is null*/)
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
                        dbCommandParams[i].Value = pp[i].Value;
                        Debug.Assert(dbCommandParams[i].ParameterName == pp[i].Name, $"ParameterName {dbCommandParams[i].ParameterName} not equals {pp[i].Name}");
                    }
                    //}

                    //compiledQuery ??= new DbCompiledQuery<TResult>(dbCommand, plan.MapDelegate, queryCommand.SingleRow);
                }
            }

            // if (plan.CompiledQuery is null)
            // {
            //     if (createEnumerator)
            //     {
            //         var enumerator = new ResultSetEnumerator<TResult>(compiledQuery!);
            //         compiledQuery.Enumerator = enumerator;
            //     }

            //     plan.CompiledQuery = compiledQuery;

            // }

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
    public virtual DbParameter CreateParam(string name, object? value)
    {
        throw new NotImplementedException();
    }

    public virtual bool MakeTop(int limit, out string? topStmt)
    {
        topStmt = null;
        return false;
    }

    public virtual string EmptySorting() => throw new NotImplementedException();
    public virtual bool RequireSorting(QueryCommand queryCommand) => false;
    public virtual void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        throw new NotImplementedException();
    }

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
    public virtual string MakeTableAlias(string tableAlias)
    {
        return " as " + Escape(tableAlias);
    }
    public virtual string MakeColumnAlias(string? colAlias)
    {
        if (string.IsNullOrEmpty(colAlias))
            return string.Empty;

        return " as " + Escape(colAlias);
    }
    internal string GetColumnName(MemberInfo member)
    {
        throw new NotImplementedException(member.Name);
    }

    public virtual string MakeCoalesce(string v1, string v2)
    {
        return $"isnull({v1},{v2})";
    }
    public virtual string Escape(string keyword) => "'" + keyword + "'";
    public virtual string MakeParam(string name)
    {
        throw new NotImplementedException(name);
    }
    public void ResetPreparation(QueryCommand queryCommand)
    {
        //_clearCache = true;
    }
    public FromExpression GetFrom(Type t)
    {
        if (DataContextCache.Metadata.TryGetValue(t, out var entity) && !string.IsNullOrEmpty(entity.TableName))
            return new FromExpression(entity.TableName);

        return new FromExpression(GetTableName(t));
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

    public static Expression MapColumn(SelectExpression column, Expression param)
    {
        if (column.Nullable)
        {
            return Expression.Condition(
                Expression.Call(param, IsDBNullMI, Expression.Constant(column.Index)),
                Expression.Constant(null, column.PropertyType),
                Expression.Convert(
                    Expression.Call(param, column.GetDataRecordMethod(), Expression.Constant(column.Index)),
                    column.PropertyType
                )
            );
        }

        return Expression.Call(param, column.GetDataRecordMethod(), Expression.Constant(column.Index));
    }

    public virtual string MakeBool(bool v)
    {
        return v ? "1" : "0";
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
        DisposeStaff();

        GC.SuppressFinalize(this);
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

    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
        {
            var sqlEnumerator = compiledQuery.Enumerator!;
            sqlEnumerator.InitEnumerator(this, @params, cancellationToken);
            return sqlEnumerator;
        }
        throw new NotSupportedException(preparedQueryCommand.GetType().Name);
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
        ////ArgumentNullException.ThrowIfNull(queryCommand);

        if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
        {
            var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
            //var sqlCommand = compiledQuery.DbCommand;
            var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
            using (reader)
            {
                var l = new List<TResult>(compiledQuery.LastRowCount);
                var mapper = compiledQuery.MapDelegate!;
                while (reader.Read())
                {
                    l.Add(mapper(reader));
                    //l.Add(default);
                }

                compiledQuery.LastRowCount = l.Count;
                return l;
            }
        }
        throw new NotSupportedException(preparedQueryCommand.GetType().Name);
    }
    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
    {
        if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
        {
            var sqlCommand = GetDbCommand(compiledQuery, @params);
            //var sqlCommand = compiledQuery.DbCommand;
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
        throw new NotSupportedException(preparedQueryCommand.GetType().Name);
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
    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull)
    {
        if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
        {
            var sqlCommand = GetDbCommand(compiledQuery, @params);
            //var sqlCommand = compiledQuery.DbCommand;

            //if (conn.State == ConnectionState.Closed)
            // if (!_connOpen)
            // {
            //     if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
            //     conn.Open();
            //     _connOpen = true;
            // }

            // _logParams?.Invoke(sqlCommand);

            var r = sqlCommand.ExecuteScalar();

            if (r is TResult res) return res;
            if (r is null or DBNull)
            {
                if (throwIfNull) throw new InvalidOperationException();
                return default;
            }

            var type = typeof(TResult);
            return (TResult)Convert.ChangeType(r, type);
        }
        throw new NotSupportedException(preparedQueryCommand.GetType().Name);
    }
    public async Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
    {
        CheckDisposed();
        if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
        {
            var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken);
            //var sqlCommand = compiledQuery.DbCommand;

            //if (conn.State == ConnectionState.Closed)
            // if (!_connOpen)
            // {
            //     if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
            //     await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            //     _connOpen = true;
            // }

            // _logParams?.Invoke(sqlCommand);

            var r = await sqlCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            // var r = true;
            if (r is TResult res) return res;
            if (r is null or DBNull)
            {
                if (throwIfNull) throw new InvalidOperationException();
                return default;
            }

            var type = typeof(TResult);
            return (TResult)Convert.ChangeType(r, type);
        }
        throw new NotSupportedException(preparedQueryCommand.GetType().Name);
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
        if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
        {
            var sqlEnumerator = compiledQuery.Enumerator!;
            sqlEnumerator.InitEnumerator(this, @params, CancellationToken.None);
            sqlEnumerator.InitReader(@params);

            return sqlEnumerator;
        }
        throw new NotSupportedException(preparedQueryCommand.GetType().Name);
    }
    public Func<Func<IDataRecord, TResult>> GetMap<TResult>(QueryCommand<TResult> queryCommand)
    {
#if DEBUG
        if (!queryCommand.IsPrepared)
            throw new InvalidOperationException("Command not prepared");
#endif
        // var key = new ExpressionKey(_exp);
        // if (!(_dataProvider as SqlDataProvider).MapCache.TryGetValue(key, out var del))
        // {
        //     if (Logger?.IsEnabled(LogLevel.Information) ?? false) Logger.LogInformation("Map delegate cache miss for: {exp}", _exp);
        var resultType = typeof(TResult);

        return () =>
        {
            var param = Expression.Parameter(typeof(IDataRecord));
            Expression<Func<IDataRecord, TResult>> lambda;

            if (queryCommand.OneColumn)
            {
                var vis = new ReplaceMemberVisitor(queryCommand.EntityType!, param);
                var body = vis.Visit(((LambdaExpression)queryCommand.SelectList![0].Expression).Body);
                lambda = Expression.Lambda<Func<IDataRecord, TResult>>(body, param);
            }
            else
            {
                var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new PrepareException($"Cannot get ctor from {resultType}");

                if (ctorInfo.GetParameters().Length == queryCommand.SelectList!.Length)
                {
                    var newParams = queryCommand.SelectList!.Select(column => DbContext.MapColumn(column, param)).ToArray();
                    var ctor = Expression.New(ctorInfo, newParams);

                    lambda = Expression.Lambda<Func<IDataRecord, TResult>>(ctor, param);
                    //if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Get instance of {type} as: {exp}", resultType, lambda);
                    // var body = Expression.Block(typeof(TResult), new Expression[] { assignValuesVariable, ctor });
                    // var lambda = Expression.Lambda<Func<object, object[]?, TResult>>(body, param, valuesParam);
                    // if (Logger?.IsEnabled(LogLevel.Debug) ?? false)
                    // {
                    //     var sb = new StringBuilder();
                    //     sb.AppendLine();
                    //     sb.Append(assignValuesVariable.ToString()).AppendLine(";");
                    //     sb.Append(ctor.ToString()).AppendLine(";");
                    //     var dumpExp = lambda.ToString().Replace("...", sb.ToString());
                    //     Logger.LogDebug("Get instance of {type} as: {exp}", resultType, dumpExp);
                    // }
                    // return lambda.Compile();
                }
                else
                {
                    var bindings = queryCommand.SelectList!.Select(column =>
                    {
                        var propInfo = column.PropertyInfo ?? resultType.GetProperty(column.PropertyName!)!;
                        return Expression.Bind(propInfo, DbContext.MapColumn(column, param));
                    }).ToArray();

                    var ctor = Expression.New(ctorInfo);

                    var memberInit = Expression.MemberInit(ctor, bindings);

                    var body = memberInit;
                    lambda = Expression.Lambda<Func<IDataRecord, TResult>>(body, param);

                }
            }

            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Get instance of {type} as: {exp}", resultType, lambda);

            // var key = new ExpressionKey(lambda, _expCache, queryCommand);
            // if (!_expCache.TryGetValue(key, out var d))
            // {
            //     d = lambda.Compile();
            //     _expCache[key] = d;
            // }
            var d = lambda.Compile();
            return (Func<IDataRecord, TResult>)d;
        };

        //         (_dataProvider as SqlDataProvider).MapCache[key] = del;
        //     }
    }

    public TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        if (preparedQueryCommand.IsScalar)
        {
            return ExecuteScalar<TResult>(preparedQueryCommand, @params, true)!;
        }
        else
        {
            if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
            {
                var sqlCommand = GetDbCommand(compiledQuery, @params);
                //var sqlCommand = compiledQuery.DbCommand;
                var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);
                var mapper = compiledQuery.MapDelegate!;
                using (reader)
                {
                    if (reader.Read())
                    {
                        return mapper(reader);
                    }

                    throw new InvalidOperationException();
                }
            }
            throw new NotSupportedException(preparedQueryCommand.GetType().Name);
        }
    }

    public async Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        if (preparedQueryCommand.IsScalar)
        {
            return (await ExecuteScalar<TResult>(preparedQueryCommand, @params, true, cancellationToken))!;
        }
        else
        {
            if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
            {
                var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
                //var sqlCommand = compiledQuery.DbCommand;
                var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
                var mapper = compiledQuery.MapDelegate!;
                using (reader)
                {
                    if (reader.Read())
                    {
                        return mapper(reader);
                    }

                    throw new InvalidOperationException();
                }
            }
            throw new NotSupportedException(preparedQueryCommand.GetType().Name);
        }
    }
    public TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
    {
        if (preparedQueryCommand.IsScalar)
        {
            return ExecuteScalar<TResult>(preparedQueryCommand, @params, false);
        }
        else
        {
            if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
            {
                var sqlCommand = GetDbCommand(compiledQuery, @params);
                //var sqlCommand = compiledQuery.DbCommand;
                var reader = sqlCommand.ExecuteReader(compiledQuery.Behavior);
                var mapper = compiledQuery.MapDelegate!;
                //var (reader, compiledQuery) = CreateReader(queryCommand, @params);
                using (reader)
                {
                    if (reader.Read())
                    {
                        return mapper(reader);
                    }

                    return default;
                }
            }
            throw new NotSupportedException(preparedQueryCommand.GetType().Name);
        }
    }

    public async Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        if (preparedQueryCommand.IsScalar)
        {
            return await ExecuteScalar<TResult>(preparedQueryCommand, @params, false, cancellationToken);
        }
        else
        {
            if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
            {
                var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
                //var sqlCommand = compiledQuery.DbCommand;
                var reader = await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false);
                var mapper = compiledQuery.MapDelegate!;
                using (reader)
                {
                    if (reader.Read())
                    {
                        return mapper(reader);
                    }

                    return default;
                }
            }
            throw new NotSupportedException(preparedQueryCommand.GetType().Name);
        }
    }

    public TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
    {
        if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
        {
            var sqlCommand = GetDbCommand(compiledQuery, @params);
            //var sqlCommand = compiledQuery.DbCommand;
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
        throw new NotSupportedException(preparedQueryCommand.GetType().Name);
    }

    public async Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
        {
            var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
            //var sqlCommand = compiledQuery.DbCommand;
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
        throw new NotSupportedException(preparedQueryCommand.GetType().Name);
    }

    public TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
    {
        if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
        {
            var sqlCommand = GetDbCommand(compiledQuery, @params);
            //var sqlCommand = compiledQuery.DbCommand;
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
        throw new NotSupportedException(preparedQueryCommand.GetType().Name);
    }

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery)
        {
            var sqlCommand = await GetDbCommand(compiledQuery, @params, cancellationToken).ConfigureAwait(false);
            //var sqlCommand = compiledQuery.DbCommand;
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
        throw new NotSupportedException(preparedQueryCommand.GetType().Name);
    }

    public virtual string MakeCount(bool distinct, bool big) => distinct switch
    {
        true => "count(distinct ",
        _ => "count("
    };

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
