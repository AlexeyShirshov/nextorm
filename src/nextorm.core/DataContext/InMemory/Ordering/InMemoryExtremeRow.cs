using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Row-selection axis of the in-memory provider: evaluates the
/// <c>SelectWhereMax</c>/<c>SelectWhereMin</c> request carried by
/// <see cref="QueryCommand.ExtremeRow"/> in process.
/// <para>
/// The clause keeps the row(s) attaining the maximum or minimum of a value selector. Without a group
/// key the extremum is taken over the whole (filtered) source; with one the source is partitioned by
/// the key (null is its own group) and the extremum is chosen per partition. Comparison nulls are
/// ignored, and a partition whose comparison values are all null contributes no rows. The surviving
/// rows then run through the query projection, so row/duplicate semantics match the mapped path.
/// Selectors are compiled once per query, outside the row loop, and the group selector is invoked
/// once per non-null row while the value selector runs during the per-partition fold.
/// </para>
/// <para>
/// A composite value selector (<c>x =&gt; new { x.A, x.B }</c>) is compared component-wise in
/// declaration order, mirroring the SQL <c>ORDER BY comp1, comp2</c> lowering, and a row is excluded as
/// soon as any component is null (matching the SQL <c>comp IS NOT NULL</c> filter). This also makes
/// anonymous types work, which <see cref="Comparer{T}.Default"/> cannot order.
/// </para>
/// </summary>
internal static class InMemoryExtremeRow
{
    private static readonly MethodInfo SelectSurvivorsMethod =
        typeof(InMemoryExtremeRow).GetMethod(nameof(SelectSurvivors), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<Type, MethodInfo> SurvivorsMethodCache = new();

    /// <summary>
    /// Materialises the extreme-row enumerator for a command that carries an <see cref="ExtremeRowClause"/>.
    /// The WHERE predicate is applied first (the source reaches here unfiltered, like the mapped path),
    /// then the surviving rows are projected and optionally ordered/distincted.
    /// </summary>
    public static IAsyncEnumerator<TResult> CreateEnumerator<TResult, TEntity>(
        InMemoryDataContext context,
        QueryCommand<TResult> queryCommand,
        InMemoryPreparedQueryCommand<TResult> cacheEntry,
        IEnumerable<TEntity> data,
        object[]? @params)
    {
        var extremeRow = queryCommand.ExtremeRow!;

        // Fail closed on exactly the combinations the SQL lowering rejects, so the in-memory provider
        // never silently returns a result for a query the mapped providers refuse. A join shapes the
        // source before the fold (so the extremum would be taken over the wrong rows), paging would be
        // applied to the pre-fold rows (so the surviving row would not be the page's extremum), and the
        // remaining modifiers (HAVING, CTEs, window functions, hints, ...) have no in-memory equivalent.
        ExtremeRowCompatibility.EnsureModifiersCompatible(queryCommand);

        if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiled)
        {
            compiled = (InMemoryCompiledQuery<TResult, TEntity>)context.GetCompiledQuery<TResult, TEntity>(queryCommand);
            cacheEntry.CompiledQuery = compiled;
        }

        // The extremum must be chosen over the rows the query actually selects: apply WHERE here, as
        // the aggregate path does, before the fold.
        Func<TEntity, bool>? predicate = compiled.ConditionDirect;
        if (compiled.ConditionFactory is not null && @params is not null)
            predicate = compiled.ConditionFactory(@params);

        var filtered = predicate is null ? data : data.Where(predicate);

        // Compile every component once, outside the row loop. A composite selector is compared
        // component-wise (the SQL ORDER BY comp1, comp2 shape) and excludes a row as soon as any
        // component is null (the SQL `comp IS NOT NULL` filter); a single value is one component.
        var components = CompileComponents<TEntity>(extremeRow.ValueSelector);

        Func<TEntity, object?>? groupSelector = null;
        if (extremeRow.GroupBy is { } groupBy)
        {
            groupSelector = Expression.Lambda<Func<TEntity, object?>>(
                groupBy.Body,
                (ParameterExpression)groupBy.Parameters[0]).Compile();
        }

        var selectSurvivors = SurvivorsMethodCache.GetOrAdd(
            typeof(TEntity),
            static entityType => SelectSurvivorsMethod.MakeGenericMethod(entityType));

        var survivors = (List<TEntity>)selectSurvivors.Invoke(
            null,
            [filtered, components, groupSelector, extremeRow.Kind == ExtremeKind.Max, extremeRow.Ties == ExtremeRowTies.All])!;

        // ORDER BY applies to the surviving rows, before the projection turns them into TResult.
        if (queryCommand.Sorting is not null)
            survivors = InMemoryOrdering.ApplyOrdering(context, survivors, queryCommand, context.SortingSelectorCache).ToList();

        var map = context.GetMap<TResult, TEntity>(queryCommand)();
        var results = new List<TResult>(survivors.Count);
        foreach (var row in survivors)
            results.Add(map(row));

        return new InMemoryListEnumerator<TResult>(results, queryCommand.IsDistinct);
    }

