#define PLAN_CACHE
#define ONLY_PLAN_CACHE

using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;


namespace nextorm.core;

public partial class DbContext : IDataContext
{
    private readonly static MethodInfo IsDBNullMI = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private readonly static IDictionary<Type, IEntityMeta> _metadata = new ConcurrentDictionary<Type, IEntityMeta>();
    private readonly static IDictionary<ExpressionKey, Delegate> _expCache = new ExpressionCache<Delegate>();
    internal protected readonly static ObjectPool<StringBuilder> _sbPool = new DefaultObjectPoolProvider().Create(new StringBuilderPooledObjectPolicy());

#if !ONLY_PLAN_CACHE
    private readonly IDictionary<QueryCommand, SqlCacheEntry> _queryCache = new Dictionary<QueryCommand, SqlCacheEntry>();
#endif
#if PLAN_CACHE
    private readonly IDictionary<QueryPlan, SqlCacheEntry> _queryPlanCache = new Dictionary<QueryPlan, SqlCacheEntry>();
#endif
    private DbConnection? _conn;
    // private bool _clearCache;
    private bool _disposedValue;
    public DbContext(DbContextBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (optionsBuilder.LoggerFactory is not null)
        {
            Logger = optionsBuilder.LoggerFactory.CreateLogger(GetType());
            CommandLogger = optionsBuilder.LoggerFactory.CreateLogger(typeof(QueryCommand));
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
    public ILogger? CommandLogger { get; set; }
    public Entity From(string table) => new(this, table) { Logger = CommandLogger };
    public DbConnection GetConnection()
    {
        if (_conn is null)
        {
            _conn = CreateConnection();
        }
        return _conn;
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
    public virtual DbCommand CreateCommand(string sql)
    {
        throw new NotImplementedException();
    }

    public virtual string GetTableName(Type type)
    {
        throw new NotImplementedException(type.ToString());
    }
    private SqlCacheEntry CreateCompiledQuery<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
    {
        DatabaseCompiledQuery<TResult>? compiledQuery = null;
#if PLAN_CACHE
        var queryPlan = new QueryPlan(queryCommand, _expCache);
        if (!queryCommand.Cache
            || !_queryPlanCache.TryGetValue(queryPlan, out var planCache))
#endif            
        {
#if PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query plan cache miss with hash: {hash}",
                queryPlan.GetHashCode());
#endif            
            var (sql, @params) = MakeSelect(queryCommand, false);
            // var (sql, @params) = MakeSelect(queryCommand, true);
            // var cacheEntryX = new SqlCacheEntry(null) { Enumerator = new EmptyEnumerator<TResult>() };
            // if (queryCommand.Cache)
            //     _queryCache[queryCommand] = cacheEntryX;
            // return cacheEntryX;

            Func<IDataRecord, TResult>? map = queryCommand.SingleRow && queryCommand.OneColumn
                ? null
                : GetMap(queryCommand)();

#if PLAN_CACHE
            var compiledPlan = new DatabaseCompiledPlan<TResult>(sql!, map, @params.Count == 0);
            if (queryCommand.Cache && storeInCache)
            {
                planCache = new SqlCacheEntry(compiledPlan);

                //    if (queryCommand.Cache)
                _queryPlanCache[queryPlan.GetCacheVersion()] = planCache;
            }
#endif
            var dbCommand = CreateCommand(sql!);
            dbCommand.Parameters.AddRange(@params.Select(it => CreateParam(it.Name, it.Value)).ToArray());
            //dbCommand.Prepare();
            //return new SqlCacheEntry(null) { Enumerator = new EmptyEnumerator<TResult>() };

            compiledQuery = new DatabaseCompiledQuery<TResult>(dbCommand, map, queryCommand.SingleRow);

            var cacheEntry = new SqlCacheEntry(compiledQuery);

            if (createEnumerator)
            {
                var enumerator = new ResultSetEnumerator<TResult>(this, compiledQuery!);
                cacheEntry.Enumerator = enumerator;
            }
#if PLAN_CACHE
            compiledPlan.CacheEntry = cacheEntry;
#endif
            return cacheEntry;
        }
#if PLAN_CACHE
        else //if (planCache.CompiledQuery is DatabaseCompiledPlan<TResult> plan)
        {

#if DEBUG
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query plan cache hit");
#endif

            var plan = (DatabaseCompiledPlan<TResult>)planCache.CompiledQuery;
            // var dbCommand = plan.GetCommand(plan.NoParams
            //     ? Array.Empty<Param>()
            //     : ExtractParams(queryCommand), this);

            compiledQuery = plan.CacheEntry?.CompiledQuery as DatabaseCompiledQuery<TResult>;

            //if (plan.CacheEntry is not null && plan.CacheEntry.CompiledQuery is DatabaseCompiledQuery<TResult> cq)
            //{
            if (!plan.NoParams || compiledQuery?.DbCommand is null)
            {
                var dbCommand = compiledQuery?.DbCommand ?? CreateCommand(plan._sql);
                if (compiledQuery?.DbCommand is not null)
                    dbCommand.Parameters.Clear();

                dbCommand.Parameters.AddRange(ExtractParams(queryCommand).Select(it => CreateParam(it.Name, it.Value)).ToArray());

                compiledQuery ??= new DatabaseCompiledQuery<TResult>(dbCommand, plan.MapDelegate, queryCommand.SingleRow);
            }

            //return plan.CacheEntry;
            //}
            // var dbCommand = CreateCommand(plan._sql);

            // dbCommand.Parameters.Add(CreateParam(MakeParam("p"), 1));

            if (plan.CacheEntry is null)
            {
                var cacheEntry = new SqlCacheEntry(compiledQuery);

                if (createEnumerator)
                {
                    var enumerator = new ResultSetEnumerator<TResult>(this, compiledQuery!);
                    cacheEntry.Enumerator = enumerator;
                }

                plan.CacheEntry = cacheEntry;

            }
            return plan.CacheEntry;
        }
#endif
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

        if (!paramMode) sqlBuilder!.Append("select ");
        if (cmd.IgnoreColumns || selectList is null)
        {
            if (!paramMode) sqlBuilder!.Append("*, ");
        }
        else
        {
            foreach (var item in selectList)
            {
                var (needAliasForColumn, column) = MakeColumn(item, entityType, cmd, false, cmd, cmd, @params, paramMode);

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

            if (cmd.Condition is not null)
            {
                var whereSql = MakeWhere(entityType, cmd, cmd.Condition, 0, null, cmd, cmd, @params, paramMode);
                if (!paramMode) sqlBuilder!.Append(" where ").Append(whereSql);
            }

            if (cmd.Sorting is not null)
            {
                if (!paramMode) sqlBuilder!.Append(" order by ");

                foreach (var sorting in cmd.Sorting)
                {
                    var sortingSql = MakeSort(entityType, cmd, sorting.PreparedExpression, 0, null, cmd, cmd, @params, paramMode);
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

            if (!paramMode)
            {
                if (cmd.Paging.Limit > 0 || cmd.Paging.Offset > 0)
                {
                    sqlBuilder!.Append(' ');
                    MakePage(cmd.Paging, sqlBuilder);
                }
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
                var (sql, p) = MakeSelect(join.Query, paramMode);
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

        var whereSql = MakeWhere(cmd.EntityType!, cmd, visitor.JoinCondition, dim, dim == 1
                        ? new ExpressionAliasProvider(join.JoinCondition)
                        : new ProjectionAliasProvider(dim, cmd.EntityType!), cmd, cmd, @params, paramMode);

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
    private string MakeWhere(Type entityType, ISourceProvider tableSource, Expression condition, int dim, IAliasProvider? aliasProvider, IParamProvider paramProvider, IQueryProvider queryProvider, List<Param> @params, bool paramMode)
    {
        // if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Where expression: {exp}", condition);
        using var visitor = new WhereExpressionVisitor(entityType, this, tableSource, dim, aliasProvider, paramProvider, queryProvider, paramMode);
        visitor.Visit(condition);
        @params.AddRange(visitor.Params);

        if (paramMode) return string.Empty;

        return visitor.ToString();
    }
    private string MakeSort(Type entityType, ISourceProvider tableSource, Expression sorting, int dim, IAliasProvider? aliasProvider, IParamProvider paramProvider, IQueryProvider queryProvider, List<Param> @params, bool paramMode)
    {
        // if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Where expression: {exp}", condition);
        using var visitor = new BaseExpressionVisitor(entityType, this, tableSource, dim, aliasProvider, paramProvider, queryProvider, true, paramMode);
        visitor.Visit(sorting);
        @params.AddRange(visitor.Params);

        if (paramMode) return string.Empty;

        return visitor.ToString();
    }

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
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var ce = queryCommand.CacheEntry;
        if (ce is SqlCacheEntry cacheEntry
#if !ONLY_PLAN_CACHE
            || (queryCommand.Cache && _queryCache.TryGetValue(queryCommand, out cacheEntry!) && cacheEntry is not null)
#endif
            )
        {
            // if (cacheEntry.Enumerator is not ResultSetEnumerator<TResult> enumerator)
            // {
            //     enumerator = new ResultSetEnumerator<TResult>(this, (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery, cancellationToken);
            //     cacheEntry.Enumerator = enumerator;
            // }
            // var sqlEnumerator = (ResultSetEnumerator<TResult>)cacheEntry.Enumerator!;
            // sqlEnumerator.InitReader(cancellationToken);
            // return sqlEnumerator;
        }
        else
        {
#if DEBUG && !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss with hash: {hash} and condition hash: {chash}",
                queryCommand.GetHashCode(),
                queryCommand.ConditionHash);
#elif !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss");
#endif

            cacheEntry = CreateCompiledQuery(queryCommand, true, true, cancellationToken);

#if !ONLY_PLAN_CACHE
            if (queryCommand.Cache)
                _queryCache[queryCommand] = cacheEntry;
#endif
        }
        var sqlEnumerator = (ResultSetEnumerator<TResult>)cacheEntry.Enumerator!;
        sqlEnumerator.InitEnumerator(@params, cancellationToken);
        return sqlEnumerator;
    }
    private List<Param> ExtractParams(QueryCommand queryCommand) => MakeSelect(queryCommand, true).Item2;
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
    public void Compile<TResult>(QueryCommand<TResult> cmd, bool nonStreamCalls, CancellationToken cancellationToken)
    {
        if (!cmd.IsPrepared)
            cmd.PrepareCommand(cancellationToken);

        cmd.CacheEntry = CreateCompiledQuery(cmd, !nonStreamCalls, false, cancellationToken);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _conn?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~SqlDataProvider()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        if (_conn is not null)
            return _conn.DisposeAsync();

        return ValueTask.CompletedTask;
    }

    public async Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var ce = queryCommand.CacheEntry;
        if (ce is SqlCacheEntry cacheEntry
#if !ONLY_PLAN_CACHE
            || (queryCommand.Cache && _queryCache.TryGetValue(queryCommand, out cacheEntry!) && cacheEntry is not null)
#endif
            )
        {
            // if (cacheEntry.Enumerator is not ResultSetEnumerator<TResult> enumerator)
            // {
            //     enumerator = new ResultSetEnumerator<TResult>(this, (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery, cancellationToken);
            //     cacheEntry.Enumerator = enumerator;
            // }
            // var sqlEnumerator = (ResultSetEnumerator<TResult>)cacheEntry.Enumerator!;
            // await sqlEnumerator.InitReaderAsync(cancellationToken, @params);
            // return sqlEnumerator;
        }
        else
        {
#if DEBUG && !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss with hash: {hash} and condition hash: {chash}",
                queryCommand.GetHashCode(),
                queryCommand.ConditionHash);
#elif !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss");
#endif

            cacheEntry = CreateCompiledQuery(queryCommand, true, true, cancellationToken);

#if !ONLY_PLAN_CACHE
            if (queryCommand.Cache)
                _queryCache[queryCommand] = cacheEntry;
#endif

        }

        var sqlEnumerator = (ResultSetEnumerator<TResult>)cacheEntry.Enumerator!;
        await sqlEnumerator.InitReaderAsync(@params, cancellationToken).ConfigureAwait(false);
        return sqlEnumerator;
    }
    public async Task<List<TResult>> ToListAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var (reader, cacheEntry, compiledQuery) = await CreateReader(queryCommand, @params, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            var l = new List<TResult>(cacheEntry.LastRowCount);
            while (reader.Read())
            {
                l.Add(compiledQuery.MapDelegate(reader));
            }

            cacheEntry.LastRowCount = l.Count;
            return l;
        }
    }
    public List<TResult> ToList<TResult>(QueryCommand<TResult> queryCommand, object[]? @params)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var (reader, cacheEntry, compiledQuery) = CreateReader(queryCommand, @params);
        using (reader)
        {
            var l = new List<TResult>(cacheEntry.LastRowCount);
            while (reader.Read())
            {
                l.Add(compiledQuery.MapDelegate(reader));
            }

            cacheEntry.LastRowCount = l.Count;
            return l;
        }
    }
    private (DbDataReader, SqlCacheEntry, DatabaseCompiledQuery<TResult>) CreateReader<TResult>(QueryCommand<TResult> queryCommand, object[]? @params)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var setParams = @params is not null;
        var ce = queryCommand.CacheEntry;
        if (ce is SqlCacheEntry cacheEntry
#if !ONLY_PLAN_CACHE
            || (queryCommand.Cache && _queryCache.TryGetValue(queryCommand, out cacheEntry!) && cacheEntry is not null)
#endif
            )
        {
            // if (cacheEntry.Enumerator is not ResultSetEnumerator<TResult> enumerator)
            // {
            //     enumerator = new ResultSetEnumerator<TResult>(this, (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery, cancellationToken);
            //     cacheEntry.Enumerator = enumerator;
            // }
            // var sqlEnumerator = (ResultSetEnumerator<TResult>)cacheEntry.Enumerator!;
            // sqlEnumerator.InitReader(cancellationToken);
            // return sqlEnumerator;
        }
        else
        {
#if DEBUG && !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss with hash: {hash} and condition hash: {chash}",
                queryCommand.GetHashCode(),
                queryCommand.ConditionHash);
#elif !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss");
#endif

            cacheEntry = CreateCompiledQuery(queryCommand, true, true, CancellationToken.None);

#if !ONLY_PLAN_CACHE
            if (queryCommand.Cache)
                _queryCache[queryCommand] = cacheEntry;
#endif
        }

        var compiledQuery = (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery;

#if DEBUG
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
#endif
        var conn = GetConnection();
        var sqlCommand = compiledQuery.DbCommand;

        if (setParams)
        {
            for (var i = 0; i < @params!.Length; i++)
            {
                var paramName = string.Format("norm_p{0}", i);
                foreach (DbParameter p in sqlCommand.Parameters)
                {
                    if (p.ParameterName == paramName)
                    {
                        p.Value = @params[i];
                        break;
                    }
                }
            }
        }
        sqlCommand.Connection = conn;

        if (conn.State == ConnectionState.Closed)
        {
#if DEBUG
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
#endif
            conn.Open();
        }

#if DEBUG
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
#endif
        return (sqlCommand.ExecuteReader(compiledQuery.Behavior), cacheEntry, compiledQuery);
    }
    public (TResult? result, bool isNull) ExecuteScalar<TResult>(QueryCommand<TResult> queryCommand, object[]? @params)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var setParams = @params is not null;
        var ce = queryCommand.CacheEntry;
        if (ce is not SqlCacheEntry cacheEntry)
        {
#if !ONLY_PLAN_CACHE
            //QueryCommandKey? cmdKey = null;
            if (queryCommand.Cache)
            {
                //cmdKey = new QueryCommandKey(queryCommand, @params);
                if (_queryCache.TryGetValue(queryCommand, out cacheEntry!))
                {
                    goto hasCacheEtry;
                }
            }
#endif
#if DEBUG && !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss with hash: {hash} and condition hash: {chash}",
                queryCommand.GetHashCode(),
                queryCommand.ConditionHash);
#elif !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss");
#endif

            cacheEntry = CreateCompiledQuery(queryCommand, false, true, CancellationToken.None);

#if !ONLY_PLAN_CACHE
            if (queryCommand.Cache)
                _queryCache[queryCommand] = cacheEntry;
#endif
        }

#if !ONLY_PLAN_CACHE
    hasCacheEtry:
#endif
        var compiledQuery = (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery;

#if DEBUG
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
#endif
        var conn = GetConnection();
        var sqlCommand = compiledQuery.DbCommand;

        if (setParams)
        {
            for (var i = 0; i < @params!.Length; i++)
            {
                var paramName = string.Format("norm_p{0}", i);
                foreach (DbParameter p in sqlCommand.Parameters)
                {
                    if (p.ParameterName == paramName)
                    {
                        p.Value = @params[i];
                        break;
                    }
                }
            }
        }
        sqlCommand.Connection = conn;

        if (conn.State == ConnectionState.Closed)
        {
#if DEBUG
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
#endif
            conn.Open();
        }

#if DEBUG
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
#endif

        var r = sqlCommand.ExecuteScalar();

        if (r is null or DBNull) return (default, true);
        if (r is TResult res) return (res, false);
        var type = typeof(TResult);
        return ((TResult)Convert.ChangeType(r, type), false);
    }
    public async Task<(TResult? result, bool isNull)> ExecuteScalar<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var setParams = @params is not null;
        var ce = queryCommand.CacheEntry;
        if (ce is not SqlCacheEntry cacheEntry)
        {
#if !ONLY_PLAN_CACHE
            //QueryCommandKey? cmdKey = null;
            if (queryCommand.Cache)
            {
                //cmdKey = new QueryCommandKey(queryCommand, @params);
                if (_queryCache.TryGetValue(queryCommand, out cacheEntry!))
                {
                    goto hasCacheEtry;
                }
            }
#endif
#if DEBUG && !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss with hash: {hash} and condition hash: {chash}",
                queryCommand.GetHashCode(),
                queryCommand.ConditionHash);
#elif !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss");
#endif

