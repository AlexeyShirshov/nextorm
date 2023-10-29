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
    private readonly List<Param> _params = new();
    private DbConnection? _conn;
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
    public DbCommand CreateCommand(QueryCommand cmd)
    {
        var dbCommand = CreateCommand(MakeSql(cmd));

        dbCommand.Parameters.AddRange(_params.Select(it => CreateParam(it.Name, it.Value)).ToArray());

        return dbCommand;
    }
    public virtual DbParameter CreateParam(string name, object? value)
    {
        throw new NotImplementedException();
    }
    public virtual string MakeSql(QueryCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd.SelectList);
        ArgumentNullException.ThrowIfNull(cmd.From);
        ArgumentNullException.ThrowIfNull(cmd.EntityType);

        var selectList = cmd.SelectList;
        var from = cmd.From;
        var entityType = cmd.EntityType;

        var sqlBuilder = _sbPool.Get();

        sqlBuilder.Append("select ");
        foreach (var item in selectList)
        {
            var col = MakeColumn(item, entityType, from);

            sqlBuilder.Append(col.Column);

            if (col.NeedAlias)
            {
                sqlBuilder.Append(MakeColumnAlias(item.PropertyName!));
            }

            sqlBuilder.Append(", ");
        }

        sqlBuilder.Length -= 2;
        sqlBuilder.Append(" from ").Append(MakeFrom(from));

        if (cmd.Condition is not null)
        {
            sqlBuilder.Append(" where ").Append(MakeWhere(entityType, from, cmd.Condition));
        }

        var r = sqlBuilder.ToString();

        _sbPool.Return(sqlBuilder);

        return r;

        //return "select id from simple_entity";
    }

    private string MakeWhere(Type entityType, FromExpression from, Expression condition)
    {
        using var visitor = new WhereExpressionVisitor(entityType, this, from);
        visitor.Visit(condition);
        _params.AddRange(visitor.Params);

        return visitor.ToString();
    }

    public (bool NeedAlias, string Column) MakeColumn(SelectExpression selectExp, Type entityType, FromExpression from)
    {
        var expression = selectExp.Expression;

        return expression.Match(
            cmd => throw new NotImplementedException(),
            exp =>
            {
                using var visitor = new BaseExpressionVisitor(entityType, this, from);
                visitor.Visit(exp);
                _params.AddRange(visitor.Params);

                return (visitor.NeedAlias, visitor.ToString());
            }
        );
    }

    public virtual string MakeFrom(FromExpression from)
    {
        ArgumentNullException.ThrowIfNull(from);

        return from.Table.Match(
            tableName => tableName + (string.IsNullOrEmpty(from.TableAlias) ? string.Empty : MakeTableAlias(from.TableAlias)),
            cmd =>
            {
                if (!cmd.IsPrepared) throw new BuildSqlCommandException("Inner query is not prepared");
                return "(" + MakeSql(cmd) + ")" + (string.IsNullOrEmpty(from.TableAlias) ? string.Empty : MakeTableAlias(from.TableAlias));
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

        var dbCommandPayload = queryCommand.GetOrAddPayload(() => new DbCommandPayload(CreateCommand(queryCommand)));
        var cmd=dbCommandPayload!.DbCommand;
        // var cmd = CreateCommand(queryCommand);
        
        //return new EmptyEnumerator<TResult>(queryCommand, this, cmd, cancellationToken);
        return new ResultSetEnumerator<TResult>(queryCommand, this, cmd, cancellationToken);
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
        queryCommand.RemovePayload<DbCommandPayload>();
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

    record DbCommandPayload(DbCommand DbCommand) : IPayload;
}

internal class EmptyEnumerator<TResult> : IAsyncEnumerator<TResult>
{
    private readonly QueryCommand<TResult> _cmd;
    private readonly SqlDataProvider _dataProvider;
    private readonly DbCommand _sqlCommand;
    private readonly CancellationToken _cancellationToken;
    private DbDataReader? _reader;
    private DbConnection? _conn;

    public EmptyEnumerator(QueryCommand<TResult> cmd, SqlDataProvider dataProvider, DbCommand sqlCommand, CancellationToken cancellationToken)
    {
        _cmd = cmd;
        _dataProvider = dataProvider;
        _sqlCommand = sqlCommand;
        _cancellationToken = cancellationToken;
    }
    public TResult Current => default;

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_reader is not null)
        {
            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Disposing data reader");
            await _reader.DisposeAsync();
        }

        // if (_conn is not null)
        // {
        //     if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Disposing connection");
        //     await _conn.DisposeAsync();
        // }

        //await _conn?.CloseAsync();
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        if (_conn is null)
        {
            _conn = _dataProvider.GetConnection();
            _sqlCommand.Connection = _conn;

            if (_conn.State == ConnectionState.Closed)
            {
                if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false) _dataProvider.Logger.LogDebug("Opening connection");

                await _conn.OpenAsync(_cancellationToken);
            }

            if (_dataProvider.Logger?.IsEnabled(LogLevel.Debug) ?? false)
            {
                _dataProvider.Logger.LogDebug(_sqlCommand.CommandText);

                if (_dataProvider.LogSensetiveData)
                {
                    foreach (DbParameter p in _sqlCommand.Parameters)
                    {
                        _dataProvider.Logger.LogDebug("param {name} is {value}", p.ParameterName, p.Value);
                    }
                }
                else if (_sqlCommand.Parameters?.Count > 0)
                {
                    _dataProvider.Logger.LogDebug("Use {method} to see param values", nameof(_dataProvider.LogSensetiveData));
                }
            }

            _reader = await _sqlCommand.ExecuteReaderAsync(_cancellationToken);
        }

        if (_dataProvider.Logger?.IsEnabled(LogLevel.Trace) ?? false) _dataProvider.Logger.LogTrace("Move next");

        return await _reader!.ReadAsync(_cancellationToken);
    }
}