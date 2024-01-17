namespace nextorm.core;

public interface IPreparedQueryCommand<TResult>
{
    bool IsScalar { get; }
    public Task<IEnumerable<TResult>> AsEnumerableAsync(IDataContext dataContext, params object[] @params) => dataContext.AsEnumerableAsync<TResult>(this, CancellationToken.None, @params);
    public async Task<IEnumerable<TResult>> AsEnumerableAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[] @params)
    {
        var enumerator = CreateAsyncEnumerator(dataContext, @params, cancellationToken);

        if (enumerator is IAsyncInit<TResult> rr)
        {
            await rr.InitReaderAsync(@params, cancellationToken);
            return new InternalEnumerable<TResult>(rr);
        }

        if (enumerator is IEnumerable<TResult> ee)
            return ee;

        throw new NotImplementedException();
    }
    public IAsyncEnumerable<TResult> AsAsyncEnumerable(IDataContext dataContext, params object[] @params) => dataContext.AsAsyncEnumerable<TResult>(this, CancellationToken.None, @params);
    public IAsyncEnumerable<TResult> AsAsyncEnumerable(IDataContext dataContext, CancellationToken cancellationToken, params object[] @params)
    {
        var asyncEnumerator = CreateAsyncEnumerator(dataContext, @params, cancellationToken);

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
    public IEnumerable<TResult> AsEnumerable(IDataContext dataContext, params object[] @params) => (IEnumerable<TResult>)CreateEnumerator(dataContext, @params);
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.CreateAsyncEnumerator(this, @params, cancellationToken);
    public IEnumerator<TResult> CreateEnumerator(IDataContext dataContext, object[]? @params) => dataContext.CreateEnumerator(this, @params);
    public Task<List<TResult>> ToListAsync(IDataContext dataContext, params object[]? @params) => ToListAsync(dataContext, @params, CancellationToken.None);
    public Task<List<TResult>> ToListAsync(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.ToListAsync(this, @params, cancellationToken);
    public List<TResult> ToList(IDataContext dataContext) => ToList(dataContext, null);
    public List<TResult> ToList(IDataContext dataContext, object[]? @params) => dataContext.ToList(this, @params);
    public Task<TResult?> ExecuteScalar(IDataContext dataContext, object[]? @params, bool throwIfNull, CancellationToken cancellationToken) => dataContext.ExecuteScalar(this, @params, throwIfNull, cancellationToken);
    public TResult? ExecuteScalar(IDataContext dataContext, object[]? @params, bool throwIfNull) => dataContext.ExecuteScalar(this, @params, throwIfNull);
    public TResult First(IDataContext dataContext, object[]? @params) => dataContext.First(this, @params);
    public Task<TResult> FirstAsync(IDataContext dataContext, params object[]? @params) => FirstAsync(dataContext, @params, CancellationToken.None);
    public Task<TResult> FirstAsync(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.FirstAsync(this, @params, cancellationToken);
    public TResult? FirstOrDefault(IDataContext dataContext, object[]? @params) => dataContext.FirstOrDefault(this, @params);
    public Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, params object[]? @params) => FirstOrDefaultAsync(dataContext, @params, CancellationToken.None);
    public Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.FirstOrDefaultAsync(this, @params, cancellationToken);
    public TResult Single(IDataContext dataContext, object[]? @params) => dataContext.Single(this, @params);
    public Task<TResult> SingleAsync(IDataContext dataContext, params object[]? @params) => SingleAsync(dataContext, @params, CancellationToken.None);
    public Task<TResult> SingleAsync(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.SingleAsync(this, @params, cancellationToken);
    public TResult? SingleOrDefault(IDataContext dataContext, object[]? @params) => dataContext.SingleOrDefault(this, @params);
    public Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, params object[]? @params) => SingleOrDefaultAsync(dataContext, @params, CancellationToken.None);
    public Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.SingleOrDefaultAsync(this, @params, cancellationToken);
}