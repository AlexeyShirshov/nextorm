using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public interface IDataProvider : IAsyncDisposable, IDisposable
{
    ILogger? Logger { get; set; }
    bool NeedMapping { get; }

    IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken);
    Task<IEnumerator<TResult>> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken);
    QueryCommand<TResult> CreateCommand<TResult>(LambdaExpression exp, Expression? condition);
    void ResetPreparation(QueryCommand queryCommand);
    FromExpression? GetFrom(Type srcType, QueryCommand queryCommand);
    Expression MapColumn(SelectExpression column, Expression param, Type recordType);
    void Compile<TResult>(QueryCommand<TResult> query, bool forToListCalls, CancellationToken cancellationToken);
    Task<IEnumerable<TResult>> ToListAsync<TResult>(QueryCommand<TResult> queryCommand, CancellationToken cancellationToken);
}