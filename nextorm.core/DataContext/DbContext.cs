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
    private readonly static MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private readonly static ConcurrentDictionary<Type, IEntityMeta> _metadata = new();
    private readonly static ConcurrentDictionary<Type, List<SelectExpression>> _selectListCache = new();
    //private readonly static ConcurrentDictionary<Expression, List<SelectExpression>> _selectListExpCache = new(ExpressionEqualityComparer.Instance);
    private readonly static ExpressionCache<Delegate> _expCache = new();
    internal protected readonly static ObjectPool<StringBuilder> _sbPool = new DefaultObjectPoolProvider().Create(new StringBuilderPooledObjectPolicy());
    private readonly static string[] _params = ["norm_p0", "norm_p1", "norm_p2", "norm_p3", "norm_p4"];
    //    private readonly IDictionary<QueryCommand, SqlCacheEntry> _queryCache = new Dictionary<QueryCommand, SqlCacheEntry>();
    private readonly Dictionary<QueryPlan, object> _queryPlanCache = new Dictionary<QueryPlan, object>();
    private DbConnection? _conn;
    protected bool _connCreated;
    private bool _disposedValue;
    private bool _connOpen;
    private readonly Action<DbCommand>? _logParams;
    public DbContext(DbContextBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (optionsBuilder.LoggerFactory is not null)
        {
            Logger = optionsBuilder.LoggerFactory.CreateLogger(GetType());
            CommandLogger = optionsBuilder.LoggerFactory.CreateLogger(typeof(QueryCommand));
            if (Logger.IsEnabled(LogLevel.Debug))
                _logParams = LogParams;
        }
        LogSensetiveData = optionsBuilder.ShouldLogSensetiveData;
    }
    //private DbCommand? _cmd;
    protected internal bool LogSensetiveData { get; set; }
    public virtual string ConcatStringOperator => "+";
    public ILogger? Logger { get; set; }
    public bool NeedMapping => true;
    public IDictionary<ExpressionKey, Delegate> ExpressionsCache => _expCache;
    public virtual string EmptyString => "''";
    public IDictionary<Type, IEntityMeta> Metadata => _metadata;
    public IDictionary<Type, List<SelectExpression>> SelectListCache => _selectListCache;
    //public IDictionary<Expression, List<SelectExpression>> SelectListExpessionCache => _selectListExpCache;
    public ILogger? CommandLogger { get; set; }
    public Entity From(string table) => new(this, table) { Logger = CommandLogger };
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
            _connCreated = true;
            _conn = CreateConnection();
            _conn.StateChange += OnStateChanged;
        }
        return _conn;
    }
    private void OnStateChanged(object sender, StateChangeEventArgs args) => _connOpen = args.CurrentState == ConnectionState.Open;
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
    public virtual DbCommand CreateCommand(string sql)
    {
        throw new NotImplementedException();
    }

    public virtual string GetTableName(Type type)
    {
        throw new NotImplementedException(type.ToString());
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<Param> ExtractParams(QueryCommand queryCommand) => MakeSelect(queryCommand, true).Item2;
    private void LogParams(DbCommand sqlCommand)
    {
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            Logger.LogDebug("Executing query: {sql}", sqlCommand.CommandText);

            if (LogSensetiveData)
            {
                foreach (DbParameter p in sqlCommand.Parameters)
                {
                    Logger.LogDebug("param {name} is {value}", p.ParameterName, p.Value);
                }
            }
            else if (sqlCommand.Parameters?.Count > 0)
            {
                Logger.LogDebug("Use {method} to see param values", nameof(LogSensetiveData));
            }
        }
    }
    private DbCompiledQuery<TResult> GetCompiledQuery<TResult>(QueryCommand<TResult> queryCommand, object[]? @params)
    {
        var compiledQuery = queryCommand._compiledQuery as DbCompiledQuery<TResult> ?? CreateCompiledQuery(queryCommand, false, true);

        // #if DEBUG
        //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
        // #endif
        var sqlCommand = compiledQuery.DbCommand;
        if (@params is not null)
        {
            var parameters = sqlCommand.Parameters;
            for (var i = 0; i < @params.Length; i++)
            {
                var idx = -1;
                if (i < compiledQuery.ParamMap.Count)
                {
                    idx = compiledQuery.ParamMap[i];
                }

                string? paramName = null;
                if (idx < 0)
                {
                    paramName = i < 5 ? _params[i] : string.Format("norm_p{0}", i);
                    // parameters[0].Value = @params[i];
                    //sqlCommand.Parameters[paramName].Value = @params[i];
                    //var added = false;
                    idx = parameters.IndexOf(paramName);

                    if (idx >= 0)
                    {
                        if (i < compiledQuery.ParamMap.Count)
                            compiledQuery.ParamMap[i] = idx;
                        else
                            compiledQuery.ParamMap.Add(idx);
                    }
                }

                if (idx >= 0)
                    parameters[idx].Value = @params[i];
                else
                {
                    if (i < compiledQuery.ParamMap.Count)
                        compiledQuery.ParamMap[i] = parameters.Count;
                    else
                        compiledQuery.ParamMap.Add(parameters.Count);

                    parameters.Add(CreateParam(paramName!, @params[i]));
                }
            }
        }

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

        _logParams?.Invoke(sqlCommand);

        return compiledQuery;
    }
    private async ValueTask<DbCompiledQuery<TResult>> GetCompiledQuery<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var compiledQuery = queryCommand._compiledQuery as DbCompiledQuery<TResult> ?? CreateCompiledQuery(queryCommand, false, true);

        // #if DEBUG
        //         if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
        // #endif
        var sqlCommand = compiledQuery.DbCommand;
        if (@params is not null)
        {
            var parameters = sqlCommand.Parameters;
            for (var i = 0; i < @params.Length; i++)
            {
                var idx = -1;
                if (i < compiledQuery.ParamMap.Count)
                {
                    idx = compiledQuery.ParamMap[i];
                }

                string? paramName = null;
                if (idx < 0)
                {
                    paramName = i < 5 ? _params[i] : string.Format("norm_p{0}", i);
                    // parameters[0].Value = @params[i];
                    //sqlCommand.Parameters[paramName].Value = @params[i];
                    //var added = false;
                    idx = parameters.IndexOf(paramName);

                    if (idx >= 0)
                    {
                        if (i < compiledQuery.ParamMap.Count)
                            compiledQuery.ParamMap[i] = idx;
                        else
                            compiledQuery.ParamMap.Add(idx);
                    }
                }

                if (idx >= 0)
                    parameters[idx].Value = @params[i];
                else
                {
                    if (i < compiledQuery.ParamMap.Count)
                        compiledQuery.ParamMap[i] = parameters.Count;
                    else
                        compiledQuery.ParamMap.Add(parameters.Count);

                    parameters.Add(CreateParam(paramName!, @params[i]));
                }
                // for (var j = 0; j < parameters.Count; j++)
                // {
                //     var p = sqlCommand.Parameters[j];
                //     if (p.ParameterName == paramName)
                //     {
                //         p.Value = @params[i];
                //         added = true;
                //         break;
                //     }
                // }
                // if (!added)
                //     sqlCommand.Parameters.Add(CreateParam(paramName, @params[i]));
            }
        }

        var conn = sqlCommand.Connection!;

        if (!_connOpen)
        {
            if (conn.State == ConnectionState.Closed)
            {
                if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
                await conn.OpenAsync(cancellationToken);
            }
            _connOpen = true;
        }

        _logParams?.Invoke(sqlCommand);

        return compiledQuery;
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

    private DbCompiledQuery<TResult> CreateCompiledQuery<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, Func<(string, List<Param>)>? makeSelect = null)
    {
        QueryPlan? queryPlan = null;
        object? planCache = null;

        if (queryCommand.Cache && storeInCache)
        {
            queryPlan = new QueryPlan(queryCommand, _expCache);
            _queryPlanCache.TryGetValue(queryPlan, out planCache);
        }

        if (planCache is null)
        {
            if ((Logger?.IsEnabled(LogLevel.Debug) ?? false) && storeInCache && queryCommand.Cache)
                Logger.LogDebug("Query plan cache miss with hash: {hash}", queryPlan!.GetHashCode());

            var (sql, @params) = makeSelect == null
                ? MakeSelect(queryCommand, false)
                : makeSelect();

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

            dbCommand.Connection = GetConnection();

            //dbCommand.Prepare();
            //return new SqlCacheEntry(null) { Enumerator = new EmptyEnumerator<TResult>() };

            var compiledQuery = new DbCompiledQuery<TResult>(dbCommand, map, queryCommand.SingleRow);

            if (createEnumerator)
            {
                var enumerator = new ResultSetEnumerator<TResult>(this, compiledQuery!);
                compiledQuery.Enumerator = enumerator;
            }

            if (storeInCache && queryCommand.Cache)
            {
                var compiledPlan = new DbCompiledPlan<TResult>(makeSelect is null ? sql! : null, map, noParams);
                _queryPlanCache[queryPlan!.GetCacheVersion()] = compiledPlan;
                compiledPlan.QueryTemplate = compiledQuery;
            }

            return compiledQuery;
        }
        else //if (planCache.CompiledQuery is DatabaseCompiledPlan<TResult> plan)
        {
#if DEBUG
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query plan cache hit");
#endif

            var plan = (DbCompiledPlan<TResult>)planCache;
            // var dbCommand = plan.GetCommand(plan.NoParams
            //     ? Array.Empty<Param>()
            //     : ExtractParams(queryCommand), this);

            var compiledQuery = plan.QueryTemplate;

            if (makeSelect is not null || string.IsNullOrEmpty(plan.SqlStmt))
            {
                var (sql, @params) = makeSelect == null
                    ? MakeSelect(queryCommand, false)
                    : makeSelect();

                var dbCommand = compiledQuery?.DbCommand;
                if (dbCommand is not null)
                {
                    dbCommand.CommandText = sql;
                    if (@params?.Any() == true)
                    {
                        dbCommand.Parameters.Clear();
                    }
                }
                else
                {
                    dbCommand = CreateCommand(sql!);
                }

                if (@params?.Any() == true)
                    dbCommand.Parameters.AddRange(@params.Select(it => CreateParam(it.Name, it.Value)).ToArray());

                compiledQuery ??= new DbCompiledQuery<TResult>(dbCommand, plan.MapDelegate, queryCommand.SingleRow);
            }
            else
            {
                if (!plan.NoParams || compiledQuery?.DbCommand is null)
                {
                    var dbCommand = compiledQuery?.DbCommand ?? CreateCommand(plan.SqlStmt);

                    if (!plan.NoParams)
                    {
                        // if (compiledQuery?.DbCommand is not null)
                        //     dbCommand.Parameters.Clear();

                        // dbCommand.Parameters.AddRange(ExtractParams(queryCommand).Select(it => CreateParam(it.Name, it.Value)).ToArray());
                        var pp = ExtractParams(queryCommand);
                        for (int i = 0; i < pp.Count; i++)
                        {
                            dbCommand.Parameters[i].Value = pp[i].Value;
                            Debug.Assert(dbCommand.Parameters[i].ParameterName == pp[i].Name, $"ParameterName {dbCommand.Parameters[i].ParameterName} not equals {pp[i].Name}");
                        }
                    }

                    compiledQuery ??= new DbCompiledQuery<TResult>(dbCommand, plan.MapDelegate, queryCommand.SingleRow);
                }
            }

            if (plan.QueryTemplate is null)
            {
                if (createEnumerator)
                {
                    var enumerator = new ResultSetEnumerator<TResult>(this, compiledQuery!);
                    compiledQuery.Enumerator = enumerator;
                }

                plan.QueryTemplate = compiledQuery;

            }

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
        var pageApplyed = false;

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
                pageApplyed = true;
            }

            foreach (var item in selectList)
            {
                var (needAliasForColumn, column) = MakeColumn(item, entityType, cmd, false, @params, paramMode);

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

            // if (cmd.Joins.Any())
            // {
            foreach (var join in cmd.Joins)
            {
                var joinSql = MakeJoin(join, cmd, @params, paramMode);
                if (!paramMode) sqlBuilder!.Append(joinSql);
            }
            //}

            if (cmd.PreparedCondition is not null)
            {
                var whereSql = MakeWhere(entityType, cmd, cmd.PreparedCondition, 0, null, @params, paramMode);
                if (!paramMode) sqlBuilder!.AppendLine().Append(" where ").Append(whereSql);
            }

            if (cmd.Sorting is not null)
            {
                if (!paramMode) sqlBuilder!.AppendLine().Append(" order by ");

                foreach (var sorting in cmd.Sorting)
                {
                    var sortingSql = MakeSort(entityType, cmd, sorting.PreparedExpression!, 0, null, @params, paramMode);
                    if (!paramMode)
                    {
                        sqlBuilder!.Append(sortingSql);
                        if (sorting.Direction == OrderDirection.Desc)
                            sqlBuilder.Append(" desc");

                        sqlBuilder.Append(", ");
                    }
                }

                if (!paramMode) sqlBuilder!.Length -= 2;
            }
            else if (!pageApplyed && RequireSorting(cmd) && !paramMode)
            {
                sqlBuilder!.AppendLine().Append(" order by ").Append(EmptySorting());
            }

            if (!pageApplyed && !paramMode && !cmd.Paging.IsEmpty)
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
#pragma warning disable C107 // Methods should not have too many parameters
    private string MakeSort(Type entityType, ISourceProvider tableSource, Expression sorting, int dim, IAliasProvider? aliasProvider, IParamProvider paramProvider, IQueryProvider queryProvider, List<Param> @params, bool paramMode)
#pragma warning restore C107 // Methods should not have too many parameters
    {
        // if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Where expression: {exp}", condition);
        using var visitor = new BaseExpressionVisitor(entityType, this, tableSource, dim, aliasProvider, paramProvider, queryProvider, true, paramMode);
        visitor.Visit(sorting);
        @params.AddRange(visitor.Params);

        if (paramMode) return string.Empty;

        return visitor.ToString();
    }
    public (bool NeedAliasForColumn, string Column) MakeColumn(SelectExpression selectExp, Type entityType, IQueryContext queryContext, bool dontNeedAlias, List<Param> @params, bool paramMode)
        => MakeColumn(selectExp, entityType, queryContext, dontNeedAlias, queryContext, queryContext, @params, paramMode);
    public (bool NeedAliasForColumn, string Column) MakeColumn(SelectExpression selectExp, Type entityType, ISourceProvider tableProvider, bool dontNeedAlias, IParamProvider paramProvider, IQueryProvider queryProvider, List<Param> @params, bool paramMode)
    {
        using var visitor = new BaseExpressionVisitor(entityType, this, tableProvider, 0, null, paramProvider, queryProvider, dontNeedAlias, paramMode);
        visitor.Visit(selectExp.Expression);
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
            if (queryCommand.Joins.Any() && srcType.IsAssignableTo(typeof(IProjection)))
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
        // var sqlTableAttr = t.GetCustomAttribute<SqlTableAttribute>(true);

        // if (sqlTableAttr is not null)
        //     return new FromExpression(sqlTableAttr.Name);
        // else
        // {
        //     var tableAttr = t.GetCustomAttribute<TableAttribute>(true);

        //     if (tableAttr is not null)
        //         return new FromExpression(tableAttr.Name);
        // }

        // foreach (var interf in t.GetInterfaces())
        // {
        //     sqlTableAttr = interf.GetCustomAttribute<SqlTableAttribute>(true);

        //     if (sqlTableAttr is not null)
        //         return new FromExpression(sqlTableAttr.Name);
        //     else
        //     {
        //         var tableAttr = interf.GetCustomAttribute<TableAttribute>(true);

        //         if (tableAttr is not null)
        //             return new FromExpression(tableAttr.Name);
        //     }
        // }

        if (_metadata.TryGetValue(t, out var entity) && !string.IsNullOrEmpty(entity.TableName))
            return new FromExpression(entity.TableName);

        return new FromExpression(GetTableName(t));
    }
    public Expression MapColumn(SelectExpression column, Expression param)
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
    public void Compile<TResult>(string sql, object? @params, QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken)
    {
        if (!queryCommand.IsPrepared)
            queryCommand.PrepareCommand(!storeInCache, cancellationToken);

        List<Param> ps = new();
        if (@params is not null)
        {
            var t = @params.GetType();
            foreach (var prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                ps.Add(new Param(prop.Name, prop.GetValue(@params)));
            }
        }

        queryCommand._compiledQuery = CreateCompiledQuery(queryCommand, !nonStreamUsing, storeInCache, () => (sql, ps));
    }
    public void Compile<TResult>(QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken)
    {
        if (!queryCommand.IsPrepared)
            queryCommand.PrepareCommand(!storeInCache, cancellationToken);

        queryCommand._compiledQuery = CreateCompiledQuery(queryCommand, !nonStreamUsing, storeInCache);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing && _conn is not null)
            {
                if (_connCreated)
                    _conn.Dispose();
                else
                    _conn.StateChange -= OnStateChanged;
            }

            _disposedValue = true;
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
        if (_conn is not null)
        {
            if (_connCreated)
                _conn.Dispose();
            else
                _conn.StateChange -= OnStateChanged;
        }
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        var compiledQuery = queryCommand.GetCompiledQuery() ?? CreateCompiledQuery(queryCommand, true, true);
        var sqlEnumerator = compiledQuery.Enumerator!;
        sqlEnumerator.InitEnumerator(@params, cancellationToken);
        return sqlEnumerator;
    }
    public async Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        var compiledQuery = queryCommand.GetCompiledQuery() ?? CreateCompiledQuery(queryCommand, true, true);

        var sqlEnumerator = compiledQuery.Enumerator!;
        await sqlEnumerator.InitReaderAsync(@params, cancellationToken).ConfigureAwait(false);
        return sqlEnumerator;
    }
    public async Task<List<TResult>> ToListAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        ////ArgumentNullException.ThrowIfNull(queryCommand);

        // var (reader, compiledQuery) = await ExecuteReader(queryCommand, @params, cancellationToken).ConfigureAwait(false);
        var compiledQuery = await GetCompiledQuery(queryCommand, @params, cancellationToken).ConfigureAwait(false);
        var sqlCommand = compiledQuery.DbCommand;
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
    public List<TResult> ToList<TResult>(QueryCommand<TResult> queryCommand, object[]? @params)
    {
        ////ArgumentNullException.ThrowIfNull(queryCommand);

        // var (reader, compiledQuery) = CreateReader(queryCommand, @params);
        var compiledQuery = GetCompiledQuery(queryCommand, @params);
        var sqlCommand = compiledQuery.DbCommand;
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
    public TResult? ExecuteScalar<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, bool throwIfNull)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        // GetCompiledQuery(queryCommand, @params, out var conn, out var sqlCommand);
        var compiledQuery = GetCompiledQuery(queryCommand, @params);
        var sqlCommand = compiledQuery.DbCommand;

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
    public async Task<TResult?> ExecuteScalar<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        // GetCompiledQuery(queryCommand, @params, out var conn, out var sqlCommand);
        var compiledQuery = await GetCompiledQuery(queryCommand, @params, cancellationToken);
        var sqlCommand = compiledQuery.DbCommand;

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
    public IEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[]? @params)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        var compiledQuery = queryCommand.GetCompiledQuery() ?? CreateCompiledQuery(queryCommand, true, true);

        var sqlEnumerator = compiledQuery.Enumerator!;
        sqlEnumerator.InitReader(@params);

        return sqlEnumerator;
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
                var vis = new ReplaceMemberVisitor(queryCommand.EntityType!, this, queryCommand, param);
                var body = vis.Visit(((LambdaExpression)queryCommand.SelectList![0].Expression).Body);
                lambda = Expression.Lambda<Func<IDataRecord, TResult>>(body, param);
            }
            else
            {
                var ctorInfo = resultType.GetConstructors().OrderByDescending(it => it.GetParameters().Length).FirstOrDefault() ?? throw new PrepareException($"Cannot get ctor from {resultType}");

                if (ctorInfo.GetParameters().Length == queryCommand.SelectList!.Count)
                {
                    var newParams = queryCommand.SelectList!.Select(column => MapColumn(column, param)).ToArray();
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
                        return Expression.Bind(propInfo, MapColumn(column, param));
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

    public TResult First<TResult>(QueryCommand<TResult> queryCommand, object[] @params)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        if (queryCommand.OneColumn)
        {
            return ExecuteScalar(queryCommand, @params, true)!;
        }
        else
        {
            // var (reader, compiledQuery) = CreateReader(queryCommand, @params);
            var compiledQuery = GetCompiledQuery(queryCommand, @params);
            var sqlCommand = compiledQuery.DbCommand;
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
    }

    public async Task<TResult> FirstAsync<TResult>(QueryCommand<TResult> queryCommand, object[] @params, CancellationToken cancellationToken)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        if (queryCommand.OneColumn)
        {
            return (await ExecuteScalar(queryCommand, @params, true, cancellationToken))!;
        }
        else
        {
            // var (reader, compiledQuery) = await ExecuteReader(queryCommand, @params, cancellationToken).ConfigureAwait(false);
            var compiledQuery = await GetCompiledQuery(queryCommand, @params, cancellationToken).ConfigureAwait(false);
            var sqlCommand = compiledQuery.DbCommand;
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
    }
    public TResult? FirstOrDefault<TResult>(QueryCommand<TResult> queryCommand, object[] @params)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        if (queryCommand.OneColumn)
        {
            return ExecuteScalar(queryCommand, @params, false);
        }
        else
        {
            // if (!_connOpen)
            // {
            //     if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
            //     conn.Open();
            //     _connOpen = true;
            // }

            //_logParams?.Invoke(sqlCommand);
            //Logger?.LogInformation(compiledQuery.Behavior.ToString("g"));
            var compiledQuery = GetCompiledQuery(queryCommand, @params);
            var sqlCommand = compiledQuery.DbCommand;
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
    }

    public async Task<TResult?> FirstOrDefaultAsync<TResult>(QueryCommand<TResult> queryCommand, object[] @params, CancellationToken cancellationToken)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        if (queryCommand.OneColumn)
        {
            return await ExecuteScalar(queryCommand, @params, false, cancellationToken);
        }
        else
        {
            //var (reader, compiledQuery) = await ExecuteReader(queryCommand, @params, cancellationToken).ConfigureAwait(false);
            var compiledQuery = await GetCompiledQuery(queryCommand, @params, cancellationToken).ConfigureAwait(false);
            var sqlCommand = compiledQuery.DbCommand;
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
    }

    public TResult Single<TResult>(QueryCommand<TResult> queryCommand, object[] @params)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        // var (reader, compiledQuery) = CreateReader(queryCommand, @params);
        var compiledQuery = GetCompiledQuery(queryCommand, @params);
        var sqlCommand = compiledQuery.DbCommand;
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

    public async Task<TResult> SingleAsync<TResult>(QueryCommand<TResult> queryCommand, object[] @params, CancellationToken cancellationToken)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        // var (reader, compiledQuery) = await ExecuteReader(queryCommand, @params, cancellationToken).ConfigureAwait(false);
        var compiledQuery = await GetCompiledQuery(queryCommand, @params, cancellationToken).ConfigureAwait(false);
        var sqlCommand = compiledQuery.DbCommand;
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

    public TResult? SingleOrDefault<TResult>(QueryCommand<TResult> queryCommand, object[] @params)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        // var (reader, compiledQuery) = CreateReader(queryCommand, @params);
        var compiledQuery = GetCompiledQuery(queryCommand, @params);
        var sqlCommand = compiledQuery.DbCommand;
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

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(QueryCommand<TResult> queryCommand, object[] @params, CancellationToken cancellationToken)
    {
        //ArgumentNullException.ThrowIfNull(queryCommand);

        // var (reader, compiledQuery) = await ExecuteReader(queryCommand, @params, cancellationToken).ConfigureAwait(false);
        var compiledQuery = await GetCompiledQuery(queryCommand, @params, cancellationToken).ConfigureAwait(false);
        var sqlCommand = compiledQuery.DbCommand;
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