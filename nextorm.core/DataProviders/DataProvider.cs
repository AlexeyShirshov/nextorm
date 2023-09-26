using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class DataProvider
{
    internal ILogger? Logger { get; set; }

    public virtual IAsyncEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand queryCommand, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public virtual IQueryCommand<T> CreateCommand<T>(LambdaExpression exp, Expression? condition)
    {
        throw new NotImplementedException();
    }
}