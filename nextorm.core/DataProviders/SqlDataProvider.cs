//#define PLAN_CACHE

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using OneOf;


namespace nextorm.core;

public class SqlDataProvider : IDataProvider
{
    private readonly IDictionary<QueryCommand, SqlCacheEntry> _queryCache = new Dictionary<QueryCommand, SqlCacheEntry>();
    private readonly IDictionary<ExpressionKey, Delegate> _expCache = new ExpressionCache<Delegate>();
#if PLAN_CACHE
    private readonly IDictionary<QueryPlan, SqlCacheEntry> _queryPlanCache = new Dictionary<QueryPlan, SqlCacheEntry>();
#endif
    private DbConnection? _conn;
    // private bool _clearCache;
    private bool disposedValue;
    internal readonly ObjectPool<StringBuilder> _sbPool = new DefaultObjectPoolProvider().Create(new StringBuilderPooledObjectPolicy());
    //private DbCommand? _cmd;
    public DbConnection GetConnection()
    {
        if (_conn is null)
        {
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Creating connection");
            _conn = CreateConnection();
        }
        return _conn;
    }
    internal bool LogSensetiveData { get; set; }
    public virtual string ConcatStringOperator => "+";
    public ILogger? Logger { get; set; }
    public bool NeedMapping => true;
    public IDictionary<ExpressionKey, Delegate> ExpressionsCache => _expCache;
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
    private SqlCacheEntry CreateCompiledQuery<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        DatabaseCompiledQuery<TResult>? compiledQuery = null;
#if PLAN_CACHE
        var queryPlan = new QueryPlan(queryCommand);
        if (!queryCommand.Cache
            || !_queryPlanCache.TryGetValue(queryPlan, out var planCache))
#endif            
        {
            var (sql, @params) = MakeSelect(queryCommand, false);
            // var (sql, @params) = MakeSelect(queryCommand, true);
            // var cacheEntryX = new SqlCacheEntry(null) { Enumerator = new EmptyEnumerator<TResult>() };
            // if (queryCommand.Cache)
            //     _queryCache[queryCommand] = cacheEntryX;
            // return cacheEntryX;

            var map = queryCommand.GetMap(typeof(TResult), typeof(DbDataReader));

#if PLAN_CACHE
            if (queryCommand.Cache)
            {
                var compiledPlan = new DatabaseCompiledPlan<TResult>(sql!, map, @params.Count == 0);

                planCache = new SqlCacheEntry(compiledPlan);

                //    if (queryCommand.Cache)
                _queryPlanCache[queryPlan] = planCache;
            }
#endif
            var dbCommand = CreateCommand(sql!);
            dbCommand.Parameters.AddRange(@params.Select(it => CreateParam(it.Name, it.Value)).ToArray());

            //return new SqlCacheEntry(null) { Enumerator = new EmptyEnumerator<TResult>() };

            compiledQuery = new DatabaseCompiledQuery<TResult>(dbCommand, map);
        }
#if PLAN_CACHE
        else if (planCache.CompiledQuery is DatabaseCompiledPlan<TResult> plan)
        {
            //var dbCommand = plan.GetCommand(ExtractParams(queryCommand), this);
            var dbCommand = CreateCommand(plan._sql);

            dbCommand.Parameters.Add(CreateParam(MakeParam("p"), 1));

            compiledQuery = new DatabaseCompiledQuery<TResult>(dbCommand, plan.MapDelegate);
        }
#endif
        var enumerator = new ResultSetEnumerator<TResult>(this, compiledQuery!, cancellationToken);

        var cacheEntry = new SqlCacheEntry(compiledQuery) { Enumerator = enumerator };

        return cacheEntry;
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
        ArgumentNullException.ThrowIfNull(cmd.SelectList);
        ArgumentNullException.ThrowIfNull(cmd.From);
        ArgumentNullException.ThrowIfNull(cmd.EntityType);

        var selectList = cmd.SelectList;
        var from = cmd.From;
        var entityType = cmd.EntityType;

        var sqlBuilder = paramMode ? null : _sbPool.Get();

        var @params = new List<Param>();

