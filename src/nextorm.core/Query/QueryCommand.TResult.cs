using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace nextorm.core;

public sealed partial class QueryCommand<TResult> : QueryCommand
{

    public QueryCommand(IDataContext? dataProvider, LambdaExpression exp, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
        : this(dataProvider, exp, null, condition, joins, paging, sorting, group, having, logger)
    {
    }
    public QueryCommand(IDataContext? dataProvider, Type srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
        : this(dataProvider, null, srcType, condition, joins, paging, sorting, group, having, logger)
    {
    }
    private QueryCommand(IDataContext? dataProvider, LambdaExpression? exp, Type? srcType, LambdaExpression? condition, JoinExpression[]? joins, Paging paging, Sorting[]? sorting, LambdaExpression? group, LambdaExpression? having, ILogger? logger)
        : base(dataProvider, exp, srcType, condition, joins, paging, sorting, group, having, logger)
    {

    }
    public override void PrepareCommand(bool dontCalculateHash, CancellationToken cancellationToken)
    {
        base.PrepareCommand(dontCalculateHash, cancellationToken);
        if (!dontCalculateHash)
        {
            ResultPlanHash = typeof(TResult).GetHashCode();
        }
        ResultType = typeof(TResult);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="nonStreamUsing">true, if optimized for buffered or scalar value results; false for non-buffered (stream) using, when result is IEnumerable or IAsyncEnumerable</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public IPreparedQueryCommand<TResult> Prepare(bool nonStreamUsing = true, CancellationToken cancellationToken = default)
    {
        return _dataContext!.GetPreparedQueryCommand(this, !nonStreamUsing, false, cancellationToken);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(params object[]? @params) => CreateAsyncEnumerator(CancellationToken.None, @params);
    public IAsyncEnumerator<TResult> CreateAsyncEnumerator(CancellationToken cancellationToken, params object[]? @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, true, true, cancellationToken);

        return _dataContext.CreateAsyncEnumerator<TResult>(preparedCommand, @params, cancellationToken);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(params object[]? @params) => CreateEnumeratorAsync(CancellationToken.None, @params);
    public Task<IEnumerator<TResult>> CreateEnumeratorAsync(CancellationToken cancellationToken, params object[]? @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, true, true, cancellationToken);

        return _dataContext.CreateEnumeratorAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
    public IAsyncEnumerable<TResult> Pipeline(params object[] @params) => Pipeline(CancellationToken.None, @params);
    /// <summary>
    /// Streams the result set asynchronously. The previous implementation ran the synchronous
    /// <see cref="ToEnumerable"/> on a thread-pool thread and pushed every row through an
    /// unbounded <c>Channel</c>: it blocked a pool thread for the whole result set, buffered the
    /// entire result in memory when the consumer was slower, and observed cancellation only
    /// between rows. Driving the async enumerator directly keeps the same semantics without the
    /// extra thread and without the unbounded buffer.
    /// </summary>
    public async IAsyncEnumerable<TResult> Pipeline(
        [EnumeratorCancellation] CancellationToken cancellationToken,
        params object[] @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, true, true, cancellationToken);

        await using var enumerator = _dataContext.CreateAsyncEnumerator<TResult>(preparedCommand, @params, cancellationToken);
        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            yield return enumerator.Current;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(params object[] @params) => _dataContext!.GetAsyncEnumerable<TResult>(_dataContext.GetPreparedQueryCommand(this, true, true, CancellationToken.None), CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResult> ToAsyncEnumerable(CancellationToken cancellationToken, params object[] @params) => _dataContext!.GetAsyncEnumerable<TResult>(_dataContext.GetPreparedQueryCommand(this, true, true, cancellationToken), cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<TResult> ToEnumerable(params object[] @params) => _dataContext!.GetEnumerable(_dataContext.GetPreparedQueryCommand(this, true, true, CancellationToken.None), @params);
    public List<TResult> ToList(params ReadOnlySpan<object?> @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);

        return _dataContext.ToList<TResult>(preparedCommand, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(params object[] @params) => _dataContext!.ToListAsync(_dataContext.GetPreparedQueryCommand(this, false, true, CancellationToken.None), @params, CancellationToken.None);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<List<TResult>> ToListAsync(CancellationToken cancellationToken, params object[] @params) => _dataContext!.ToListAsync(_dataContext.GetPreparedQueryCommand(this, false, true, cancellationToken), @params, cancellationToken);
    public TResult[] ToArray(params ReadOnlySpan<object?> @params) => ToList(@params).ToArray();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult[]> ToArrayAsync(params object[] @params) => ToArrayAsync(CancellationToken.None, @params);
    public async Task<TResult[]> ToArrayAsync(CancellationToken cancellationToken, params object[] @params)
        => (await ToListAsync(cancellationToken, @params).ConfigureAwait(false)).ToArray();
    public HashSet<TResult> ToHashSet(params ReadOnlySpan<object?> @params) => new(ToList(@params));
    public HashSet<TResult> ToHashSet(IEqualityComparer<TResult>? comparer, params ReadOnlySpan<object?> @params) => new(ToList(@params), comparer);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<HashSet<TResult>> ToHashSetAsync(params object[] @params) => ToHashSetAsync(CancellationToken.None, @params);
    public async Task<HashSet<TResult>> ToHashSetAsync(CancellationToken cancellationToken, params object[] @params)
        => new(await ToListAsync(cancellationToken, @params).ConfigureAwait(false));
    public async Task<HashSet<TResult>> ToHashSetAsync(IEqualityComparer<TResult>? comparer, CancellationToken cancellationToken, params object[] @params)
        => new(await ToListAsync(cancellationToken, @params).ConfigureAwait(false), comparer);
    public Dictionary<TKey, TResult> ToDictionary<TKey>(Func<TResult, TKey> keySelector, params ReadOnlySpan<object?> @params) where TKey : notnull
        => ToDictionary(keySelector, null, @params);
    public Dictionary<TKey, TResult> ToDictionary<TKey>(Func<TResult, TKey> keySelector, IEqualityComparer<TKey>? comparer, params ReadOnlySpan<object?> @params) where TKey : notnull
    {
        var list = ToList(@params);
        var dict = new Dictionary<TKey, TResult>(list.Count, comparer);
        foreach (var item in list)
            dict.Add(keySelector(item), item);
        return dict;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>(Func<TResult, TKey> keySelector, params object[] @params) where TKey : notnull
        => ToDictionaryAsync(keySelector, CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>(Func<TResult, TKey> keySelector, CancellationToken cancellationToken, params object[] @params) where TKey : notnull
        => ToDictionaryAsync(keySelector, null, cancellationToken, @params);
    public async Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>(Func<TResult, TKey> keySelector, IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken, params object[] @params) where TKey : notnull
    {
        var list = await ToListAsync(cancellationToken, @params).ConfigureAwait(false);
        var dict = new Dictionary<TKey, TResult>(list.Count, comparer);
        foreach (var item in list)
            dict.Add(keySelector(item), item);
        return dict;
    }
    public bool Any() => Any(ReadOnlySpan<object?>.Empty);
    public bool Any(params ReadOnlySpan<object?> @params)
    {
        if (this is not QueryCommand<bool> queryCommand || !queryCommand.SingleRow)
        {
            // Keep IgnoreColumns set: restoring it would change ColumnsPlanHash and force a full
            // re-prepare on every call, even though the cached plan is already built.
            if (!_isPrepared || !IgnoreColumns)
            {
                IgnoreColumns = true;
                PrepareCommand(false, CancellationToken.None);
            }

            queryCommand = EntityBuilder<TResult>.GetAnyCommand(_dataContext!, this);
        }

        var preparedCommand = _dataContext!.GetPreparedQueryCommand(queryCommand, false, true, CancellationToken.None);
        return _dataContext.ExecuteScalar<bool>(preparedCommand, @params, true);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> AnyAsync(params object[] @params) => AnyAsync(CancellationToken.None, @params);
    public async Task<bool> AnyAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (this is not QueryCommand<bool> queryCommand || !queryCommand.SingleRow)
        {
            if (!_isPrepared || !IgnoreColumns)
            {
                IgnoreColumns = true;
                PrepareCommand(false, cancellationToken);
            }

            queryCommand = EntityBuilder<TResult>.GetAnyCommand(_dataContext!, this);
        }

        var preparedCommand = _dataContext!.GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
        return await _dataContext.ExecuteScalar<bool>(preparedCommand, @params, true, cancellationToken).ConfigureAwait(false);
    }
    public TResult? ExecuteScalar(params ReadOnlySpan<object?> @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.ExecuteScalar<TResult>(preparedCommand, @params, false);
    }
    public Task<TResult?> ExecuteScalarAsync(CancellationToken cancellationToken, params object[] @params)
    {
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.ExecuteScalar<TResult>(preparedCommand, @params, false, cancellationToken);
    }

    public TResult First() => First(ReadOnlySpan<object?>.Empty);
    public TResult First(params ReadOnlySpan<object?> @params)
    {
        // Keep the single-row shape on the command. Restoring Paging.Limit would change the cached
        // plan key (QueryPlanEqualityComparer includes Limit/SingleRow) and force a full re-prepare
        // (SQL + hashes) on every call.
        if (Paging.Limit != 1 || !_isPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(false, CancellationToken.None);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.First<TResult>(preparedCommand, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> FirstAsync(params object[] @params) => FirstAsync(CancellationToken.None, @params);
    public Task<TResult> FirstAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (Paging.Limit != 1 || !_isPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(false, cancellationToken);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.FirstAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
    public TResult? FirstOrDefault() => FirstOrDefault(ReadOnlySpan<object?>.Empty);
    public TResult? FirstOrDefault(params ReadOnlySpan<object?> @params)
    {
        if (Paging.Limit != 1 || !_isPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(false, CancellationToken.None);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.FirstOrDefault<TResult>(preparedCommand, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> FirstOrDefaultAsync(params object[] @params) => FirstOrDefaultAsync(CancellationToken.None, @params);
    public Task<TResult?> FirstOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (Paging.Limit != 1 || !_isPrepared)
        {
            SingleRow = true;
            Paging.Limit = 1;
            PrepareCommand(false, cancellationToken);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.FirstOrDefaultAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
    public TResult Single() => Single(ReadOnlySpan<object?>.Empty);
    public TResult Single(params ReadOnlySpan<object?> @params)
    {
        if (Paging.Limit != 2 || !_isPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(false, CancellationToken.None);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.Single<TResult>(preparedCommand, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> SingleAsync(params object[] @params) => SingleAsync(CancellationToken.None, @params);
    public Task<TResult> SingleAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (Paging.Limit != 2 || !_isPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(false, cancellationToken);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.SingleAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
    public TResult? SingleOrDefault() => SingleOrDefault(ReadOnlySpan<object?>.Empty);
    public TResult? SingleOrDefault(params ReadOnlySpan<object?> @params)
    {
        if (Paging.Limit != 2 || !_isPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(false, CancellationToken.None);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, CancellationToken.None);
        return _dataContext.SingleOrDefault<TResult>(preparedCommand, @params);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> SingleOrDefaultAsync(params object[] @params) => SingleOrDefaultAsync(CancellationToken.None, @params);
    public Task<TResult?> SingleOrDefaultAsync(CancellationToken cancellationToken, params object[] @params)
    {
        if (Paging.Limit != 2 || !_isPrepared)
        {
            Paging.Limit = 2;
            PrepareCommand(false, cancellationToken);
        }
        var preparedCommand = _dataContext!.GetPreparedQueryCommand(this, false, true, cancellationToken);
        return _dataContext.SingleOrDefaultAsync<TResult>(preparedCommand, @params, cancellationToken);
    }
    protected override QueryCommand CreateSelf()
    {
        return new QueryCommand<TResult>(_dataContext, _exp, _srcType, _condition, Joins, Paging, _sorting, _groupExp, _having, Logger);
    }
    protected override QueryCommand CreateSelfForClone()
    {
        return new QueryCommand<TResult>(null, null, _srcType, null, CloneForCache(Joins), Paging, _sorting, null, _having, Logger);
    }
    /// <summary>
    /// Builds the command that returns the last row of the ordered query by reversing every
    /// <c>ORDER BY</c> direction and reusing the <c>First</c> path. A query without <c>ORDER BY</c>
    /// has no defined last row, so it is rejected rather than returning an arbitrary one.
    /// </summary>
    private QueryCommand<TResult> ForLast()
    {
        if (_sorting is null || _sorting.Length == 0)
            throw new InvalidOperationException("Last/LastOrDefault requires an ORDER BY; the source order is not defined without one.");

        var reversed = new Sorting[_sorting.Length];
        for (var i = 0; i < _sorting.Length; i++)
        {
            var sorting = _sorting[i];
            var flipped = sorting.ColumnIndex is int columnIndex
                ? new Sorting(columnIndex)
                : new Sorting(sorting.SortExpression!);
            flipped.Direction = sorting.Direction == OrderDirection.Asc ? OrderDirection.Desc : OrderDirection.Asc;
            flipped.PreparedExpression = sorting.PreparedExpression;
            reversed[i] = flipped;
        }

        var cmd = new QueryCommand<TResult>(_dataContext, _exp, _srcType, _condition, Joins, Paging, reversed, _groupExp, _having, Logger);
        CopyTo(cmd, true);
        cmd.ResetPreparation();
        return cmd;
    }
    public TResult Last() => Last(ReadOnlySpan<object?>.Empty);
    public TResult Last(params ReadOnlySpan<object?> @params) => ForLast().First(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult> LastAsync(params object[] @params) => LastAsync(CancellationToken.None, @params);
    public Task<TResult> LastAsync(CancellationToken cancellationToken, params object[] @params) => ForLast().FirstAsync(cancellationToken, @params);
    public TResult? LastOrDefault() => LastOrDefault(ReadOnlySpan<object?>.Empty);
    public TResult? LastOrDefault(params ReadOnlySpan<object?> @params) => ForLast().FirstOrDefault(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResult?> LastOrDefaultAsync(params object[] @params) => LastOrDefaultAsync(CancellationToken.None, @params);
    public Task<TResult?> LastOrDefaultAsync(CancellationToken cancellationToken, params object[] @params) => ForLast().FirstOrDefaultAsync(cancellationToken, @params);
    public QueryCommand<TResult> OrderBy(int columnIndex, OrderDirection direction)
    {
        if (columnIndex < 1) throw new ArgumentException("Column index must be greater than zero", nameof(columnIndex));

        var cmd = new QueryCommand<TResult>(_dataContext, _exp, _srcType, _condition, Joins, Paging, _sorting is null
            ? [new Sorting(columnIndex) { Direction = direction }]
            : [.. _sorting, new Sorting(columnIndex) { Direction = direction }], _groupExp, _having, Logger);

        CopyTo(cmd, true);
        cmd.ResetPreparation();
        return cmd;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<TResult> OrderBy(int columnIndex) => OrderBy(columnIndex, OrderDirection.Asc);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryCommand<TResult> OrderByDescending(int columnIndex) => OrderBy(columnIndex, OrderDirection.Desc);
    public QueryCommand<TResult> Distinct()
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.IsDistinct = true;
        return cmd;
    }
    /// <summary>
    /// Returns a new command carrying statement-level query hints, for example SQL Server
    /// <c>"recompile"</c> (rendered as <c>OPTION (recompile)</c>). Repeated calls accumulate hints.
    /// A dialect that does not support query hints rejects a command that carries them when its SQL
    /// is built.
    /// </summary>
    public QueryCommand<TResult> Hint(params string[] hints)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.AddHints(hints);
        return cmd;
    }
    /// <summary>
    /// Appends a SQL Server <c>FOR JSON</c> clause to the statement, so the result set is returned as a
    /// single JSON document (<c>FOR JSON PATH</c> by default). A dialect that does not support it
    /// rejects the command when its SQL is built. The projection should be a single string column (or
    /// a scalar projection) because the database returns one JSON column.
    /// </summary>
    public QueryCommand<TResult> ForJson(ForJsonMode mode = ForJsonMode.Path, string? root = null, bool includeNullValues = false)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.ForJsonClause = new ForJsonClause(mode, root, includeNullValues);
        return cmd;
    }
    /// <summary>
    /// Appends a SQL Server <c>FOR XML</c> clause to the statement, so the result set is returned as a
    /// single XML document (<c>FOR XML PATH</c> by default). A dialect that does not support it rejects
    /// the command when its SQL is built, and combining it with <see cref="ForJson"/> throws.
    /// </summary>
    public QueryCommand<TResult> ForXml(ForXmlMode mode = ForXmlMode.Path, string? elementName = null, string? root = null, bool elements = false)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.ForXmlClause = new ForXmlClause(mode, elementName, root, elements);
        return cmd;
    }
    public QueryCommand<TResult> Union<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.Distinct);
        return cmd;
    }
    public QueryCommand<TResult> UnionAll<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.All);
        return cmd;
    }
    public QueryCommand<TResult> Intersect<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.Intersect);
        return cmd;
    }
    public QueryCommand<TResult> IntersectAll<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.IntersectAll);
        return cmd;
    }
    public QueryCommand<TResult> Except<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.Except);
        return cmd;
    }
    public QueryCommand<TResult> ExceptAll<T>(QueryCommand<T> queryCommand)
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ResetPreparation();
        cmd.SetOperation(queryCommand, UnionType.ExceptAll);
        return cmd;
    }

    /// <summary>
    /// Clone of this command with the set operation removed, so the first operand of a
    /// <c>UNION</c>/<c>INTERSECT</c>/<c>EXCEPT</c> can be executed independently.
    /// </summary>
    internal QueryCommand<TResult> CloneWithoutUnion()
    {
        var cmd = (QueryCommand<TResult>)Clone();
        cmd.ClearUnion();
        cmd.ResetPreparation();
        return cmd;
    }

}
