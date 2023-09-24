using System.Linq.Expressions;

namespace nextorm.core;

public class ScalarSqlCommand : SqlCommand<ValueType>
{
    public ScalarSqlCommand(SqlClient sqlClient, LambdaExpression exp, Expression? condition) : base(sqlClient, exp, condition)
    {
    }
}
