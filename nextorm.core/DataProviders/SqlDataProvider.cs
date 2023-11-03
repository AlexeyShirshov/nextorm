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
    private readonly IDictionary<QueryCommand, SqlCacheEntry> _cmdIdx = new Dictionary<QueryCommand, SqlCacheEntry>();
    private DbConnection? _conn;
    private bool _clearCache;
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
    public DatabaseCompiledQuery<TResult> CreateCompiledQuery<TResult>(QueryCommand<TResult> cmd)
    {
        var (sql, @params) = MakeSelect(cmd);
        var dbCommand = CreateCommand(sql);

        dbCommand.Parameters.AddRange(@params.Select(it => CreateParam(it.Name, it.Value)).ToArray());

        return new DatabaseCompiledQuery<TResult>(dbCommand, cmd.GetMap(typeof(TResult), typeof(DbDataReader)));
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
    public virtual (string, List<Param>) MakeSelect(QueryCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd.SelectList);
        ArgumentNullException.ThrowIfNull(cmd.From);
        ArgumentNullException.ThrowIfNull(cmd.EntityType);

        var selectList = cmd.SelectList;
        var from = cmd.From;
        var entityType = cmd.EntityType;

        var sqlBuilder = _sbPool.Get();

        var @params = new List<Param>();

        sqlBuilder.Append("select ");
        foreach (var item in selectList)
        {
            var (needAliasForColumn, column) = MakeColumn(item, entityType, cmd, false, @params);

            sqlBuilder.Append(column);

            if (needAliasForColumn)
            {
                sqlBuilder.Append(MakeColumnAlias(item.PropertyName!));
            }

            sqlBuilder.Append(", ");
        }

        sqlBuilder.Length -= 2;
        sqlBuilder.Append(" from ").Append(MakeFrom(from, @params));

        // if (cmd.Joins.Any())
        // {
        foreach (var join in cmd.Joins)
        {
            sqlBuilder.Append(MakeJoin(join, cmd, @params));
        }
        //}

        if (cmd.Condition is not null)
        {
            sqlBuilder.Append(" where ").Append(MakeWhere(entityType, cmd, cmd.Condition, 0, null, @params));
        }

        var r = sqlBuilder.ToString();

        _sbPool.Return(sqlBuilder);

        return (r, @params);
    }

    private string MakeJoin(JoinExpression join, QueryCommand cmd, List<Param> @params)
    {
        var sqlBuilder = _sbPool.Get();

        switch (join.JoinType)
        {
            case JoinType.Inner:
                sqlBuilder.Append(" join ");
                break;
            default:
                throw new NotImplementedException(join.JoinType.ToString());
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
            var (sql, p) = MakeSelect(join.Query);
            @params.AddRange(p);
            sqlBuilder.Append('(').Append(sql).Append(") ");
            sqlBuilder.Append(GetAliasFromProjection(cmd, visitor.JoinType, 1));
        }
        else
        {
            fromExp = GetFrom(visitor.JoinType);

            fromExp.TableAlias = GetAliasFromProjection(cmd, visitor.JoinType, dim);

            sqlBuilder.Append(MakeFrom(fromExp, @params));
        }

        sqlBuilder.Append(" on ");
        sqlBuilder.Append(MakeWhere(cmd.EntityType!, cmd, visitor.JoinCondition, dim, dim == 1
                ? new ExpressionAliasProvider(join.JoinCondition)
                : new ProjectionAliasProvider(dim, cmd.EntityType!), @params));

        var r = sqlBuilder.ToString();

        _sbPool.Return(sqlBuilder);

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
    private string MakeWhere(Type entityType, ISourceProvider tableSource, Expression condition, int dim, IAliasProvider? aliasProvider, List<Param> @params)
    {
        using var visitor = new WhereExpressionVisitor(entityType, this, tableSource, dim, aliasProvider);
        visitor.Visit(condition);
        @params.AddRange(visitor.Params);

        return visitor.ToString();
    }

    public (bool NeedAliasForColumn, string Column) MakeColumn(SelectExpression selectExp, Type entityType, ISourceProvider tableProvider, bool dontNeedAlias, List<Param> @params)
    {
        var expression = selectExp.Expression;

        return expression.Match(
            cmd => throw new NotImplementedException(),
            exp =>
            {
                using var visitor = new BaseExpressionVisitor(entityType, this, tableProvider, 0, null, dontNeedAlias);
                visitor.Visit(exp);
                @params.AddRange(visitor.Params);

                return (visitor.NeedAliasForColumn || selectExp.PropertyName != visitor.ColumnName, visitor.ToString());
            }
        );
    }

    public virtual string MakeFrom(FromExpression from, List<Param> @params)
    {
        ArgumentNullException.ThrowIfNull(from);

        return from.Table.Match(
            tableName => tableName + (string.IsNullOrEmpty(from.TableAlias) ? string.Empty : MakeTableAlias(from.TableAlias)),
            cmd =>
            {
                if (!cmd.IsPrepared) throw new BuildSqlCommandException("Inner query is not prepared");
                var (sql, p) = MakeSelect(cmd);
                @params.AddRange(p);
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
        if (ce is SqlCacheEntry cacheEntry || _cmdIdx.TryGetValue(queryCommand, out cacheEntry!) && cacheEntry is not null)
        {
            if (cacheEntry.Enumerator is not ResultSetEnumerator<TResult> enumerator)
            {
                enumerator = new ResultSetEnumerator<TResult>(this, (DatabaseCompiledQuery<TResult>)cacheEntry.CompiledQuery, cancellationToken);
                cacheEntry.Enumerator = enumerator;
            }

            return enumerator;
        }
        else
        {
            var compiledQuery = CreateCompiledQuery(queryCommand);
            var enumerator = new ResultSetEnumerator<TResult>(this, compiledQuery, cancellationToken);
            cacheEntry = new SqlCacheEntry(compiledQuery) { Enumerator = enumerator };

            if (queryCommand.Cache)
                _cmdIdx[queryCommand] = cacheEntry;

            return enumerator;
        }
    }
    public QueryCommand<T> CreateCommand<T>(LambdaExpression exp, Expression? condition)
    {
        return new QueryCommand<T>(this, exp, condition);
    }

    public void ResetPreparation(QueryCommand queryCommand)
    {
        _clearCache = true;
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

        cmd.CacheEntry = new CacheEntry(CreateCompiledQuery(cmd));
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
    //record CompiledQueryPayload<TResult>(DatabaseCompiledQuery<TResult> CompiledQuery) : IPayload;
    class SqlCacheEntry : CacheEntry
    {

        public SqlCacheEntry(object? compiledQuery)
            : base(compiledQuery!)
        {
        }
        public object? Enumerator { get; set; }
    }
}