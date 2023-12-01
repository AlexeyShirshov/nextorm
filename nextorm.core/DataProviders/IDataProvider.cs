using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace nextorm.core;

public interface IDataContext : IAsyncDisposable, IDisposable
{
    ILogger? Logger { get; set; }
    ILogger? CommandLogger { get; set; }
    bool NeedMapping { get; }
    IDictionary<ExpressionKey, Delegate> ExpressionsCache { get; }
    IDictionary<Type, IEntityMeta> Metadata { get; }
    QueryCommand<TResult> CreateCommand<TResult>(LambdaExpression exp, Expression? condition);
    void ResetPreparation(QueryCommand queryCommand);
    FromExpression? GetFrom(Type srcType, QueryCommand queryCommand);
    void Compile<TResult>(QueryCommand<TResult> query, bool bufferedOrScalarCalls, CancellationToken cancellationToken);
    IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken);
    IEnumerator<TResult> CreateEnumerator<TResult>(QueryCommand<TResult> queryCommand, object[] @params);
    Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken);
    Task<List<TResult>> ToListAsync<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken);
    List<TResult> ToList<TResult>(QueryCommand<TResult> queryCommand, object[]? @params);
    Task<TResult?> ExecuteScalar<TResult>(QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken);
    TResult? ExecuteScalar<TResult>(QueryCommand<TResult> queryCommand, object[]? @params);
    public Entity<TResult> From<TResult>(QueryCommand<TResult> query) => new(this, query) { Logger = CommandLogger };
    public Entity<TResult> From<TResult>(Entity<TResult> builder) => new(this, builder) { Logger = CommandLogger };
    public Entity<T> Create<T>(Action<EntityBuilder<T>>? configEntity = null)
    {
        if (!Metadata.ContainsKey(typeof(T)))
        {
            var eb = new EntityBuilder<T>();
            configEntity?.Invoke(eb);
            Metadata[typeof(T)] = eb.Build();
        }
        return new(this) { Logger = CommandLogger };
    }
}