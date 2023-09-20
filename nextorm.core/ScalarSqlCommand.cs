using System.Linq.Expressions;

namespace nextorm.core;

public class ScalarSqlCommand : SqlCommandFinal<ValueType>
{
    public ScalarSqlCommand(SqlClient sqlClient, LambdaExpression exp) : base(sqlClient, exp)
    {
    }
}
