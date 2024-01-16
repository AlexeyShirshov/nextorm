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
    public Dictionary<string, object> Properties { get; }
    public Lazy<QueryCommand<bool>> AnyCommand { get; set; }
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
    public Task<bool> AnyAsync(IPreparedQueryCommand<bool> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken) => ExecuteScalar<bool>(preparedQueryCommand, @params, true, cancellationToken);
    public Task<bool> AnyAsync(IPreparedQueryCommand<bool> preparedQueryCommand, CancellationToken cancellationToken) => ExecuteScalar<bool>(preparedQueryCommand, null, true, cancellationToken);
    public Task<bool> AnyAsync(IPreparedQueryCommand<bool> preparedQueryCommand, params object[]? @params) => ExecuteScalar<bool>(preparedQueryCommand, @params, true, CancellationToken.None);
    public bool Any(IPreparedQueryCommand<bool> preparedQueryCommand, params object[]? @params) => ExecuteScalar<bool>(preparedQueryCommand, @params, true);
    public Task<IEnumerable<TResult>> AsEnumerableAsync<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[] @params) => AsEnumerableAsync<TResult>(preparedCommand, CancellationToken.None, @params);
    public async Task<IEnumerable<TResult>> AsEnumerableAsync<TResult>(IPreparedQueryCommand<TResult> preparedCommand, CancellationToken cancellationToken, params object[] @params)
    {
        var enumerator = CreateAsyncEnumerator<TResult>(preparedCommand, @params, cancellationToken);

        if (enumerator is IAsyncInit<TResult> rr)
        {
            await rr.InitReaderAsync(@params, cancellationToken);
            return new InternalEnumerable<TResult>(rr);
        }

        if (enumerator is IEnumerable<TResult> ee)
            return ee;

        throw new NotImplementedException();
    }
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[] @params) => AsAsyncEnumerable<TResult>(preparedCommand, CancellationToken.None, @params);
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, CancellationToken cancellationToken, params object[] @params)
    {
        var asyncEnumerator = CreateAsyncEnumerator<TResult>(preparedCommand, @params, cancellationToken);

        if (asyncEnumerator is IAsyncEnumerable<TResult> asyncEnumerable)
            return asyncEnumerable;

        return Iterate();

        async IAsyncEnumerable<TResult> Iterate()
        {
            await using (asyncEnumerator)
            {
                while (await asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                    yield return asyncEnumerator.Current;
            }
        }
    }
    public IEnumerable<TResult> AsEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[] @params) => (IEnumerable<TResult>)CreateEnumerator<TResult>(preparedCommand, @params);
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
    IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(string sql, object? @params, QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken);
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="queryCommand"></param>
    /// <param name="nonStreamUsing">true, if optimized for buffered or scalar value results; false for non-buffered (stream) using, when result is IEnumerable or IAsyncEnumerable</param>
    /// <param name="storeInCache">true to store query plan in cache, overwise it is stored only in query command</param>
    /// <param name="cancellationToken"></param>
    // void Compile<TResult>(QueryCommand<TResult> queryCommand, bool nonStreamUsing, bool storeInCache, CancellationToken cancellationToken);
    IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken, string? manualSql = null, Func<List<Param>>? makeParams = null);
    IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    // Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(IPreparedQueryCommand preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    public Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params) => ToListAsync(preparedQueryCommand, @params, CancellationToken.None);
    List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand) => ToList(preparedQueryCommand, null);
    Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken);
    TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull);
    TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    public Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params) => FirstAsync(preparedQueryCommand, @params, CancellationToken.None);
    TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    public Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params) => FirstOrDefaultAsync(preparedQueryCommand, @params, CancellationToken.None);
    TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    public Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params) => SingleAsync(preparedQueryCommand, @params, CancellationToken.None);
    TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);
    Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    public Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params) => SingleOrDefaultAsync(preparedQueryCommand, @params, CancellationToken.None);
    Entity<TResult> From<TResult>(QueryCommand<TResult> query) => new(this, query) { Logger = CommandLogger };
    Entity<TResult> From<TResult>(Entity<TResult> builder) => new(this, builder) { Logger = CommandLogger };
}