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
        var asyncEnumerator = CreateAsyncEnumerator(dataContext, cancellationToken, @params);

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
    public IEnumerable<TResult> ToEnumerable(IDataContext dataContext, params object[]? @params) => dataContext.GetEnumerable(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(IDataContext dataContext, params object[]? @params) => CreateAsyncEnumerator(dataContext, CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.CreateAsyncEnumerator(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<TResult> CreateEnumerator(IDataContext dataContext, params object[]? @params) => dataContext.CreateEnumerator(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(IDataContext dataContext, params object[]? @params) => CreateEnumeratorAsync(dataContext, CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.CreateEnumeratorAsync(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(IDataContext dataContext, params object[]? @params) => ToListAsync(dataContext, CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.ToListAsync(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<TResult> ToList(IDataContext dataContext, params object[]? @params) => dataContext.ToList(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> ExecuteScalarAsync(IDataContext dataContext, bool throwIfNull, params object[]? @params) => ExecuteScalarAsync(dataContext, throwIfNull, CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> ExecuteScalarAsync(IDataContext dataContext, bool throwIfNull, CancellationToken cancellationToken, params object[]? @params) => dataContext.ExecuteScalar(this, @params, throwIfNull, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? ExecuteScalar(IDataContext dataContext, bool throwIfNull, params object[]? @params) => dataContext.ExecuteScalar(this, @params, throwIfNull);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult First(IDataContext dataContext, params object[]? @params) => dataContext.First(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> FirstAsync(IDataContext dataContext, params object[]? @params) => FirstAsync(dataContext, CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> FirstAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.FirstAsync(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? FirstOrDefault(IDataContext dataContext, params object[]? @params) => dataContext.FirstOrDefault(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, params object[]? @params) => FirstOrDefaultAsync(dataContext, CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.FirstOrDefaultAsync(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Single(IDataContext dataContext, params object[]? @params) => dataContext.Single(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> SingleAsync(IDataContext dataContext, params object[]? @params) => SingleAsync(dataContext, CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> SingleAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.SingleAsync(this, @params, cancellationToken);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? SingleOrDefault(IDataContext dataContext, params object[]? @params) => dataContext.SingleOrDefault(this, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, params object[]? @params) => SingleOrDefaultAsync(dataContext, CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.SingleOrDefaultAsync(this, @params, cancellationToken);
}