            cacheEntry = CreateCompiledQuery(queryCommand, false, true, cancellationToken);

#if !ONLY_PLAN_CACHE
            if (queryCommand.Cache)
                _queryCache[queryCommand] = cacheEntry;
#endif
        }

#if !ONLY_PLAN_CACHE
    hasCacheEtry:
#endif
        var compiledQuery = (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery;

#if DEBUG
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
#endif
        var conn = GetConnection();
        var sqlCommand = compiledQuery.DbCommand;

        if (setParams)
        {
            for (var i = 0; i < @params!.Length; i++)
            {
                var paramName = string.Format("norm_p{0}", i);
                foreach (DbParameter p in sqlCommand.Parameters)
                {
                    if (p.ParameterName == paramName)
                    {
                        p.Value = @params[i];
                        break;
                    }
                }
            }
        }
        sqlCommand.Connection = conn;

        if (conn.State == ConnectionState.Closed)
        {
#if DEBUG
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
#endif
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

#if DEBUG
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
#endif
        var r = await sqlCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        // var r = true;
        if (r is null or DBNull) return (default, true);
        if (r is TResult res) return (res, false);
        var type = typeof(TResult);
        return ((TResult)Convert.ChangeType(r, type), false);
    }
    private async Task<(DbDataReader, SqlCacheEntry, DatabaseCompiledQuery<TResult>)> CreateReader<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var setParams = @params is not null;
        var ce = queryCommand.CacheEntry;
        if (ce is not SqlCacheEntry cacheEntry)
        {
#if !ONLY_PLAN_CACHE
            //QueryCommandKey? cmdKey = null;
            if (queryCommand.Cache)
            {
                //cmdKey = new QueryCommandKey(queryCommand, @params);
                if (_queryCache.TryGetValue(queryCommand, out cacheEntry!))
                {
                    goto hasCacheEtry;
                }
            }
#endif
#if DEBUG && !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss with hash: {hash} and condition hash: {chash}",
                queryCommand.GetHashCode(),
                queryCommand.ConditionHash);
#elif !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss");
#endif

            cacheEntry = CreateCompiledQuery(queryCommand, false, true, cancellationToken);

#if !ONLY_PLAN_CACHE
            if (queryCommand.Cache)
                _queryCache[queryCommand] = cacheEntry;
#endif
        }

