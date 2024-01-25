using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public interface IDataContext : IAsyncDisposable, IDisposable
{
    ILogger? Logger { get; }
    ILogger? CommandLogger { get; }
    // ILogger? ResultSetEnumeratorLogger { get; }
    bool NeedMapping { get; }
    public Dictionary<string, object> Properties { get; }
    public Lazy<QueryCommand<bool>>? AnyCommand { get; set; }
    public Entity<T> Create<T>(Action<EntityBuilder<T>>? configEntity = null)
    {
        if (!DataContextCache.Metadata.ContainsKey(typeof(T)))
        {
            var eb = new EntityBuilder<T>();
            configEntity?.Invoke(eb);
            DataContextCache.Metadata[typeof(T)] = eb.Build();
        }
        return new(this) { Logger = CommandLogger };
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<T> CreateCommand<T>(LambdaExpression exp, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
    {
        return new QueryCommand<T>(this, exp, condition, joins, paging, sorting, group, having, logger);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<T> CreateCommand<T>(Type srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
    {
        return new QueryCommand<T>(this, srcType, condition, joins, paging, sorting, group, having, logger);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> AnyAsync(IPreparedQueryCommand<bool> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken) => ExecuteScalar<bool>(preparedQueryCommand, @params, true, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> AnyAsync(IPreparedQueryCommand<bool> preparedQueryCommand, CancellationToken cancellationToken) => ExecuteScalar<bool>(preparedQueryCommand, null, true, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> AnyAsync(IPreparedQueryCommand<bool> preparedQueryCommand, params object[]? @params) => ExecuteScalar<bool>(preparedQueryCommand, @params, true, CancellationToken.None);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Any(IPreparedQueryCommand<bool> preparedQueryCommand, params object[]? @params) => ExecuteScalar<bool>(preparedQueryCommand, @params, true);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> GetAsyncEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[] @params) => GetAsyncEnumerable<TResult>(preparedCommand, CancellationToken.None, @params);
    public IAsyncEnumerable<TResult> GetAsyncEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, CancellationToken cancellationToken, params object[] @params)
    {
        var asyncEnumerator = CreateAsyncEnumerator<TResult>(preparedCommand, @params, cancellationToken);

        return Iterate();

        async IAsyncEnumerable<TResult> Iterate()
        {
            await using (asyncEnumerator)
            {
                while (await asyncEnumerator.MoveNextAsync())
                    yield return asyncEnumerator.Current;
            }
        }
    }
    public IEnumerable<TResult> GetEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[]? @params)
    {
        using var enumerator = CreateEnumerator(preparedCommand, @params);
        while (enumerator.MoveNext())
            yield return enumerator.Current;
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
    //IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(string sql, object? @params, QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken);
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="queryCommand"></param>
    /// <param name="nonStreamUsing">true, if optimized for buffered or scalar value results; false for non-buffered (stream) using, when result is IEnumerable or IAsyncEnumerable</param>
    /// <param name="storeInCache">true to store query plan in cache, overwise it is stored only in query command</param>
    /// <param name="cancellationToken"></param>
    // void Compile<TResult>(QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken);
    IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken);
    IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    public async Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var enumerator = CreateAsyncEnumerator(preparedQueryCommand, @params, cancellationToken);

        await ((IAsyncInit<TResult>)enumerator).InitReaderAsync(@params, cancellationToken);

        return (IEnumerator<TResult>)enumerator;
    }
    Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params) => ToListAsync(preparedQueryCommand, @params, CancellationToken.None);
    List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand) => ToList(preparedQueryCommand, null);
    Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken);
    TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull);
    TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params) => FirstAsync(preparedQueryCommand, @params, CancellationToken.None);
    TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params) => FirstOrDefaultAsync(preparedQueryCommand, @params, CancellationToken.None);
    TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params) => SingleAsync(preparedQueryCommand, @params, CancellationToken.None);
    TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params) => SingleOrDefaultAsync(preparedQueryCommand, @params, CancellationToken.None);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<TResult> From<TResult>(QueryCommand<TResult> query) => new(this, query) { Logger = CommandLogger };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity<TResult> From<TResult>(Entity<TResult> builder) => new(this, builder) { Logger = CommandLogger };
    void PurgeQueryCache();
    // bool CacheExpressions { get; set; }
}