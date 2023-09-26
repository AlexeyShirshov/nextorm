using System.Linq.Expressions;

namespace nextorm.core;

public class InMemoryDataProvider : DataProvider
{
    public override IQueryCommand<T> CreateCommand<T>(LambdaExpression exp, Expression? condition)
    {
        return new QueryCommand<T>(this, exp, condition);
    }
}