        if (!paramMode) sqlBuilder!.Append("select ");
        foreach (var item in selectList)
        {
            var (needAliasForColumn, column) = MakeColumn(item, entityType, cmd, false, @params, paramMode);

            if (!paramMode)
            {
                sqlBuilder!.Append(column);

                if (needAliasForColumn)
                {
                    sqlBuilder.Append(MakeColumnAlias(item.PropertyName!));
                }

                sqlBuilder.Append(", ");
            }
        }

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
            var whereSql = MakeWhere(entityType, cmd, cmd.Condition, 0, null, @params, paramMode);
            if (!paramMode) sqlBuilder!.Append(" where ").Append(whereSql);
        }

        string? r = null;
        if (!paramMode)
        {
            r = sqlBuilder!.ToString();

            _sbPool.Return(sqlBuilder);
        }

        return (r, @params);
    }

    private string MakeJoin(JoinExpression join, QueryCommand cmd, List<Param> @params, bool paramMode)
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

        var whereSql = MakeWhere(cmd.EntityType!, cmd, visitor.JoinCondition, dim, dim == 1
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
    private string MakeWhere(Type entityType, ISourceProvider tableSource, Expression condition, int dim, IAliasProvider? aliasProvider, List<Param> @params, bool paramMode)
    {
        if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Where expression: {exp}", condition);
        using var visitor = new WhereExpressionVisitor(entityType, this, tableSource, dim, aliasProvider, paramMode);
        visitor.Visit(condition);
        @params.AddRange(visitor.Params);

        if (paramMode) return string.Empty;

        return visitor.ToString();
    }

    public (bool NeedAliasForColumn, string Column) MakeColumn(SelectExpression selectExp, Type entityType, ISourceProvider tableProvider, bool dontNeedAlias, List<Param> @params, bool paramMode)
    {
        var expression = selectExp.Expression;

        return expression.Match(
            cmd => throw new NotImplementedException(),
            exp =>
            {
                using var visitor = new BaseExpressionVisitor(entityType, this, tableProvider, 0, null, dontNeedAlias, paramMode);
                visitor.Visit(exp);
                @params.AddRange(visitor.Params);

                if (paramMode) return (false, string.Empty);

                return (visitor.NeedAliasForColumn || selectExp.PropertyName != visitor.ColumnName, visitor.ToString());
            }
        );
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
    public virtual string MakeColumnAlias(string colAlias)
    {
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
    public IAsyncEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        var ce = queryCommand.CacheEntry;
        if (ce is SqlCacheEntry cacheEntry || (queryCommand.Cache && _queryCache.TryGetValue(queryCommand, out cacheEntry!) && cacheEntry is not null))
        {
            // if (cacheEntry.Enumerator is not ResultSetEnumerator<TResult> enumerator)
            // {
            //     enumerator = new ResultSetEnumerator<TResult>(this, (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery, cancellationToken);
            //     cacheEntry.Enumerator = enumerator;
            // }

            return (IAsyncEnumerator<TResult>)cacheEntry.Enumerator!;
        }
        else
        {
            if (Logger?.IsEnabled(LogLevel.Information) ?? false) Logger.LogInformation("Query cache miss");

            cacheEntry = CreateCompiledQuery(queryCommand, cancellationToken);

            if (queryCommand.Cache)
                _queryCache[queryCommand] = cacheEntry;

            return (IAsyncEnumerator<TResult>)cacheEntry.Enumerator!;
        }
    }
    private List<Param> ExtractParams(QueryCommand queryCommand) => MakeSelect(queryCommand, true).Item2;
    public QueryCommand<T> CreateCommand<T>(LambdaExpression exp, Expression? condition)
    {
        return new QueryCommand<T>(this, exp, condition);
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
        else throw new BuildSqlCommandException($"From must be specified for {nameof(TableAlias)} as source type");
    }

    private FromExpression GetFrom(Type t)
    {
        var sqlTableAttr = t.GetCustomAttribute<SqlTableAttribute>(true);

        if (sqlTableAttr is not null)
            return new FromExpression(sqlTableAttr.Name);
        else
        {
            var tableAttr = t.GetCustomAttribute<TableAttribute>(true);

            if (tableAttr is not null)
                return new FromExpression(tableAttr.Name);
        }

        foreach (var interf in t.GetInterfaces())
        {
            sqlTableAttr = interf.GetCustomAttribute<SqlTableAttribute>(true);

            if (sqlTableAttr is not null)
                return new FromExpression(sqlTableAttr.Name);
            else
            {
                var tableAttr = interf.GetCustomAttribute<TableAttribute>(true);

                if (tableAttr is not null)
                    return new FromExpression(tableAttr.Name);
            }
        }

        return new FromExpression(GetTableName(t));
    }
    public Expression MapColumn(SelectExpression column, Expression param, Type recordType)
    {
        if (column.Nullable)
        {
            return Expression.Condition(
                Expression.Call(param, recordType.GetMethod(nameof(IDataRecord.IsDBNull))!, Expression.Constant(column.Index)),
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
    public void Compile<TResult>(QueryCommand<TResult> cmd, CancellationToken cancellationToken)
    {
        if (!cmd.IsPrepared)
            cmd.PrepareCommand(cancellationToken);

        cmd.CacheEntry = CreateCompiledQuery(cmd, cancellationToken);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _conn?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
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

    // public async IAsyncEnumerable<TResult> CreateFetchEnumerator<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    // {
    //     throw new NotImplementedException();
    // }

    //record CompiledQueryPayload<TResult>(DatabaseCompiledQuery<TResult> CompiledQuery) : IPayload;
    class SqlCacheEntry : CacheEntry
    {

        public SqlCacheEntry(object? compiledQuery)
            : base(compiledQuery!)
        {
        }
        public object? Enumerator { get; set; }
    }
#if PLAN_CACHE

    class QueryPlan
    {
        public readonly QueryCommand QueryCommand;
        public QueryPlan(QueryCommand cmd)
        {
            QueryCommand = cmd;
        }
        public override int GetHashCode()
        {
            if (!QueryCommand._hashPlan.HasValue)
            {
                QueryCommand._hashPlan = QueryPlanEqualityComparer.Instance.GetHashCode(QueryCommand);
            }

            return QueryCommand._hashPlan.Value;
        }
        public override bool Equals(object? obj)
        {
            return Equals(obj as QueryPlan);
        }
        public bool Equals(QueryPlan? obj)
        {
            if (obj is null) return false;

            return QueryPlanEqualityComparer.Instance.Equals(QueryCommand, obj.QueryCommand);
        }
    }
#endif
    class EmptyEnumerator<TResult> : IAsyncEnumerator<TResult>
    {
        public TResult Current => default;

        public ValueTask DisposeAsync()
        {
            //throw new NotImplementedException();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return ValueTask.FromResult(false);
        }
    }
}