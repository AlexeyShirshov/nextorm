namespace NextORM.Core;

/// <summary>
/// Set-operation axis of the in-memory provider: both operands of a
/// <c>UNION</c>/<c>UNION ALL</c>/<c>INTERSECT</c>/<c>EXCEPT</c> (and the <c>*ALL</c> variants) are
/// materialised and combined with SQL value semantics, then the projected ordering is applied.
/// <para>
/// The context is passed in only to reach the public <c>GetPreparedQueryCommand</c>; no instance
/// cache is touched here. Bodies are moved verbatim from <see cref="InMemoryDataContext"/> (F13
/// follow-up).
/// </para>
/// </summary>
internal static class InMemorySetOperations
{
    /// <summary>
    /// Evaluates a set operation (<c>UNION</c>/<c>UNION ALL</c>/<c>INTERSECT</c>/<c>INTERSECT ALL</c>/
    /// <c>EXCEPT</c>/<c>EXCEPT ALL</c>): both operands are materialised and combined with SQL value
    /// semantics, then the whole result is ordered/paged.
    /// </summary>
    public static IAsyncEnumerator<TResult> CreateSetOperationEnumerator<TResult>(InMemoryDataContext context, QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var left = MaterializeBuffered(context, queryCommand.CloneWithoutUnion(), @params, cancellationToken);

        if (queryCommand.UnionQuery is not QueryCommand<TResult> rightQuery)
            throw new NotSupportedException("Set operations between different result types are not supported by the in-memory provider.");

        var right = MaterializeBuffered(context, rightQuery, @params, cancellationToken);

        var combined = CombineSet(left, right, queryCommand.UnionType);

        if (queryCommand.Sorting is not null)
            combined = InMemoryOrdering.ApplyProjectedOrdering(combined, queryCommand);

        return new InMemoryListEnumerator<TResult>(combined, false);
    }

    /// <summary>
    /// Fully materialises a command. Set operations are buffered, so an async source cannot be combined
    /// and is rejected instead of silently producing a wrong result.
    /// </summary>
    private static List<TResult> MaterializeBuffered<TResult>(InMemoryDataContext context, QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var prepared = (InMemoryPreparedQueryCommand<TResult>)context.GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
        var enumerator = prepared.CreateEnumerator(prepared.QueryCommand, prepared, @params, cancellationToken);

        if (enumerator is not IEnumerator<TResult> sync)
        {
            if (enumerator is IAsyncDisposable disposable)
                disposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw new NotSupportedException("Set operations over an async source are not supported by the in-memory provider.");
        }

        var list = new List<TResult>();
        try
        {
            while (sync.MoveNext()) list.Add(sync.Current);
        }
        finally
        {
            (sync as IDisposable)?.Dispose();
        }

        return list;
    }

    private static List<TResult> CombineSet<TResult>(List<TResult> left, List<TResult> right, UnionType type)
    {
        var comparer = InMemoryDistinct.GetComparer<TResult>();
        switch (type)
        {
            case UnionType.All:
            {
                var all = new List<TResult>(left.Count + right.Count);
                all.AddRange(left);
                all.AddRange(right);
                return all;
            }
            case UnionType.Distinct:
            {
                var seen = new HashSet<TResult>(comparer);
                var result = new List<TResult>();
                foreach (var item in left)
                    if (seen.Add(item)) result.Add(item);
                foreach (var item in right)
                    if (seen.Add(item)) result.Add(item);
                return result;
            }
            case UnionType.Intersect:
            {
                var rightSet = new HashSet<TResult>(right, comparer);
                var seen = new HashSet<TResult>(comparer);
                var result = new List<TResult>();
                foreach (var item in left)
                    if (rightSet.Contains(item) && seen.Add(item)) result.Add(item);
                return result;
            }
            case UnionType.IntersectAll:
            {
                var counts = CountValues(right, comparer);
                var result = new List<TResult>();
                foreach (var item in left)
                {
                    var idx = FindValue(counts, item, comparer);
                    if (idx < 0 || counts[idx].Count == 0) continue;
                    counts[idx] = (counts[idx].Value, counts[idx].Count - 1);
                    result.Add(item);
                }

                return result;
            }
            case UnionType.Except:
            {
                var rightSet = new HashSet<TResult>(right, comparer);
                var seen = new HashSet<TResult>(comparer);
                var result = new List<TResult>();
                foreach (var item in left)
                    if (!rightSet.Contains(item) && seen.Add(item)) result.Add(item);
                return result;
            }
            case UnionType.ExceptAll:
            {
                var counts = CountValues(right, comparer);
                var result = new List<TResult>();
                foreach (var item in left)
                {
                    var idx = FindValue(counts, item, comparer);
                    if (idx >= 0 && counts[idx].Count > 0)
                        counts[idx] = (counts[idx].Value, counts[idx].Count - 1);
                    else
                        result.Add(item);
                }

                return result;
            }
            default:
                throw new NotSupportedException(type.ToString("G"));
        }
    }

    private static List<(TResult Value, int Count)> CountValues<TResult>(List<TResult> values, IEqualityComparer<TResult> comparer)
    {
        var counts = new List<(TResult Value, int Count)>();
        foreach (var item in values)
        {
            var idx = FindValue(counts, item, comparer);
            if (idx < 0) counts.Add((item, 1));
            else counts[idx] = (counts[idx].Value, counts[idx].Count + 1);
        }

        return counts;
    }

    private static int FindValue<TResult>(List<(TResult Value, int Count)> counts, TResult item, IEqualityComparer<TResult> comparer)
    {
        for (var i = 0; i < counts.Count; i++)
            if (comparer.Equals(counts[i].Value, item))
                return i;
        return -1;
    }
}
