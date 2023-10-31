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
    private IDictionary<QueryCommand, object> _cmdIdx = new Dictionary<QueryCommand, object>();
    private DbConnection? _conn;
    private bool _clearCache;
    private bool disposedValue;
    internal readonly ObjectPool<StringBuilder> _sbPool = new DefaultObjectPoolProvider().Create(new StringBuilderPooledObjectPolicy());
    //private DbCommand? _cmd;
    internal bool LogSensetiveData { get; set; }
    public DbConnection GetConnection()
    {
        if (_conn is null)
        {
            if (Logger?.IsEnabled(LogLevel.Debug) ?? false) Logger.LogDebug("Creating connection");
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
        throw new NotImplementedException();
    }
    public DatabaseCompiledQuery<TResult> CreateCompiledQuery<TResult>(QueryCommand<TResult> cmd)
    {
        var (sql, @params) = MakeSql(cmd);
        var dbCommand = CreateCommand(sql);

        dbCommand.Parameters.AddRange(@params.Select(it => CreateParam(it.Name, it.Value)).ToArray());

        return new DatabaseCompiledQuery<TResult>(dbCommand, cmd.GetMap(typeof(TResult), typeof(DbDataReader)));
    }
    public DatabaseCompiledQuery<TResult> GetCompiledQuery<TResult>(QueryCommand<TResult> cmd)
    {
        var compiled = cmd.Compiled as DatabaseCompiledQuery<TResult>;
        if (compiled is not null)
            return compiled;

        if (!cmd.Cache || !_cmdIdx.TryGetValue(cmd, out var query))
        {
            query = CreateCompiledQuery(cmd);

            if (cmd.Cache)
                _cmdIdx[cmd] = query;
        }

        return (DatabaseCompiledQuery<TResult>)query;
    }
    public virtual DbParameter CreateParam(string name, object? value)
    {
        throw new NotImplementedException();
    }
    public virtual (string, List<Param>) MakeSql(QueryCommand cmd)
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
            var col = MakeColumn(item, entityType, from, @params);

            sqlBuilder.Append(col.Column);

            if (col.NeedAlias)
            {
                sqlBuilder.Append(MakeColumnAlias(item.PropertyName!));
            }

            sqlBuilder.Append(", ");
        }

        sqlBuilder.Length -= 2;
        sqlBuilder.Append(" from ").Append(MakeFrom(from, @params));

        if (cmd.Condition is not null)
        {
            sqlBuilder.Append(" where ").Append(MakeWhere(entityType, from, cmd.Condition, @params));
        }

        var r = sqlBuilder.ToString();

        _sbPool.Return(sqlBuilder);

        return (r, @params);
    }

    private string MakeWhere(Type entityType, FromExpression from, Expression condition, List<Param> @params)
    {
        using var visitor = new WhereExpressionVisitor(entityType, this, from);
        visitor.Visit(condition);
        @params.AddRange(visitor.Params);

        return visitor.ToString();
    }

    public (bool NeedAlias, string Column) MakeColumn(SelectExpression selectExp, Type entityType, FromExpression from, List<Param> @params)
    {
        var expression = selectExp.Expression;

        return expression.Match(
            cmd => throw new NotImplementedException(),
            exp =>
            {
                using var visitor = new BaseExpressionVisitor(entityType, this, from);
                visitor.Visit(exp);
                @params.AddRange(visitor.Params);

                return (visitor.NeedAlias, visitor.ToString());
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
                var (sql, p) = MakeSql(cmd);
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
    internal bool GetColumnName(MemberInfo member)
    {
        throw new NotImplementedException();
    }

    public virtual string MakeCoalesce(string v1, string v2)
    {
        return $"isnull({v1},{v2})";
    }

    public virtual string MakeParam(string name)
    {
        throw new NotImplementedException();
    }
    public IAsyncEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryCommand);

        DatabaseCompiledQuery<TResult> compiledQuery;
        if (queryCommand.Cache)
            compiledQuery = GetCompiledQuery(queryCommand);
        else
        {
            var factory = () => new CompiledQueryPayload<TResult>(GetCompiledQuery(queryCommand));
            CompiledQueryPayload<TResult>? compiledQueryPayload;
            if (_clearCache)
            {
                compiledQueryPayload = queryCommand.AddOrUpdatePayload(factory);
                _clearCache = false;
            }
            else
                compiledQueryPayload = queryCommand.GetOrAddPayload(factory);

            compiledQuery = compiledQueryPayload!.CompiledQuery;
        }
        // var cmd = CreateCommand(queryCommand);

        //return new EmptyEnumerator<TResult>(queryCommand, this, cmd, cancellationToken);
        return new ResultSetEnumerator<TResult>(this, compiledQuery, cancellationToken);
    }
    public virtual string ConcatStringOperator => "+";

    public ILogger? Logger { get; set; }

    public bool NeedMapping => true;

    public QueryCommand<T> CreateCommand<T>(LambdaExpression exp, Expression? condition)
    {
        return new QueryCommand<T>(this, exp, condition);
    }

    public void ResetPreparation(QueryCommand queryCommand)
    {
        _clearCache = true;
    }

    public FromExpression? GetFrom(Type srcType)
    {
        if (srcType != typeof(TableAlias))
        {
            var sqlTableAttr = srcType.GetCustomAttribute<SqlTableAttribute>(true);

            if (sqlTableAttr is not null)
                return new FromExpression(sqlTableAttr.Name);
            else
            {
                var tableAttr = srcType.GetCustomAttribute<TableAttribute>(true);

                if (tableAttr is not null)
                    return new FromExpression(tableAttr.Name);
            }

            foreach (var interf in srcType.GetInterfaces())
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

            return new FromExpression(GetTableName(srcType));
        }
        else throw new BuildSqlCommandException($"From must be specified for {nameof(TableAlias)} as source type");
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

    public void Compile<TResult>(QueryCommand<TResult> cmd)
    {
        if (!cmd.IsPrepared)
            cmd.PrepareCommand(CancellationToken.None);

        cmd.Compiled = CreateCompiledQuery(cmd);
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
    record CompiledQueryPayload<TResult>(DatabaseCompiledQuery<TResult> CompiledQuery) : IPayload;
}