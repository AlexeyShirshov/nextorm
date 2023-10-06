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
        throw new NotImplementedException(type.ToString());
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
            var col = MakeColumn(item, entityType, cmd, false);

            sqlBuilder.Append(col.Column);

            if (col.NeedAliasForColumn)
            {
                sqlBuilder.Append(MakeColumnAlias(item.PropertyName!));
            }

            sqlBuilder.Append(", ");
        }

        sqlBuilder.Length -= 2;
        sqlBuilder.Append(" from ").Append(MakeFrom(from));

        if (cmd.Joins.Any())
        {
            foreach (var join in cmd.Joins)
            {
                sqlBuilder.Append(MakeJoin(join, cmd));
            }
        }

        if (cmd.Condition is not null)
        {
            sqlBuilder.Append(" where ").Append(MakeWhere(entityType, cmd, cmd.Condition, 0, null));
        }

        return sqlBuilder.ToString();
    }

    private string MakeJoin(JoinExpression join, QueryCommand cmd)
    {
        var sqlBuilder = new StringBuilder();

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
            sqlBuilder.Append('(').Append(MakeSql(join.Query)).Append(") ");
            sqlBuilder.Append(GetAliasFromProjection(cmd, visitor.JoinType, 1));
        }
        else
        {
            fromExp = GetFrom(visitor.JoinType);

            fromExp.TableAlias = GetAliasFromProjection(cmd, visitor.JoinType, dim);

            sqlBuilder.Append(MakeFrom(fromExp));
        }

        sqlBuilder.Append(" on ");
        sqlBuilder.Append(MakeWhere(cmd.EntityType!, cmd, visitor.JoinCondition, dim, dim == 1
                ? new ExpressionAliasProvider(join.JoinCondition)
                : new ProjectionAliasProvider(dim, cmd.EntityType!)));

        return sqlBuilder.ToString();
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
    private string MakeWhere(Type entityType, ISourceProvider tableSource, Expression condition, int dim, IAliasProvider? aliasProvider)
    {
        var visitor = new WhereExpressionVisitor(entityType, this, tableSource, dim, aliasProvider);
        visitor.Visit(condition);
        _params.AddRange(visitor.Params);

        return visitor.ToString();
    }

    public (bool NeedAliasForColumn, string Column) MakeColumn(SelectExpression selectExp, Type entityType, ISourceProvider tableProvider, bool dontNeedAlias)
    {
        var expression = selectExp.Expression;

        return expression.Match(
            cmd => throw new NotImplementedException(),
            exp =>
            {
                var visitor = new BaseExpressionVisitor(entityType, this, tableProvider, 0, null, dontNeedAlias);
                visitor.Visit(exp);
                _params.AddRange(visitor.Params);

                return (visitor.NeedAliasForColumn || selectExp.PropertyName != visitor.ColumnName, visitor.ToString());
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

    public FromExpression? GetFrom(Type srcType, QueryCommand queryCommand)
    {
        if (srcType != typeof(TableAlias))
        {
            if (queryCommand.Joins.Any() && srcType.FullName!.StartsWith("nextorm.core.Projection"))
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

    public virtual string MakeBool(bool v)
    {
        return v ? "1" : "0";
    }

    record DbCommandPayload(DbCommand DbCommand) : IPayload;
}
