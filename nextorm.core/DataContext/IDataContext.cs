using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace nextorm.core;

public interface IDataContext : IAsyncDisposable, IDisposable
{
    ILogger? Logger { get; }
    ILogger? CommandLogger { get; }
    ILogger? ResultSetEnumeratorLogger { get; }
    bool NeedMapping { get; }
    IDictionary<ExpressionKey, Delegate> ExpressionsCache { get; }
    IDictionary<Type, IEntityMeta> Metadata { get; }
    IDictionary<Type, List<SelectExpression>> SelectListCache { get; }
    //IDictionary<Expression, List<SelectExpression>> SelectListExpressionCache { get; }
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
    public QueryCommand<T> CreateCommand<T>(LambdaExpression exp, Expression? condition, Paging paging, List<Sorting>? sorting)
    {
        return new QueryCommand<T>(this, exp, condition, paging, sorting);
    }
    void EnsureConnectionOpen();
    Task EnsureConnectionOpenAsync();
    void ResetPreparation(QueryCommand queryCommand);
    FromExpression? GetFrom(Type srcType, QueryCommand queryCommand);
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="sql"></param>
    /// <param name="params"></param>
    /// <param name="queryCommand"></param>
    /// <param name="nonStreamUsing">true, if optimized for buffered or scalar value results; false for non-buffered (stream) using, when result is IEnumerable or IAsyncEnumerable</param>
    /// <param name="storeInCache">true to store query plan in cache, overwise it is stored only in query command</param>
    /// <param name="cancellationToken"></param>
    void Compile<TResult>(string sql, object? @params, QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken);
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="queryCommand"></param>
    /// <param name="nonStreamUsing">true, if optimized for buffered or scalar value results; false for non-buffered (stream) using, when result is IEnumerable or IAsyncEnumerable</param>
    /// <param name="storeInCache">true to store query plan in cache, overwise it is stored only in query command</param>
    /// <param name="cancellationToken"></param>
    // void Compile<TResult>(QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken);
    IPreparedQueryCommand GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken, string? manualSql = null, Func<List<Param>>? makeParams = null);
    IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params);
    // Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    List<TResult> ToList<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params);
    Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken);
    TResult? ExecuteScalar<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params, bool throwIfNull);
    TResult First<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params);
    Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    TResult? FirstOrDefault<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params);
    Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    TResult Single<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params);
    Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    TResult? SingleOrDefault<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params);
    Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    Entity<TResult> From<TResult>(QueryCommand<TResult> query) => new(this, query) { Logger = CommandLogger };
    Entity<TResult> From<TResult>(Entity<TResult> builder) => new(this, builder) { Logger = CommandLogger };
}