#if !ONLY_PLAN_CACHE
    hasCacheEtry:
#endif
        var compiledQuery = (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery;

#if DEBUG
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Getting connection");
#endif
        var conn = GetConnection();
        var sqlCommand = compiledQuery.DbCommand;

        if (setParams)
        {
            for (var i = 0; i < @params!.Length; i++)
            {
                var paramName = string.Format("norm_p{0}", i);
                foreach (DbParameter p in sqlCommand.Parameters)
                {
                    if (p.ParameterName == paramName)
                    {
                        p.Value = @params[i];
                        break;
                    }
                }
            }
        }
        sqlCommand.Connection = conn;

        if (conn.State == ConnectionState.Closed)
        {
#if DEBUG
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Opening connection");
#endif
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

#if DEBUG
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
#endif

        return (await sqlCommand.ExecuteReaderAsync(compiledQuery.Behavior, cancellationToken).ConfigureAwait(false), cacheEntry, compiledQuery);
    }
    public IEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[] @params)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var ce = queryCommand.CacheEntry;
        if (ce is SqlCacheEntry cacheEntry
#if !ONLY_PLAN_CACHE
            || (queryCommand.Cache && _queryCache.TryGetValue(queryCommand, out cacheEntry!) && cacheEntry is not null)
#endif
            )
        {
            // if (cacheEntry.Enumerator is not ResultSetEnumerator<TResult> enumerator)
            // {
            //     enumerator = new ResultSetEnumerator<TResult>(this, (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery, cancellationToken);
            //     cacheEntry.Enumerator = enumerator;
            // }
            // var sqlEnumerator = (ResultSetEnumerator<TResult>)cacheEntry.Enumerator!;
            // sqlEnumerator.InitReader(cancellationToken);
            // return sqlEnumerator;
        }
        else
        {
#if DEBUG && !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss with hash: {hash} and condition hash: {chash}",
                queryCommand.GetHashCode(),
                queryCommand.ConditionHash);
