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
    IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken);
    IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params);

    IAsyncEnumerable<TResult> GetAsyncEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, CancellationToken cancellationToken, params object[] @params)
    {
        var asyncEnumerator = CreateAsyncEnumerator<TResult>(preparedCommand, @params, cancellationToken);

        return new ResultSetAsyncEnumerable<TResult>(asyncEnumerator);
    }

    IAsyncEnumerable<TResult> GetAsyncEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[] @params)
        => GetAsyncEnumerable<TResult>(preparedCommand, CancellationToken.None, @params);

    IEnumerable<TResult> GetEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[]? @params)
    {
        using var enumerator = CreateEnumerator(preparedCommand, @params);
        while (enumerator.MoveNext())
            yield return enumerator.Current;
    }

    async Task<IEnumerator<TResult>> CreateEnumeratorAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var enumerator = CreateAsyncEnumerator(preparedQueryCommand, @params, cancellationToken);

        await ((IAsyncInit<TResult>)enumerator).InitReaderAsync(@params, cancellationToken).ConfigureAwait(false);

        return (IEnumerator<TResult>)enumerator;
    }
}
