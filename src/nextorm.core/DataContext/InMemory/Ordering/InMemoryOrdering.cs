using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Ordering axis of the in-memory provider: applies <c>ORDER BY</c> to a buffered source, including
/// the projected form that orders grouped/set-operation results by select-list column index.
/// <para>
/// The compiled key selectors are held in a caller-owned dictionary (the context's per-instance
/// <c>_sortingSelectorCache</c>), so this type is stateless and does not share compiled delegates
/// process-wide. Bodies are moved verbatim from <see cref="InMemoryDataContext"/> (F13 follow-up).
/// </para>
/// </summary>
internal static class InMemoryOrdering
{
    /// <summary>Applies the query's <c>ORDER BY</c> to a buffered source using the compiled key selectors.</summary>
    public static IEnumerable<TEntity> ApplyOrdering<TEntity>(InMemoryDataContext context, IEnumerable<TEntity> data, QueryCommand queryCommand, IDictionary<ExpressionKey, Delegate> sortingSelectorCache)
    {
        IOrderedEnumerable<TEntity>? intData = null;
        foreach (var sorting in queryCommand.Sorting!)
        {
            var del = GetSortingSelector<TEntity>(context, sorting, queryCommand, sortingSelectorCache);
            var nullsPosition = NullsOrderingRenderer.Validate(sorting.NullsPosition);
            if (nullsPosition == NullsPosition.Default)
            {
                // A second key must extend the previous ordering instead of replacing it: Enumerable.OrderBy
                // is a fresh stable sort, so calling it again would silently make the last key primary and
                // the earlier keys (including an explicit placement) disappear. The single-key path is
                // unchanged.
                if (intData is null)
                    intData = sorting.Direction == OrderDirection.Asc ? data.OrderBy(del) : data.OrderByDescending(del);
                else
                    intData = sorting.Direction == OrderDirection.Asc ? intData.ThenBy(del) : intData.ThenByDescending(del);
            }
            else
            {
                // Placement is direction-independent, matching the SQL oracle: the rank key groups the
                // NULLs first or last (always ascending), then the value key applies the requested
                // direction inside each group.
                var nullsFirst = nullsPosition == NullsPosition.First;
                Func<TEntity, int> rank = row => del(row) is null ? (nullsFirst ? 0 : 1) : (nullsFirst ? 1 : 0);
                var ranked = intData is null ? data.OrderBy(rank) : intData.ThenBy(rank);
                intData = sorting.Direction == OrderDirection.Asc ? ranked.ThenBy(del) : ranked.ThenByDescending(del);
            }
        }
        return intData ?? data;
    }

    /// <summary>
    /// Compiles (once per expression/command pair) the ordering key selector. Without the cache an
    /// ordered query recompiled its selector on every execution, which dominated <c>Last</c>/ordered
    /// iteration.
    /// </summary>
    private static Func<TEntity, object> GetSortingSelector<TEntity>(InMemoryDataContext context, Sorting sorting, QueryCommand queryCommand, IDictionary<ExpressionKey, Delegate> sortingSelectorCache)
    {
        Expression<Func<TEntity, object>> expression;
        if (InMemoryCorrelatedSubqueryRewriter.IsNeeded(queryCommand))
        {
            var rewritten = new InMemoryCorrelatedSubqueryRewriter(context, queryCommand).Rewrite(sorting.PreparedExpression!);
            expression = rewritten is LambdaExpression { Parameters.Count: 1 } single
                ? Expression.Lambda<Func<TEntity, object>>(
                    single.Body.Type == typeof(object) ? single.Body : Expression.Convert(single.Body, typeof(object)),
                    single.Parameters[0])
                : throw new NotSupportedException("The in-memory provider supports ORDER BY expressions that are single-parameter lambdas only.");
        }
        else
        {
            // A key over a reference type (for example string) is compiled as
            // Expression<Func<TEntity, string>>: C# elides the implicit reference conversion to object in
            // an expression tree, so the runtime cast to Expression<Func<TEntity, object>> would fail.
            // Rebuild the lambda over object instead of casting, which keeps the natural key selector for
            // every nullable value and reference type.
            expression = sorting.PreparedExpression switch
            {
                Expression<Func<TEntity, object>> typed => typed,
                LambdaExpression { Parameters.Count: 1 } lambda => Expression.Lambda<Func<TEntity, object>>(
                    lambda.Body.Type == typeof(object) ? lambda.Body : Expression.Convert(lambda.Body, typeof(object)),
                    lambda.Parameters[0]),
                _ => throw new NotSupportedException("The in-memory provider supports ORDER BY expressions that are single-parameter lambdas only."),
            };
        }

        var key = new ExpressionKey(expression, queryCommand);
        if (sortingSelectorCache.TryGetValue(key, out var cached))
            return (Func<TEntity, object>)cached;

        var compiled = expression.Compile();
        sortingSelectorCache[key] = compiled;
        return compiled;
    }

