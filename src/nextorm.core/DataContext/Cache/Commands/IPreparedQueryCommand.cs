using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// A query that has been compiled once and can be executed repeatedly.
/// </summary>
public interface IPreparedQueryCommand<TResult>
{
    /// <summary>
    /// Streams the result rows as an asynchronous sequence using <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>An asynchronous sequence over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(IDataContext dataContext, params object[] @params) => dataContext.GetAsyncEnumerable<TResult>(this, CancellationToken.None, @params);
    /// <summary>
    /// Streams the result rows as an asynchronous sequence, honouring a cancellation token.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels the asynchronous enumeration.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>An asynchronous sequence over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(IDataContext dataContext, CancellationToken cancellationToken, params object[] @params)
    {
        var asyncEnumerator = CreateAsyncEnumerator(dataContext, cancellationToken, @params);

        return new ResultSetAsyncEnumerable<TResult>(asyncEnumerator);
    }
    /// <summary>
    /// Streams the result rows as a synchronous sequence.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A synchronous sequence over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<TResult> ToEnumerable(IDataContext dataContext, params object[]? @params) => dataContext.GetEnumerable(this, @params);
    /// <summary>
    /// Creates an asynchronous enumerator over the result rows using <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>An asynchronous enumerator over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(IDataContext dataContext, params object[]? @params) => CreateAsyncEnumerator(dataContext, CancellationToken.None, @params);
    /// <summary>
    /// Creates an asynchronous enumerator over the result rows, honouring a cancellation token.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels the opening and enumeration of the reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>An asynchronous enumerator over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.CreateAsyncEnumerator(this, @params, cancellationToken);
    /// <summary>
    /// Creates a synchronous enumerator over the result rows.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A synchronous enumerator over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<TResult> CreateEnumerator(IDataContext dataContext, params object[]? @params) => dataContext.CreateEnumerator(this, @params);
    /// <summary>
    /// Creates a synchronous enumerator over the result rows using <see cref="CancellationToken.None"/> to open the reader.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing a synchronous enumerator over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(IDataContext dataContext, params object[]? @params) => CreateEnumeratorAsync(dataContext, CancellationToken.None, @params);
    /// <summary>
    /// Creates a synchronous enumerator over the result rows, awaiting the reader open with a cancellation token.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels the opening of the reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing a synchronous enumerator over the result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.CreateEnumeratorAsync(this, @params, cancellationToken);
    /// <summary>
    /// Materializes the result rows into a list using <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the materialized result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(IDataContext dataContext, params object[]? @params) => ToListAsync(dataContext, CancellationToken.None, @params);
    /// <summary>
    /// Materializes the result rows into a list, honouring a cancellation token.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels reading of the result rows.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the materialized result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.ToListAsync(this, @params, cancellationToken);
    /// <summary>
    /// Materializes the result rows into a list.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The materialized result rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<TResult> ToList(IDataContext dataContext, params ReadOnlySpan<object?> @params) => dataContext.ToList(this, @params);
    /// <summary>
    /// Executes the query and returns its scalar value using <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="throwIfNull">Whether to throw when the scalar is <see langword="null"/> instead of returning it.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the scalar value, or <see langword="null"/> when none was returned.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> ExecuteScalarAsync(IDataContext dataContext, bool throwIfNull, params object[]? @params) => ExecuteScalarAsync(dataContext, throwIfNull, CancellationToken.None, @params);
    /// <summary>
    /// Executes the query and returns its scalar value, honouring a cancellation token.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="throwIfNull">Whether to throw when the scalar is <see langword="null"/> instead of returning it.</param>
    /// <param name="cancellationToken">Cancels execution of the query.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the scalar value, or <see langword="null"/> when none was returned.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> ExecuteScalarAsync(IDataContext dataContext, bool throwIfNull, CancellationToken cancellationToken, params object[]? @params) => dataContext.ExecuteScalar(this, @params, throwIfNull, cancellationToken);
    /// <summary>
    /// Executes the query and returns its scalar value.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="throwIfNull">Whether to throw when the scalar is <see langword="null"/> instead of returning it.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The scalar value, or <see langword="null"/> when none was returned.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? ExecuteScalar(IDataContext dataContext, bool throwIfNull, params ReadOnlySpan<object?> @params) => dataContext.ExecuteScalar(this, @params, throwIfNull);
    /// <summary>
    /// Returns the first result row, throwing when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The first result row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult First(IDataContext dataContext, params ReadOnlySpan<object?> @params) => dataContext.First(this, @params);
    /// <summary>
    /// Returns the first result row asynchronously using <see cref="CancellationToken.None"/>, throwing when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the first result row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> FirstAsync(IDataContext dataContext, params object[]? @params) => FirstAsync(dataContext, CancellationToken.None, @params);
    /// <summary>
    /// Returns the first result row asynchronously, honouring a cancellation token and throwing when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the first result row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> FirstAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.FirstAsync(this, @params, cancellationToken);
    /// <summary>
    /// Returns the first result row, or the default value when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The first result row, or the default value of <typeparamref name="TResult"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? FirstOrDefault(IDataContext dataContext, params ReadOnlySpan<object?> @params) => dataContext.FirstOrDefault(this, @params);
    /// <summary>
    /// Returns the first result row asynchronously using <see cref="CancellationToken.None"/>, or the default value when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the first result row, or the default value of <typeparamref name="TResult"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, params object[]? @params) => FirstOrDefaultAsync(dataContext, CancellationToken.None, @params);
    /// <summary>
    /// Returns the first result row asynchronously, or the default value when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the first result row, or the default value of <typeparamref name="TResult"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> FirstOrDefaultAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.FirstOrDefaultAsync(this, @params, cancellationToken);
    /// <summary>
    /// Returns the single result row, throwing when the query returns none or more than one.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The single result row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Single(IDataContext dataContext, params ReadOnlySpan<object?> @params) => dataContext.Single(this, @params);
    /// <summary>
    /// Returns the single result row asynchronously using <see cref="CancellationToken.None"/>, throwing when the query returns none or more than one.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the single result row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> SingleAsync(IDataContext dataContext, params object[]? @params) => SingleAsync(dataContext, CancellationToken.None, @params);
    /// <summary>
    /// Returns the single result row asynchronously, honouring a cancellation token and throwing when the query returns none or more than one.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the single result row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> SingleAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.SingleAsync(this, @params, cancellationToken);
    /// <summary>
    /// Returns the single result row, or the default value when the query returns none, throwing when it returns more than one.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>The single result row, or the default value of <typeparamref name="TResult"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult? SingleOrDefault(IDataContext dataContext, params ReadOnlySpan<object?> @params) => dataContext.SingleOrDefault(this, @params);
    /// <summary>
    /// Returns the single result row asynchronously using <see cref="CancellationToken.None"/>, or the default value when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the single result row, or the default value of <typeparamref name="TResult"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, params object[]? @params) => SingleOrDefaultAsync(dataContext, CancellationToken.None, @params);
    /// <summary>
    /// Returns the single result row asynchronously, or the default value when the query returns none.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels reading of the result row.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing the single result row, or the default value of <typeparamref name="TResult"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SingleOrDefaultAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params) => dataContext.SingleOrDefaultAsync(this, @params, cancellationToken);
}
