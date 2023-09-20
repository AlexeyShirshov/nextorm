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
    public DbCommand CreateCommand<T>(SqlCommand<T> cmd)
    {
        return CreateCommand(MakeSql(cmd.SelectList, cmd.From, cmd.EntityType));
    }
    public virtual string MakeSql(List<SelectExpression> selectList, FromExpression from, Type entityType)
    {
        var sqlBuilder = new StringBuilder();

        sqlBuilder.Append("select ");
        foreach (var item in selectList)
        {
            sqlBuilder.Append(MakeColumn(item.Expression, entityType)).Append(", ");
        }

        sqlBuilder.Length-=2;
        sqlBuilder.Append(" from ").Append(MakeFrom(from));

        return sqlBuilder.ToString();
    }

    private string MakeColumn(OneOf<ScalarSqlCommand, Expression> expression, Type entityType)
    {
        return expression.Match(cmd=>throw new NotImplementedException(),
        exp=>
        {
            var visitor = new StringExpressionVisitor(entityType, this);
            visitor.Visit(exp);
            return visitor.ToString();
        });
    }

    public virtual string MakeFrom(FromExpression from)
    {
        return from.TableName;
    }

    internal bool GetColumnName(MemberInfo member)
    {
        throw new NotImplementedException();
    }
}
