using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using OneOf;

namespace nextorm.core;

public class SqlClient
{
    internal ILogger? Logger { get; set; }

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
    public DbCommand CreateCommand(SqlCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd.SelectList);
        ArgumentNullException.ThrowIfNull(cmd.From);
        ArgumentNullException.ThrowIfNull(cmd.EntityType);

        return CreateCommand(MakeSql(cmd.SelectList, cmd.From, cmd.EntityType));
    }
    public virtual string MakeSql(List<SelectExpression> selectList, FromExpression from, Type entityType)
    {
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

        return sqlBuilder.ToString();
    }

    public (bool NeedAlias, string Column) MakeColumn(SelectExpression selectExp, Type entityType, FromExpression from)
    {
        var expression = selectExp.Expression;

        return expression.Match(
            cmd => throw new NotImplementedException(),
            exp =>
            {
                var visitor = new StringExpressionVisitor(entityType, this, from);
                visitor.Visit(exp);

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
                return "(" + MakeSql(cmd.SelectList!, cmd.From!, cmd.EntityType!) + ")" + (string.IsNullOrEmpty(from.TableAlias) ? string.Empty : MakeTableAlias(from.TableAlias));
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

    public virtual string ConcatStringOperator=>"+";
}
