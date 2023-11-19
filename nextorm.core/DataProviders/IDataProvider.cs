using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public interface IDataProvider : IAsyncDisposable, IDisposable
{
    ILogger? Logger { get; set; }
    bool NeedMapping { get; }
    IDictionary<ExpressionKey, Delegate> ExpressionsCache { get; }
    QueryCommand<TResult> CreateCommand<TResult>(LambdaExpression exp, Expression? condition);
    void ResetPreparation(QueryCommand queryCommand);
    FromExpression? GetFrom(Type srcType, QueryCommand queryCommand);
    void Compile<TResult>(QueryCommand<TResult> query, bool forToListCalls, CancellationToken cancellationToken);
    IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken);
    IEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[] @params);
    Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken);
    Task<List<TResult>> ToListAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken);
    List<TResult> ToList<TResult>(QueryCommand<TResult> queryCommand, object[]? @params);
}