#elif !ONLY_PLAN_CACHE
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Query cache miss");
#endif

            cacheEntry = CreateCompiledQuery(queryCommand, true, true, CancellationToken.None);

#if !ONLY_PLAN_CACHE
            if (queryCommand.Cache)
                _queryCache[queryCommand] = cacheEntry;
#endif
        }
        var sqlEnumerator = (ResultSetEnumerator<TResult>)cacheEntry.Enumerator!;
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

            var key = new ExpressionKey(lambda, _expCache, queryCommand);
            if (!_expCache.TryGetValue(key, out var d))
            {
                d = lambda.Compile();
                _expCache[key] = d;
            }

            return (Func<IDataRecord, TResult>)d;
        };

        //         (_dataProvider as SqlDataProvider).MapCache[key] = del;
        //     }
    }

    public TResult First<TResult>(QueryCommand<TResult> queryCommand, object[] @params)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        if (queryCommand.OneColumn)
        {
            var (result, isNull) = ExecuteScalar(queryCommand, @params);
            if (isNull) throw new InvalidOperationException();
            return result!;
        }
        else
        {
            var (reader, _, compiledQuery) = CreateReader(queryCommand, @params);
            using (reader)
            {
                if (reader.Read())
                {
                    return compiledQuery.MapDelegate(reader);
                }

                throw new InvalidOperationException();
            }
        }
    }

    public async Task<TResult> FirstAsync<TResult>(QueryCommand<TResult> queryCommand, object[] @params, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        if (queryCommand.OneColumn)
        {
            var (result, isNull) = await ExecuteScalar(queryCommand, @params, cancellationToken);
            if (isNull) throw new InvalidOperationException();
            return result!;
        }
        else
        {
            var (reader, _, compiledQuery) = await CreateReader(queryCommand, @params, cancellationToken).ConfigureAwait(false);
            using (reader)
            {
                if (reader.Read())
                {
                    return compiledQuery.MapDelegate(reader);
                }

                throw new InvalidOperationException();
            }
        }
    }
    public TResult? FirstOrDefault<TResult>(QueryCommand<TResult> queryCommand, object[] @params)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        if (queryCommand.OneColumn)
        {
            var (result, _) = ExecuteScalar(queryCommand, @params);
            return result;
        }
        else
        {
            var (reader, cacheEntry, compiledQuery) = CreateReader(queryCommand, @params);
            using (reader)
            {
                if (reader.Read())
                {
                    return compiledQuery.MapDelegate(reader);
                }

                return default;
            }
        }
    }

    public async Task<TResult?> FirstOrDefaultAsync<TResult>(QueryCommand<TResult> queryCommand, object[] @params, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        if (queryCommand.OneColumn)
        {
            var (result, _) = await ExecuteScalar(queryCommand, @params, cancellationToken);
            return result;
        }
        else
        {
            var (reader, _, compiledQuery) = await CreateReader(queryCommand, @params, cancellationToken).ConfigureAwait(false);
            using (reader)
            {
                if (reader.Read())
                {
                    return compiledQuery.MapDelegate(reader);
                }

                return default;
            }
        }
    }

    public TResult Single<TResult>(QueryCommand<TResult> queryCommand, object[] @params)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var (reader, _, compiledQuery) = CreateReader(queryCommand, @params);
        using (reader)
        {
            TResult r = default!;
            var hasResult = false;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = compiledQuery.MapDelegate(reader);
                hasResult = true;
            }

            if (!hasResult)
                throw new InvalidOperationException();

            return r!;
        }
    }

    public async Task<TResult> SingleAsync<TResult>(QueryCommand<TResult> queryCommand, object[] @params, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var (reader, _, compiledQuery) = await CreateReader(queryCommand, @params, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            TResult r = default!;
            var hasResult = false;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = compiledQuery.MapDelegate(reader);
                hasResult = true;
            }

            if (!hasResult)
                throw new InvalidOperationException();

            return r!;
        }
    }

    public TResult? SingleOrDefault<TResult>(QueryCommand<TResult> queryCommand, object[] @params)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var (reader, _, compiledQuery) = CreateReader(queryCommand, @params);
        using (reader)
        {
            TResult? r = default;
            var hasResult = false;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = compiledQuery.MapDelegate(reader);
                hasResult = true;
            }

            return r;
        }
    }

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(QueryCommand<TResult> queryCommand, object[] @params, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var (reader, _, compiledQuery) = await CreateReader(queryCommand, @params, cancellationToken).ConfigureAwait(false);
        using (reader)
        {
            TResult? r = default;
            var hasResult = false;
            while (reader.Read())
            {
                if (hasResult)
                    throw new InvalidOperationException();

                r = compiledQuery.MapDelegate(reader);
                hasResult = true;
            }

            return r;
        }
    }

    class EmptyEnumerator<TResult> : IAsyncEnumerator<TResult>, IEnumerator<TResult>
    {
        public TResult Current => default;

        object IEnumerator.Current => default;

        public void Dispose()
        {

        }

        public ValueTask DisposeAsync()
        {
            //throw new NotImplementedException();
            return ValueTask.CompletedTask;
        }

        public bool MoveNext()
        {
            return false;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return ValueTask.FromResult(false);
        }

        public void Reset()
        {

        }
    }
}