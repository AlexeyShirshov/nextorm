namespace NextORM.Core;

/// <summary>A query prepared for repeated execution that streams result rows.</summary>
/// <typeparam name="TResult">The type of a result row.</typeparam>
public interface IPreparedStreamingQueryCommand<TResult>
{
    /// <summary>
    /// Streams the result rows as an asynchronous sequence using <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>An asynchronous sequence over the result rows.</returns>
    IAsyncEnumerable<TResult> ToAsyncEnumerable(IDataContext dataContext, params object[] @params);

    /// <summary>
    /// Streams the result rows as an asynchronous sequence, honouring a cancellation token.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels the asynchronous enumeration.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>An asynchronous sequence over the result rows.</returns>
    IAsyncEnumerable<TResult> ToAsyncEnumerable(IDataContext dataContext, CancellationToken cancellationToken, params object[] @params);

    /// <summary>
    /// Streams the result rows as a synchronous sequence.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A synchronous sequence over the result rows.</returns>
    IEnumerable<TResult> ToEnumerable(IDataContext dataContext, params object[]? @params);

    /// <summary>
    /// Creates an asynchronous enumerator over the result rows using <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>An asynchronous enumerator over the result rows.</returns>
    IAsyncEnumerator<TResult> CreateAsyncEnumerator(IDataContext dataContext, params object[]? @params);

    /// <summary>
    /// Creates an asynchronous enumerator over the result rows, honouring a cancellation token.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels the opening and enumeration of the reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>An asynchronous enumerator over the result rows.</returns>
    IAsyncEnumerator<TResult> CreateAsyncEnumerator(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params);

    /// <summary>
    /// Creates a synchronous enumerator over the result rows.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A synchronous enumerator over the result rows.</returns>
    IEnumerator<TResult> CreateEnumerator(IDataContext dataContext, params object[]? @params);

    /// <summary>
    /// Creates a synchronous enumerator over the result rows using <see cref="CancellationToken.None"/> to open the reader.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing a synchronous enumerator over the result rows.</returns>
    Task<IEnumerator<TResult>> CreateEnumeratorAsync(IDataContext dataContext, params object[]? @params);

    /// <summary>
    /// Creates a synchronous enumerator over the result rows, awaiting the reader open with a cancellation token.
    /// </summary>
    /// <param name="dataContext">The context that supplies the underlying row reader.</param>
    /// <param name="cancellationToken">Cancels the opening of the reader.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A task producing a synchronous enumerator over the result rows.</returns>
    Task<IEnumerator<TResult>> CreateEnumeratorAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params);
}

internal sealed class PreparedStreamingQueryCommand<TResult>(IPreparedQueryCommand<TResult> command)
    : IPreparedStreamingQueryCommand<TResult>
{
    internal IPreparedQueryCommand<TResult> Command { get; } = command;

    public IAsyncEnumerable<TResult> ToAsyncEnumerable(IDataContext dataContext, params object[] @params)
        => Command.ToAsyncEnumerable(dataContext, @params);

    public IAsyncEnumerable<TResult> ToAsyncEnumerable(IDataContext dataContext, CancellationToken cancellationToken, params object[] @params)
        => Command.ToAsyncEnumerable(dataContext, cancellationToken, @params);

    public IEnumerable<TResult> ToEnumerable(IDataContext dataContext, params object[]? @params)
        => Command.ToEnumerable(dataContext, @params);

    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(IDataContext dataContext, params object[]? @params)
        => Command.CreateAsyncEnumerator(dataContext, @params);

    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params)
        => Command.CreateAsyncEnumerator(dataContext, cancellationToken, @params);

    public IEnumerator<TResult> CreateEnumerator(IDataContext dataContext, params object[]? @params)
        => Command.CreateEnumerator(dataContext, @params);

    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(IDataContext dataContext, params object[]? @params)
        => Command.CreateEnumeratorAsync(dataContext, @params);

    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(IDataContext dataContext, CancellationToken cancellationToken, params object[]? @params)
        => Command.CreateEnumeratorAsync(dataContext, cancellationToken, @params);
}
