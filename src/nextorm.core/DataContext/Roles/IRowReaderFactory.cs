namespace NextORM.Core;

/// <summary>
/// Creates row readers over a prepared query command and exposes them as enumerables.
/// The enumerable members are default implementations built on the two reader factories;
/// a provider may override <see cref="GetEnumerable{TResult}"/> when its readers already
/// implement <see cref="IEnumerable{T}"/> (the in-memory context does, to skip the
/// compiler-generated iterator state machine).
/// </summary>
public interface IRowReaderFactory
{
    /// <summary>
    /// Creates an asynchronous enumerator that reads the rows of a compiled query.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to read.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels opening and enumeration of the reader.</param>
    /// <returns>An asynchronous enumerator over the result rows.</returns>
    IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    /// <summary>
    /// Creates a synchronous enumerator that reads the rows of a compiled query.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to read.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A synchronous enumerator over the result rows.</returns>
    IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);

    /// <summary>
    /// Wraps the rows of a compiled query in an asynchronous sequence, honouring a cancellation token.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedCommand">The compiled query to read.</param>
    /// <param name="cancellationToken">Cancels opening and enumeration of the reader.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>An asynchronous sequence over the result rows.</returns>
    IAsyncEnumerable<TResult> GetAsyncEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, CancellationToken cancellationToken, params object[] @params)
    {
        var asyncEnumerator = CreateAsyncEnumerator<TResult>(preparedCommand, @params, cancellationToken);

        return new ResultSetAsyncEnumerable<TResult>(asyncEnumerator);
    }

    /// <summary>
    /// Wraps the rows of a compiled query in an asynchronous sequence using <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedCommand">The compiled query to read.</param>
    /// <param name="params">The positional parameter values bound to the query.</param>
    /// <returns>An asynchronous sequence over the result rows.</returns>
    IAsyncEnumerable<TResult> GetAsyncEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[] @params)
        => GetAsyncEnumerable<TResult>(preparedCommand, CancellationToken.None, @params);

    /// <summary>
    /// Wraps the rows of a compiled query in a synchronous sequence. Overridden by providers whose
    /// readers already implement <see cref="IEnumerable{T}"/>, to skip the iterator state machine.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedCommand">The compiled query to read.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <returns>A synchronous sequence over the result rows.</returns>
    IEnumerable<TResult> GetEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[]? @params)
    {
        using var enumerator = CreateEnumerator(preparedCommand, @params);
        while (enumerator.MoveNext())
            yield return enumerator.Current;
    }

    /// <summary>
    /// Creates a synchronous enumerator, awaiting the asynchronous reader initialization first.
    /// </summary>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="preparedQueryCommand">The compiled query to read.</param>
    /// <param name="params">The positional parameter values bound to the query, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the initialization of the reader.</param>
    /// <returns>A task producing a synchronous enumerator over the result rows.</returns>
    async Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var enumerator = CreateAsyncEnumerator(preparedQueryCommand, @params, cancellationToken);

        await ((IAsyncInit<TResult>)enumerator).InitReaderAsync(@params, cancellationToken).ConfigureAwait(false);

        return (IEnumerator<TResult>)enumerator;
    }
}