    /// <summary>
    /// Orders an async source by buffering it: ordering needs the whole set before the first row can
    /// be yielded, and an <see cref="IAsyncEnumerable{T}"/> cannot be re-sorted lazily.
    /// </summary>
    public static async IAsyncEnumerable<TEntity> OrderAsyncEnumerable<TEntity>(
        InMemoryDataContext context,
        IAsyncEnumerable<TEntity> source,
        QueryCommand queryCommand,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        IDictionary<ExpressionKey, Delegate> sortingSelectorCache)
    {
        var data = new List<TEntity>();
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            data.Add(item);

        foreach (var item in ApplyOrdering(context, data, queryCommand, sortingSelectorCache))
            yield return item;
    }

    /// <summary>
    /// Orders projected rows by the select-list column index, as SQL does for an <c>ORDER BY</c> on a
    /// grouped query. Expression-based ordering is not supported on grouped in-memory results.
    /// </summary>
    public static List<TResult> ApplyProjectedOrdering<TResult>(List<TResult> results, QueryCommand queryCommand)
    {
        if (queryCommand.SelectList is null)
            throw new NotSupportedException("Grouped ordering requires a select list in the in-memory provider.");

        IOrderedEnumerable<TResult>? ordered = null;
        foreach (var sorting in queryCommand.Sorting!)
        {
            if (sorting.ColumnIndex is not int columnIndex)
                throw new NotSupportedException("Grouped ordering by expression is not supported by the in-memory provider; order by column index instead.");

            var propertyName = queryCommand.SelectList[columnIndex - 1].PropertyName
                ?? throw new NotSupportedException($"{nameof(Sorting)} column {columnIndex} has no name in the in-memory provider.");
            var property = typeof(TResult).GetProperty(propertyName)
                ?? throw new NotSupportedException($"Grouped result type '{typeof(TResult).Name}' has no property '{propertyName}'.");

            object? Key(TResult row) => property.GetValue(row);

            var nullsPosition = NullsOrderingRenderer.Validate(sorting.NullsPosition);
            if (nullsPosition == NullsPosition.Default)
            {
                ordered = sorting.Direction == OrderDirection.Asc
                    ? (ordered is null ? results.OrderBy(Key) : ordered.ThenBy(Key))
                    : (ordered is null ? results.OrderByDescending(Key) : ordered.ThenByDescending(Key));
            }
            else
            {
                var nullsFirst = nullsPosition == NullsPosition.First;
                Func<TResult, int> rank = row => Key(row) is null ? (nullsFirst ? 0 : 1) : (nullsFirst ? 1 : 0);
                var ranked = ordered is null ? results.OrderBy(rank) : ordered.ThenBy(rank);
                ordered = sorting.Direction == OrderDirection.Asc ? ranked.ThenBy(Key) : ranked.ThenByDescending(Key);
            }
        }

        return ordered?.ToList() ?? results;
    }
}
