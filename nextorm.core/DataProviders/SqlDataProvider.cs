using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using OneOf;

namespace nextorm.core;

public class SqlDataProvider : IDataProvider
{
    private readonly List<Param> _params = new();
    internal bool LogSensetiveData { get; set; }

    public virtual DbConnection CreateConnection()
    {
        throw new NotImplementedException();
    }

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

        var sqlBuilder = new StringBuilder();

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

        return sqlBuilder.ToString();
    }

    private string MakeWhere(Type entityType, FromExpression from, Expression condition)
    {
        var visitor = new WhereExpressionVisitor(entityType, this, from);
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
                var visitor = new BaseExpressionVisitor(entityType, this, from);
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

        return new ResultSetEnumerator<TResult>(queryCommand, this, dbCommandPayload!.DbCommand, cancellationToken);
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
            var sqlTableAttr = srcType.GetCustomAttribute<SqlTableAttribute>();

            if (sqlTableAttr is not null)
                return new FromExpression(sqlTableAttr.Name);
            else
            {
                var tableAttr = srcType.GetCustomAttribute<TableAttribute>();

                if (tableAttr is not null)
                    return new FromExpression(tableAttr.Name);
            }

            return new FromExpression(GetTableName(srcType));
        }
        else throw new BuildSqlCommandException($"From must be specified for {nameof(TableAlias)} as source type");
    }

    public Expression MapColumn(SelectExpression column, ParameterExpression param, Type recordType)
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
