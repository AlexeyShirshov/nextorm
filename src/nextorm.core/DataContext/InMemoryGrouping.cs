using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Grouping and aggregate axis of the in-memory provider: detects a foldable aggregate projection
/// and evaluates a <c>GROUP BY</c> query in process (filter, group by key, fold <c>HAVING</c> and the
/// projection per group).
/// <para>
/// The compiled aggregate selector is cached in the caller-owned per-instance dictionary (the
/// context's <c>_aggregateSelectorCache</c>), so this type is stateless. The reflection-addressed
/// <c>CreateCompiledQuery</c> stays private on the context and is reached through the
/// <c>GetCompiledQuery</c> internal seam. Bodies are moved verbatim from <see cref="InMemoryDataContext"/>
/// (F13 follow-up).
/// </para>
/// </summary>
internal static class InMemoryGrouping
{
    public static Delegate? GetAggregateSelector<TEntity>(Expression? selectorBody, ParameterExpression? parameter, Type valueType, QueryCommand queryCommand, IDictionary<ExpressionKey, Delegate> aggregateSelectorCache)
    {
        if (selectorBody is null || parameter is null) return null;

        var key = new ExpressionKey(selectorBody, queryCommand);
        if (aggregateSelectorCache.TryGetValue(key, out var cached))
            return cached;

        var compiled = InMemoryAggregates.CompileSelector<TEntity>(selectorBody, parameter, valueType);
        aggregateSelectorCache[key] = compiled;
        return compiled;
    }

    /// <summary>
    /// Detects an aggregate projection (<c>SqlFunctions.Sql.min/max/sum/avg/count/...</c>) in a single-column
    /// select list, so the enumerator can compute the value over the whole source instead of mapping
    /// every row through the (CLR no-op) aggregate method.
    /// </summary>
    public static bool TryGetAggregate(QueryCommand queryCommand, out string? name, out ParameterExpression? parameter, out Expression? selectorBody)
    {
        name = null;
        parameter = null;
        selectorBody = null;

        if (queryCommand.SelectList is not [var column]) return false;
        if (column.Expression is not LambdaExpression lambda) return false;
        if (lambda.Body is not MethodCallExpression call) return false;
        if (call.Method.DeclaringType != typeof(CommonFunctions) && call.Method.DeclaringType != typeof(PostgresFunctions)) return false;
        if (!InMemoryAggregates.IsAggregate(call.Method.Name)) return false;

        name = call.Method.Name;
        parameter = lambda.Parameters.Count > 0 ? lambda.Parameters[0] : null;

        if (call.Arguments.Count > 0)
        {
            switch (call.Arguments[0])
            {
                // count() is emitted as a single empty-array argument for the params array.
                case NewArrayExpression { Expressions.Count: 0 }:
                    break;
                case NewArrayExpression { Expressions.Count: 1 } array:
                    selectorBody = array.Expressions[0];
                    break;
                case NewArrayExpression:
                    throw new NotSupportedException($"Aggregate '{name}' over multiple properties is not supported by the in-memory provider.");
                default:
                    selectorBody = call.Arguments[0];
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Evaluates a <c>GROUP BY</c> query in process: the source is filtered, grouped by the key
    /// selector, and for each group the <c>HAVING</c> predicate and the projection are evaluated with
    /// aggregate calls folded to per-group constants. ORDER BY is applied to the projected rows,
    /// matching SQL where grouping precedes ordering.
    /// </summary>
    public static IAsyncEnumerator<TResult> CreateGroupedEnumerator<TResult, TEntity>(
        InMemoryDataContext context,
        QueryCommand<TResult> queryCommand,
        InMemoryPreparedQueryCommand<TResult> cacheEntry,
        IEnumerable<TEntity> data,
        object[]? @params)
    {
        if (queryCommand.ProjectionExpression is not LambdaExpression projection)
            throw new NotSupportedException("GroupBy requires a projection in the in-memory provider.");

        if (queryCommand.GroupingType != GroupingType.None)
            throw new NotSupportedException("The ROLLUP/CUBE grouping modifiers are not supported by the in-memory provider.");

        if (queryCommand.GroupByWithTotals)
            throw new NotSupportedException("The GROUP BY ... WITH TOTALS modifier is not supported by the in-memory provider.");

        if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiled)
        {
            compiled = (InMemoryCompiledQuery<TResult, TEntity>)context.GetCompiledQuery<TResult, TEntity>(queryCommand);
            cacheEntry.CompiledQuery = compiled;
        }

        Func<TEntity, bool>? predicate = compiled.ConditionDirect;
        if (compiled.ConditionFactory is not null && @params is not null)
            predicate = compiled.ConditionFactory(@params);

        var source = predicate is null ? data : data.Where(predicate);

        var entityParam = (ParameterExpression)projection.Parameters[0];
        var keySelectorBody = Expression.Convert(queryCommand.GroupBy!.Body, typeof(object));
        var keySelector = Expression.Lambda<Func<TEntity, object?>>(keySelectorBody, (ParameterExpression)queryCommand.GroupBy.Parameters[0]).Compile();

        // Grouping by value: anonymous types and records expose structural equality. A linear scan
        // keeps null keys working without a null-hostile dictionary key.
        var keys = new List<object?>();
        var groups = new List<List<TEntity>>();
        foreach (var row in source)
        {
            var key = keySelector(row);
            var found = false;
            for (var i = 0; i < keys.Count; i++)
            {
                if (Equals(keys[i], key))
                {
                    groups[i].Add(row);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                keys.Add(key);
                groups.Add([row]);
            }
        }

        var results = new List<TResult>(groups.Count);
        foreach (var group in groups)
        {
            if (queryCommand.Having is not null)
            {
                var havingParam = (ParameterExpression)queryCommand.Having.Parameters[0];
                var havingBody = new InMemoryGroupAggregateVisitor<TEntity>(group, havingParam).Visit(queryCommand.Having.Body);
                var having = Expression.Lambda<Func<TEntity, bool>>(havingBody, havingParam).Compile();
                if (!having(group[0])) continue;
            }

            var projectionBody = new InMemoryGroupAggregateVisitor<TEntity>(group, entityParam).Visit(projection.Body);
            var map = Expression.Lambda<Func<TEntity, TResult>>(projectionBody, entityParam).Compile();
            results.Add(map(group[0]));
        }

        if (queryCommand.Sorting is not null)
            results = InMemoryOrdering.ApplyProjectedOrdering(results, queryCommand);

        return new InMemoryListEnumerator<TResult>(results, queryCommand.IsDistinct);
    }
}
