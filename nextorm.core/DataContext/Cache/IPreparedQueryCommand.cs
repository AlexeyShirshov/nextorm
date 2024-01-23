using System.Runtime.CompilerServices;

namespace nextorm.core;

public interface IPreparedQueryCommand<TResult>
{
    bool IsScalar { get; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(IDataContext dataContext, params object[] @params) => dataContext.GetAsyncEnumerable<TResult>(this, CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(IDataContext dataContext, CancellationToken cancellationToken, params object[] @params)
    {
        var asyncEnumerator = CreateAsyncEnumerator(dataContext, @params, cancellationToken);

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<TResult> ToEnumerable(IDataContext dataContext, params object[] @params) => dataContext.GetEnumerable(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.CreateAsyncEnumerator(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<TResult> CreateEnumerator(IDataContext dataContext, object[]? @params) => dataContext.CreateEnumerator(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(IDataContext dataContext, params object[]? @params) => ToListAsync(dataContext, @params, CancellationToken.None);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.ToListAsync(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<TResult> ToList(IDataContext dataContext) => ToList(dataContext, null);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<TResult> ToList(IDataContext dataContext, object[]? @params) => dataContext.ToList(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> ExecuteScalar(IDataContext dataContext, object[]? @params, bool throwIfNull, CancellationToken cancellationToken) => dataContext.ExecuteScalar(this, @params, throwIfNull, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? ExecuteScalar(IDataContext dataContext, object[]? @params, bool throwIfNull) => dataContext.ExecuteScalar(this, @params, throwIfNull);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult First(IDataContext dataContext, object[]? @params) => dataContext.First(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> FirstAsync(IDataContext dataContext, params object[]? @params) => FirstAsync(dataContext, @params, CancellationToken.None);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> FirstAsync(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.FirstAsync(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? FirstOrDefault(IDataContext dataContext, object[]? @params) => dataContext.FirstOrDefault(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, params object[]? @params) => FirstOrDefaultAsync(dataContext, @params, CancellationToken.None);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.FirstOrDefaultAsync(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Single(IDataContext dataContext, object[]? @params) => dataContext.Single(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> SingleAsync(IDataContext dataContext, params object[]? @params) => SingleAsync(dataContext, @params, CancellationToken.None);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> SingleAsync(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.SingleAsync(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? SingleOrDefault(IDataContext dataContext, object[]? @params) => dataContext.SingleOrDefault(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, params object[]? @params) => SingleOrDefaultAsync(dataContext, @params, CancellationToken.None);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, object[]? @params, CancellationToken cancellationToken) => dataContext.SingleOrDefaultAsync(this, @params, cancellationToken);
}