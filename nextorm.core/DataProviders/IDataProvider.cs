using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public interface IDataProvider
{
    public ILogger? Logger { get; set; }
    bool NeedMapping { get; }

    IAsyncEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken);
    QueryCommand<TResult> CreateCommand<TResult>(LambdaExpression exp, Expression? condition);
    void ResetPreparation(QueryCommand queryCommand);
    FromExpression? GetFrom(Type srcType, QueryCommand queryCommand);
    public Expression MapColumn(SelectExpression column, ParameterExpression param, Type recordType);
}