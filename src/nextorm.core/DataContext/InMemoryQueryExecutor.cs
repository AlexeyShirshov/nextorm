using System.Collections;

namespace NextORM.Core;

/// <summary>
/// Execution axis of the in-memory context: terminal operators and row readers over an
/// <see cref="InMemoryPreparedQueryCommand{TResult}"/>. The collaborator is self-contained — the
/// enumerator factory is carried by the prepared command itself — which lets
/// <see cref="InMemoryDataContext"/> stay a thin facade here, symmetrically with
/// <see cref="DataContext"/> and its <see cref="QueryExecutor"/> (F13).
/// <para>
/// The bodies are deliberately <b>not</b> shared with <see cref="QueryExecutor"/>: the in-memory
/// path has no <c>DbCommand</c>/<c>DbDataReader</c> and applies <c>Paging.Offset</c>/<c>Limit</c>
/// itself while reading, so only the facade shape is common, not the implementation.
/// </para>
/// </summary>
internal sealed class InMemoryQueryExecutor : IQueryExecutor, IRowReaderFactory
{
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        return cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, cancellationToken)!;
    }

    public IEnumerator<TResult> CreateEnumerator<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params)
    {
        return (IEnumerator<TResult>)CreateAsyncEnumerator<TResult>(preparedQueryCommand, @params, CancellationToken.None);
    }

    /// <summary>
    /// In-memory override: returns the enumerator directly (it is already <see cref="IEnumerable{T}"/>),
    /// avoiding the compiler-generated <c>yield</c> state machine used by the default interface method.
    /// </summary>
    public IEnumerable<TResult> GetEnumerable<TResult>(IPreparedQueryCommand<TResult> preparedCommand, params object[]? @params)
    {
        var enumerator = CreateEnumerator(preparedCommand, @params);
        if (enumerator is IEnumerable<TResult> enumerable)
            return enumerable;

        return new EnumeratorEnumerable<TResult>(enumerator);
    }

    private sealed class EnumeratorEnumerable<TResult>(IEnumerator<TResult> enumerator) : IEnumerable<TResult>
    {
        public IEnumerator<TResult> GetEnumerator() => enumerator;
        IEnumerator IEnumerable.GetEnumerator() => enumerator;
    }

    public async Task<List<TResult>> ToListAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;
        var l = new List<TResult>(cacheEntry.LastRowCount);

        var offset = cacheEntry.QueryCommand.Paging.Offset;
        var limit = cacheEntry.QueryCommand.Paging.Limit;
        var (rowCnt, absRowCnt) = (0, 0);

        while (await ee.MoveNextAsync())
        {
            if (offset > 0 && absRowCnt++ < offset)
                continue;

            l.Add(ee.Current);

            if (limit > 0 && ++rowCnt >= limit)
                break;
        }

        cacheEntry.LastRowCount = l.Count;

        return l;
    }

    public List<TResult> ToList<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;
        var l = new List<TResult>(cacheEntry.LastRowCount);

        var offset = cacheEntry.QueryCommand.Paging.Offset;
        var limit = cacheEntry.QueryCommand.Paging.Limit;
        var (rowCnt, absRowCnt) = (0, 0);

        while (ee.MoveNext())
        {
            if (offset > 0 && absRowCnt++ < offset)
                continue;

            l.Add(ee.Current);

            if (limit > 0 && ++rowCnt >= limit)
                break;
        }

        cacheEntry.LastRowCount = l.Count;

        return l;
    }

    public async Task<TResult?> ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, bool throwIfNull, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;

        if (await ee.MoveNextAsync())
            return ee.Current;

        if (throwIfNull) throw new InvalidOperationException();

        return default;
    }

    public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params, bool throwIfNull)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;

        if (ee.MoveNext())
            return ee.Current;

        if (throwIfNull) throw new InvalidOperationException();

        return default;
    }

    public TResult First<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;

        var absRowCnt = 0;

        while (ee.MoveNext())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            return ee.Current;
        }

        throw new InvalidOperationException();
    }

    public TResult? FirstOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;

        var absRowCnt = 0;

        while (ee.MoveNext())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            return ee.Current;
        }

        return default;
    }

    public async Task<TResult> FirstAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;

        var absRowCnt = 0;

        while (await ee.MoveNextAsync())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            return ee.Current;
        }

        throw new InvalidOperationException();
    }

    public async Task<TResult?> FirstOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;

        var absRowCnt = 0;

        while (await ee.MoveNextAsync())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            return ee.Current;
        }

        return default;
    }

    public TResult Single<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;

        var absRowCnt = 0;
        TResult? r = default;
        bool hasResult = false;

        while (ee.MoveNext())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            if (hasResult)
                throw new InvalidOperationException();

            r = ee.Current;
            hasResult = true;
        }

        if (!hasResult)
            throw new InvalidOperationException();

        return r!;
    }

    public TResult? SingleOrDefault<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        using var ee = (IEnumerator<TResult>)cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, ToParams(@params), CancellationToken.None)!;

        var absRowCnt = 0;
        TResult? r = default;
        bool hasResult = false;

        while (ee.MoveNext())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            if (hasResult)
                throw new InvalidOperationException();

            r = ee.Current;
            hasResult = true;
        }

        return r;
    }

    public async Task<TResult> SingleAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;

        var absRowCnt = 0;
        TResult? r = default;
        bool hasResult = false;

        while (await ee.MoveNextAsync())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            if (hasResult)
                throw new InvalidOperationException();

            r = ee.Current;
            hasResult = true;
        }

        if (!hasResult)
            throw new InvalidOperationException();

        return r!;
    }

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = AsInMemoryCommand(preparedQueryCommand);

        await using var ee = cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, CancellationToken.None)!;

        var absRowCnt = 0;
        TResult? r = default;
        bool hasResult = false;

        while (await ee.MoveNextAsync())
        {
            if (cacheEntry.QueryCommand.Paging.Offset > 0 && absRowCnt++ < cacheEntry.QueryCommand.Paging.Offset)
                continue;

            if (hasResult)
                throw new InvalidOperationException();

            r = ee.Current;
            hasResult = true;
        }

        return r;
    }

    /// <summary>
    /// Single entry guard for the public execution overloads: the in-memory context only executes
    /// its own command representation (mixing storage backends is not supported). Foreign
    /// implementations are rejected here, once, instead of in every overload.
    /// </summary>
    private static InMemoryPreparedQueryCommand<TResult> AsInMemoryCommand<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand)
        => preparedQueryCommand as InMemoryPreparedQueryCommand<TResult>
           ?? throw new ArgumentException($"Expected {nameof(InMemoryPreparedQueryCommand<TResult>)}, got {preparedQueryCommand.GetType().Name}", nameof(preparedQueryCommand));

    /// <summary>
    /// The in-memory core still consumes an <c>object[]</c> (the enumerator stores it), so the
    /// span-based sync entry points materialize here. This is allocation-neutral vs. the previous
    /// <c>params object[]</c> public API.
    /// </summary>
    private static object[]? ToParams(ReadOnlySpan<object?> @params)
    {
        if (@params.IsEmpty) return null;

        var arr = new object[@params.Length];
        for (var i = 0; i < @params.Length; i++) arr[i] = @params[i]!;
        return arr;
    }
}
