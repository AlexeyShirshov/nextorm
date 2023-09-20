using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using OneOf;

namespace nextorm.core;

public class SqlClient
{
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
            sqlBuilder.Append(MakeColumn(item.Expression, entityType, from)).Append(", ");
        }

        sqlBuilder.Length -= 2;
        sqlBuilder.Append(" from ").Append(MakeFrom(from));

        return sqlBuilder.ToString();
    }

    public string MakeColumn(OneOf<ScalarSqlCommand, Expression> expression, Type entityType, FromExpression from)
    {
        return expression.Match(
            cmd => throw new NotImplementedException(),
            exp =>
            {
                var visitor = new StringExpressionVisitor(entityType, this, from);
                visitor.Visit(exp);
                return visitor.ToString();
            }
        );
    }

    public virtual string MakeFrom(FromExpression from)
    {
        ArgumentNullException.ThrowIfNull(from);

        return from.Table.Match(
            tableName => tableName + (string.IsNullOrEmpty(from.TableAlias) ? string.Empty : MakeTableAlias(from.TableAlias)),
            cmd => "(" + MakeSql(cmd.SelectList!, cmd.From!, cmd.EntityType!) + ")" + (string.IsNullOrEmpty(from.TableAlias) ? string.Empty : MakeTableAlias(from.TableAlias))
        );


    }

    private string MakeTableAlias(string tableAlias)
    {
        throw new NotImplementedException();
    }

    internal bool GetColumnName(MemberInfo member)
    {
        throw new NotImplementedException();
    }
}