    /// <summary>
    /// Compiles the value selector into one boxed accessor per comparison component. A composite
    /// <see cref="NewExpression"/> (an anonymous type or a tuple constructor, matching the SQL
    /// <c>BuildKeyColumns</c> shape) yields one accessor per constructor argument in declaration order;
    /// any other body yields a single accessor.
    /// </summary>
    private static IReadOnlyList<Func<TEntity, object?>> CompileComponents<TEntity>(LambdaExpression valueSelector)
    {
        var parameter = (ParameterExpression)valueSelector.Parameters[0];

        if (valueSelector.Body is NewExpression ctor && !TypeFacts.IsSingleColumnProjection(ctor.Type))
        {
            var arguments = ctor.Arguments;
            var components = new Func<TEntity, object?>[arguments.Count];
            for (var i = 0; i < arguments.Count; i++)
            {
                components[i] = Expression.Lambda<Func<TEntity, object?>>(
                    Expression.Convert(arguments[i], typeof(object)), parameter).Compile();
            }

            return components;
        }

        return
        [
            Expression.Lambda<Func<TEntity, object?>>(
                Expression.Convert(valueSelector.Body, typeof(object)), parameter).Compile()
        ];
    }

    /// <summary>
    /// Partitions the source by <paramref name="groupSelector"/> (a null selector means one global
    /// partition; a null key is its own group) and keeps, per partition, the single row or every row
    /// attaining the maximal/minimal comparison value. A row whose comparison tuple has any null
    /// component is excluded, so a partition with no fully non-null value is dropped. Component
    /// comparison is lexicographic, matching the SQL <c>ORDER BY</c> shape.
    /// </summary>
    private static List<TEntity> SelectSurvivors<TEntity>(
        IEnumerable<TEntity> source,
        IReadOnlyList<Func<TEntity, object?>> components,
        Func<TEntity, object?>? groupSelector,
        bool keepMax,
        bool keepAllTies)
    {
        var comparer = Comparer<object>.Default;

        // A linear scan keeps null and composite (anonymous-type) keys working without a null-hostile
        // dictionary key; the same shape the GROUP BY evaluator uses.
        var keys = new List<object?>();
        var groups = new List<List<(TEntity Row, object?[] Values)>>();

        foreach (var row in source)
        {
            var values = new object?[components.Count];
            var hasNull = false;
            for (var i = 0; i < components.Count; i++)
            {
                var value = components[i](row);
                if (value is null)
                {
                    hasNull = true;
                    break;
                }

                values[i] = value;
            }

            if (hasNull)
                continue;

            var key = groupSelector?.Invoke(row);
            var index = -1;
            for (var i = 0; i < keys.Count; i++)
            {
                if (Equals(keys[i], key))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                keys.Add(key);
                groups.Add([]);
                index = groups.Count - 1;
            }

            groups[index].Add((row, values));
        }

        var survivors = new List<TEntity>();
        foreach (var group in groups)
        {
            var best = group[0].Values;
            for (var i = 1; i < group.Count; i++)
            {
                var values = group[i].Values;
                var cmp = CompareValues(comparer, values, best);
                if (keepMax ? cmp > 0 : cmp < 0)
                    best = values;
            }

            foreach (var (row, values) in group)
            {
                if (CompareValues(comparer, values, best) == 0)
                {
                    survivors.Add(row);
                    if (!keepAllTies)
                        break;
                }
            }
        }

        return survivors;
    }

    /// <summary>
    /// Lexicographic component comparison: the first differing component decides, and two tuples are
    /// equal only when every component is equal (no nulls reach here).
    /// </summary>
    private static int CompareValues(Comparer<object> comparer, object?[] left, object?[] right)
    {
        for (var i = 0; i < left.Length; i++)
        {
            var cmp = comparer.Compare(left[i], right[i]);
            if (cmp != 0)
                return cmp;
        }

        return 0;
    }
}
