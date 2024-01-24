using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using System.Collections.Concurrent;
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
    private bool _connOpen;
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
    protected internal bool LogSensitiveData { get; set; }
    public virtual string ConcatStringOperator => "+";
    public virtual string EmptyString => "''";
    public ILogger? Logger { get; }
    public ILogger? CommandLogger { get; }
    public ILogger? ResultSetEnumeratorLogger { get; }
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
    private List<Param> ExtractParams(QueryCommand queryCommand) => MakeSelect(queryCommand, true).Item2;
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
                ? MakeSelect(queryCommand, false)
                : (ext.ManualSql, ext.MakeParams?.Invoke());

            // var (sql, @params) = MakeSelect(queryCommand, true);
            // var cacheEntryX = new SqlCacheEntry(null) { Enumerator = new EmptyEnumerator<TResult>() };
            // if (queryCommand.Cache)
            //     _queryCache[queryCommand] = cacheEntryX;
            // return cacheEntryX;

            Func<IDataRecord, TResult>? map = queryCommand.SingleRow && queryCommand.OneColumn
                ? null
                : GetMap(queryCommand)();

            var noParams = @params.Count == 0;

            var dbCommand = CreateCommand(sql!);
            if (!noParams)
                dbCommand.Parameters.AddRange(@params.Select(it => CreateParam(it.Name, it.Value)).ToArray());

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
    public virtual (string?, List<Param>) MakeSelect(QueryCommand cmd, bool paramMode)
    {
#if DEBUG
        if (!cmd.IsPrepared)
            throw new InvalidOperationException("Command not prepared");

        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Making sql with param mode {mode}", paramMode);
#endif
        //ArgumentNullException.ThrowIfNull(cmd.SelectList);
        //ArgumentNullException.ThrowIfNull(cmd.From);
        ArgumentNullException.ThrowIfNull(cmd.EntityType);

        var selectList = cmd.SelectList;
        var from = cmd.From;
        var entityType = cmd.EntityType;

        var sqlBuilder = paramMode ? null : _sbPool.Get();

        var @params = new List<Param>();
        var pageApplied = false;

        if (!paramMode) sqlBuilder!.Append("select ");
        if (cmd.IgnoreColumns || selectList is null)
        {
            if (!paramMode) sqlBuilder!.Append("*, ");
        }
        else
        {
            if (!paramMode && cmd.Paging.IsTop && MakeTop(cmd.Paging.Limit, out var topStmt))
            {
                sqlBuilder!.Append(topStmt).Append(' ');
                pageApplied = true;
            }

            var selectListCount = selectList.Length;
            for (var i = 0; i < selectListCount; i++)
            {
                var item = selectList[i];
                var (needAliasForColumn, column) = MakeColumn(item.Expression!, entityType, cmd, false, @params, paramMode);

                if (!paramMode)
                {
                    sqlBuilder!.Append(column);

                    if (needAliasForColumn)
                    {
                        sqlBuilder.Append(MakeColumnAlias(item.PropertyName));
                    }

                    sqlBuilder.Append(", ");
                }
            }
        }

        if (from is not null)
        {
            var fromStr = MakeFrom(from, @params, paramMode);
            if (!paramMode)
            {
                sqlBuilder!.Length -= 2;
                sqlBuilder.Append(" from ").Append(fromStr);
            }

            if (cmd.Joins is not null)
            {
                for (var idx = 0; idx < cmd.Joins.Length; idx++)
                {
                    var join = cmd.Joins[idx];

                    var joinSql = MakeJoin(join, cmd, @params, paramMode);
                    if (!paramMode) sqlBuilder!.Append(joinSql);
                }
            }

            if (cmd.PreparedCondition is not null)
            {
                var whereSql = MakeWhere(entityType, cmd, cmd.PreparedCondition, 0, null, @params, paramMode);
                if (!paramMode) sqlBuilder!.AppendLine().Append(" where ").Append(whereSql);
            }

            if (cmd.GroupingList?.Length > 0)
            {
                if (!paramMode) sqlBuilder!.AppendLine().Append(" group by ");

                var groupingListCount = cmd.GroupingList.Length;
                for (var i = 0; i < groupingListCount; i++)
                {
                    var item = cmd.GroupingList[i];
                    var (needAliasForColumn, column) = MakeColumn(item.Expression!, entityType, cmd, false, @params, paramMode);

                    if (!paramMode)
                    {
                        sqlBuilder!.Append(column);

                        if (needAliasForColumn)
                        {
                            sqlBuilder.Append(MakeColumnAlias(item.PropertyName));
                        }

                        sqlBuilder.Append(", ");
                    }
                }

                if (!paramMode)
                {
                    sqlBuilder!.Length -= 2;
                }

                if (cmd.Having is not null)
                {
                    var havingSql = MakeWhere(entityType, cmd, cmd.Having, 0, null, @params, paramMode);
                    if (!paramMode) sqlBuilder!.AppendLine().Append(" having ").Append(havingSql);
                }
            }

            if (cmd.Sorting is not null)
            {
                if (!paramMode) sqlBuilder!.AppendLine().Append(" order by ");

                foreach (var sorting in cmd.Sorting)
                {
                    if (sorting.PreparedExpression is not null)
                    {
                        var sortingSql = MakeSort(entityType, cmd, sorting.PreparedExpression, 0, null, @params, paramMode);
                        if (!paramMode)
                        {
                            sqlBuilder!.Append(sortingSql);
                            if (sorting.Direction == OrderDirection.Desc)
                                sqlBuilder.Append(" desc");

                            sqlBuilder.Append(", ");
                        }
                    }
                    else if (!paramMode && sorting.ColumnIndex.HasValue)
                    {
                        sqlBuilder!.Append(sorting.ColumnIndex);
                        if (sorting.Direction == OrderDirection.Desc)
                            sqlBuilder.Append(" desc");

                        sqlBuilder.Append(", ");
                    }
                }

                if (!paramMode) sqlBuilder!.Length -= 2;
            }
            else if (!pageApplied && RequireSorting(cmd) && !paramMode)
            {
                sqlBuilder!.AppendLine().Append(" order by ").Append(EmptySorting());
            }

            if (!pageApplied && !paramMode && !cmd.Paging.IsEmpty)
            {
                sqlBuilder!.AppendLine();
                MakePage(cmd.Paging, sqlBuilder);
            }
        }
        else if (!paramMode && sqlBuilder!.Length > 0)
            sqlBuilder.Length -= 2;


        string? r = null;
        if (!paramMode)
        {
            r = sqlBuilder!.ToString();

            _sbPool.Return(sqlBuilder);
        }

#if DEBUG
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Generated sql with param mode {mode}: {sql}", paramMode, r);
#endif

        return (r, @params);
    }

    protected virtual bool MakeTop(int limit, out string? topStmt)
    {
        topStmt = null;
        return false;
    }

    protected virtual string EmptySorting() => throw new NotImplementedException();
    protected virtual bool RequireSorting(QueryCommand queryCommand) => false;
    protected virtual void MakePage(Paging paging, StringBuilder sqlBuilder)
    {
        throw new NotImplementedException();
    }

    private string? MakeJoin(JoinExpression join, QueryCommand cmd, List<Param> @params, bool paramMode)
    {
        var sqlBuilder = paramMode ? null : _sbPool.Get();

        if (!paramMode)
        {
            switch (join.JoinType)
            {
                case JoinType.Inner:
                    sqlBuilder!.Append(" join ");
                    break;
                default:
                    throw new NotImplementedException(join.JoinType.ToString());
            }
        }

        var visitor = new JoinExpressionVisitor();
        visitor.Visit(join.JoinCondition);

        var dim = 1;
        if (join.JoinCondition.Parameters[0].Type.TryGetProjectionDimension(out var joinDim))
            dim = joinDim;

        FromExpression fromExp;
        if (visitor.JoinType is not null)
        {
            if (visitor.JoinType.IsAnonymous())
            {
                //fromExp = GetFrom(join.Query.EntityType);
                var (sql, p) = MakeSelect(join.Query!, paramMode);
                @params.AddRange(p);
                if (!paramMode)
                {
                    sqlBuilder!.Append('(').Append(sql).Append(") ");
                    sqlBuilder.Append(GetAliasFromProjection(cmd, visitor.JoinType, 1));
                }
            }
            else
            {
                fromExp = GetFrom(visitor.JoinType);

                if (!paramMode)
                    fromExp.TableAlias = GetAliasFromProjection(cmd, visitor.JoinType, dim);

                var fromSql = MakeFrom(fromExp, @params, paramMode);
                if (!paramMode)
                {
                    sqlBuilder!.Append(fromSql);
                }
            }
        }

        var whereSql = MakeWhere(cmd.EntityType!, cmd, visitor.JoinCondition!, dim, dim == 1
                        ? new ExpressionAliasProvider(join.JoinCondition)
                        : new ProjectionAliasProvider(dim, cmd.EntityType!), @params, paramMode);

        if (!paramMode)
        {
            sqlBuilder!.Append(" on ");

            sqlBuilder.Append(whereSql);
        }

        string? r = null;

        if (!paramMode)
        {
            r = sqlBuilder!.ToString();

            _sbPool.Return(sqlBuilder);
        }

        return r;
    }
    private static string GetAliasFromProjection(QueryCommand cmd, Type declaringType, int from)
    {
        int idx = 0;
        foreach (var prop in cmd.EntityType!.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (++idx > from && prop.PropertyType == declaringType)
                return prop.Name;
        }

        throw new BuildSqlCommandException($"Cannot find alias of type {declaringType} in {cmd.EntityType}");
    }
    private string MakeWhere(Type entityType, IQueryContext queryContext, Expression condition, int dim, IAliasProvider? aliasProvider, List<Param> @params, bool paramMode)
    {
        // if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Where expression: {exp}", condition);
        using var visitor = new WhereExpressionVisitor(entityType, this, queryContext, dim, aliasProvider, queryContext, queryContext, paramMode);
        visitor.Visit(condition);
        @params.AddRange(visitor.Params);

        if (paramMode) return string.Empty;

        return visitor.ToString();
    }
    private string MakeSort(Type entityType, IQueryContext queryContext, Expression sorting, int dim, IAliasProvider? aliasProvider, List<Param> @params, bool paramMode)
        => MakeSort(entityType, queryContext, sorting, dim, aliasProvider, queryContext, queryContext, @params, paramMode);
    private string MakeSort(Type entityType, ISourceProvider tableSource, Expression sorting, int dim, IAliasProvider? aliasProvider, IParamProvider paramProvider, IQueryProvider queryProvider, List<Param> @params, bool paramMode)
    {
        // if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Where expression: {exp}", condition);
        using var visitor = new BaseExpressionVisitor(entityType, this, tableSource, dim, aliasProvider, paramProvider, queryProvider, true, paramMode);
        visitor.Visit(sorting);
        @params.AddRange(visitor.Params);

        if (paramMode) return string.Empty;

        return visitor.ToString();
    }
    public (bool NeedAliasForColumn, string Column) MakeColumn(Expression selectExp, Type entityType, IQueryContext queryContext, bool dontNeedAlias, List<Param> @params, bool paramMode)
        => MakeColumn(selectExp, entityType, queryContext, dontNeedAlias, queryContext, queryContext, @params, paramMode);
    public (bool NeedAliasForColumn, string Column) MakeColumn(Expression selectExp, Type entityType, ISourceProvider tableProvider, bool dontNeedAlias, IParamProvider paramProvider, IQueryProvider queryProvider, List<Param> @params, bool paramMode)
    {
        using var visitor = new BaseExpressionVisitor(entityType, this, tableProvider, 0, null, paramProvider, queryProvider, dontNeedAlias, paramMode);
        visitor.Visit(selectExp);
        @params.AddRange(visitor.Params);

        if (paramMode) return (false, string.Empty);

        return (visitor.NeedAliasForColumn, visitor.ToString());
    }

    public virtual string MakeFrom(FromExpression from, List<Param> @params, bool paramMode)
    {
        ArgumentNullException.ThrowIfNull(from);

        return from.Table.Match(
            tableName => tableName + (string.IsNullOrEmpty(from.TableAlias) ? string.Empty : MakeTableAlias(from.TableAlias)),
            cmd =>
            {
                if (!cmd.IsPrepared) throw new BuildSqlCommandException("Inner query is not prepared");
                var (sql, p) = MakeSelect(cmd, paramMode);
                @params.AddRange(p);

                if (paramMode) return string.Empty;

                return "(" + sql + ")" + (string.IsNullOrEmpty(from.TableAlias) ? string.Empty : MakeTableAlias(from.TableAlias));
            }
        );
    }
    public virtual string MakeTableAlias(string tableAlias)
    {
        return " as " + tableAlias;
    }
    public virtual string MakeColumnAlias(string? colAlias)
    {
        if (string.IsNullOrEmpty(colAlias))
            return string.Empty;

        return " as " + colAlias;
    }
    internal string GetColumnName(MemberInfo member)
    {
        throw new NotImplementedException(member.Name);
    }

    public virtual string MakeCoalesce(string v1, string v2)
    {
        return $"isnull({v1},{v2})";
    }

    public virtual string MakeParam(string name)
    {
        throw new NotImplementedException(name);
    }
    public void ResetPreparation(QueryCommand queryCommand)
    {
        //_clearCache = true;
    }

    public FromExpression? GetFrom(Type srcType, QueryCommand queryCommand)
    {
        if (srcType != typeof(TableAlias))
        {
            if (queryCommand.Joins?.Length > 0 && srcType.IsAssignableTo(typeof(IProjection)))
            {
                var prop_t1 = srcType.GetProperty("t1") ?? throw new BuildSqlCommandException($"Projection {srcType} must have t1 property");

                var f = GetFrom(prop_t1.PropertyType);

                f.TableAlias = "t1";

                return f;
            }
            else
                return GetFrom(srcType);
        }
        else //throw new BuildSqlCommandException($"From must be specified for {nameof(TableAlias)} as source type");
            return null;
    }

    private FromExpression GetFrom(Type t)
    {
        if (DataContextCache.Metadata.TryGetValue(t, out var entity) && !string.IsNullOrEmpty(entity.TableName))
            return new FromExpression(entity.TableName);

        return new FromExpression(GetTableName(t));
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
                var vis = new ReplaceMemberVisitor(queryCommand.EntityType!, queryCommand, param);
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