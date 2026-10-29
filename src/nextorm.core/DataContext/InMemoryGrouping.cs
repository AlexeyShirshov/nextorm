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

    /// <summary>
    /// Evaluates a <c>SelectGroup</c> query in process. The grouping keys were inferred from the raw
    /// projection at preparation time, so the execution shape follows the resolved
    /// <see cref="SelectGroupMode"/>: one row per distinct key vector (keyed), one row folded over the
    /// whole filtered source (global aggregate), or one row per distinct row-independent output vector
    /// (constant projection). <c>HAVING</c> and the projection are folded per group through
    /// <see cref="InMemoryGroupAggregateVisitor{TEntity}"/>, and <c>ORDER BY</c> is applied to the
    /// materialized rows. Every provider function that has no in-memory execution is rejected before a
    /// row is touched instead of degrading to a marker default or a first-row value.
    /// </summary>
    public static IAsyncEnumerator<TResult> CreateSelectGroupEnumerator<TResult, TEntity>(
        InMemoryDataContext context,
        QueryCommand<TResult> queryCommand,
        InMemoryPreparedQueryCommand<TResult> cacheEntry,
        IEnumerable<TEntity> data,
        object[]? @params)
    {
        if (queryCommand.ProjectionExpression is not { Parameters.Count: 1 } projection)
            throw new NotSupportedException("SelectGroup requires a single-parameter projection in the in-memory provider.");

        if (queryCommand.GroupingType != GroupingType.None)
            throw new NotSupportedException("The ROLLUP/CUBE grouping modifiers are not supported by the in-memory provider.");

        if (queryCommand.GroupByWithTotals)
            throw new NotSupportedException("The GROUP BY ... WITH TOTALS modifier is not supported by the in-memory provider.");

        if (queryCommand.SelectGroupMode is SelectGroupMode.None)
            throw new NotSupportedException("SelectGroup projection analysis is missing.");

        // Fail closed before anything is evaluated: a custom [SqlFunction], a filtered aggregate and a
        // provider-only function (aggregate or window member) have no in-memory body, so executing the
        // compiled call would return its marker default, a first-row value or a NullReferenceException.
        SelectGroupFunctionGuard.Reject(projection.Body);
        if (queryCommand.Having is { } having)
            SelectGroupFunctionGuard.Reject(having.Body);
        if (queryCommand.Sorting is { } sorting)
        {
            for (var i = 0; i < sorting.Length; i++)
            {
                if (sorting[i].SortExpression is LambdaExpression sortLambda)
                    SelectGroupFunctionGuard.Reject(sortLambda.Body);
            }
        }

        if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiled)
        {
            compiled = (InMemoryCompiledQuery<TResult, TEntity>)context.GetCompiledQuery<TResult, TEntity>(queryCommand);
            cacheEntry.CompiledQuery = compiled;
        }

        Func<TEntity, bool>? predicate = compiled.ConditionDirect;
        if (compiled.ConditionFactory is not null && @params is not null)
            predicate = compiled.ConditionFactory(@params);

        // Materialised once: every mode needs the filtered row count (the empty-source semantics) and a
        // representative row to evaluate the row-independent parts of the projection.
        var rows = (predicate is null ? data : data.Where(predicate)).ToList();

        var results = queryCommand.SelectGroupMode switch
        {
            SelectGroupMode.Keyed => CreateKeyed<TResult, TEntity>(queryCommand, projection, rows),
            SelectGroupMode.GlobalAggregate => CreateGlobalAggregate<TResult, TEntity>(queryCommand, projection, rows),
            SelectGroupMode.ConstantProjection => CreateConstantProjection<TResult, TEntity>(queryCommand, projection, rows),
            _ => throw new NotSupportedException("SelectGroup projection analysis is missing."),
        };

        if (queryCommand.Sorting is not null)
            results = InMemoryOrdering.ApplyProjectedOrdering(results, queryCommand);

        // The modes already collapse duplicates, so DISTINCT is never applied on top of them.
        return new InMemoryListEnumerator<TResult>(results, distinct: false);
    }

    /// <summary>
    /// Keyed mode: group the filtered rows by the inferred key vector (a linear scan keeps null keys
    /// working without a null-hostile dictionary key) and fold each group's projection with a
    /// representative row. A group is never empty, so the representative exists by construction.
    /// </summary>
    private static List<TResult> CreateKeyed<TResult, TEntity>(
        QueryCommand<TResult> queryCommand,
        LambdaExpression projection,
        List<TEntity> rows)
    {
        var keys = queryCommand.SelectGroupKeys;
        if (keys is not { Count: > 0 })
            throw new NotSupportedException("SelectGroup keyed execution requires the inferred grouping keys.");

        var entityParam = (ParameterExpression)projection.Parameters[0];
        var keySelector = CompileVectorSelector<TEntity>(keys, entityParam);

        var groupKeys = new List<object?[]>();
        var groups = new List<List<TEntity>>();
        foreach (var row in rows)
        {
            var key = keySelector(row);
            var found = false;
            for (var i = 0; i < groupKeys.Count; i++)
            {
                if (!VectorEquals(groupKeys[i], key))
                    continue;

                groups[i].Add(row);
                found = true;
                break;
            }

            if (!found)
            {
                groupKeys.Add(key);
                groups.Add([row]);
            }
        }

        var results = new List<TResult>(groups.Count);
        foreach (var group in groups)
        {
            if (!PassesHaving(queryCommand, group))
                continue;

            var projectionBody = new InMemoryGroupAggregateVisitor<TEntity>(group, entityParam).Visit(projection.Body);
            var map = Expression.Lambda<Func<TEntity, TResult>>(projectionBody, entityParam).Compile();
            results.Add(map(group[0]));
        }

        return results;
    }

    /// <summary>
    /// Global-aggregate mode: the whole filtered source is one group. The projection and <c>HAVING</c>
    /// are folded over it; an empty source is folded over <c>default(TEntity)</c> because the analyzer
    /// guarantees no row reference survives outside an aggregate in this mode, and the aggregate fold
    /// already defines the empty-input values (sum over no rows is 0, the rest default).
    /// </summary>
    private static List<TResult> CreateGlobalAggregate<TResult, TEntity>(
        QueryCommand<TResult> queryCommand,
        LambdaExpression projection,
        List<TEntity> rows)
    {
        var entityParam = (ParameterExpression)projection.Parameters[0];
        var exemplar = rows.Count > 0 ? rows[0] : default(TEntity)!;

        if (queryCommand.Having is { } having)
        {
            var havingParam = (ParameterExpression)having.Parameters[0];
            var havingBody = new InMemoryGroupAggregateVisitor<TEntity>(rows, havingParam).Visit(having.Body);
            var predicate = Expression.Lambda<Func<TEntity, bool>>(havingBody, havingParam).Compile();
            if (!predicate(exemplar))
                return [];
        }

        var projectionBody = new InMemoryGroupAggregateVisitor<TEntity>(rows, entityParam).Visit(projection.Body);
        var map = Expression.Lambda<Func<TEntity, TResult>>(projectionBody, entityParam).Compile();
        return [map(exemplar)];
    }

    /// <summary>
    /// Constant-projection mode: every output is row-independent, so the statement behaves like
    /// <c>select distinct ...</c> over the filtered source. The outputs are evaluated per row into a
    /// vector, duplicate vectors are collapsed structurally, and <c>TResult</c> is materialized from the
    /// first row of each distinct vector (no source row means no result row at all). A validated
    /// row-independent <c>HAVING</c> filters the source before that projection.
    /// </summary>
    private static List<TResult> CreateConstantProjection<TResult, TEntity>(
        QueryCommand<TResult> queryCommand,
        LambdaExpression projection,
        List<TEntity> rows)
    {
        if (rows.Count == 0)
            return [];

        if (queryCommand.Having is { } having)
        {
            // Constant-projection HAVING carries no aggregate (an aggregate would have selected the
            // global-aggregate mode) and no row reference, so the first filtered row is representative.
            var havingParam = (ParameterExpression)having.Parameters[0];
            var predicate = Expression.Lambda<Func<TEntity, bool>>(having.Body, havingParam).Compile();
            if (!predicate(rows[0]))
                return [];
        }

        var outputs = queryCommand.SelectGroupOutputs;
        if (outputs is not { Count: > 0 })
            throw new NotSupportedException("SelectGroup constant projection requires the analyzed scalar outputs.");

        var entityParam = (ParameterExpression)projection.Parameters[0];
        var outputSelector = CompileVectorSelector<TEntity>(outputs, entityParam);

        var vectors = new List<object?[]>();
        var representatives = new List<TEntity>();
        foreach (var row in rows)
        {
            var vector = outputSelector(row);
            var seen = false;
            for (var i = 0; i < vectors.Count; i++)
            {
                if (VectorEquals(vectors[i], vector))
                {
                    seen = true;
                    break;
                }
            }

            if (seen)
                continue;

            vectors.Add(vector);
            representatives.Add(row);
        }

        var map = Expression.Lambda<Func<TEntity, TResult>>(projection.Body, entityParam).Compile();
        var results = new List<TResult>(representatives.Count);
        foreach (var row in representatives)
            results.Add(map(row));

        return results;
    }

    private static bool PassesHaving<TEntity>(QueryCommand queryCommand, List<TEntity> group)
    {
        if (queryCommand.Having is not { } having)
            return true;

        var havingParam = (ParameterExpression)having.Parameters[0];
        var havingBody = new InMemoryGroupAggregateVisitor<TEntity>(group, havingParam).Visit(having.Body);
        var predicate = Expression.Lambda<Func<TEntity, bool>>(havingBody, havingParam).Compile();
        return predicate(group[0]);
    }

    /// <summary>
    /// Compiles a row-to-<c>object?[]</c> vector selector over <paramref name="entityParam"/>. Each
    /// element expression is re-anchored to that exact parameter instance by type: the raw keys and
    /// outputs come from the projection lambda itself, but a structurally equal expression may carry a
    /// different <see cref="ParameterExpression"/> instance, and <see cref="Expression.Lambda(Expression, ParameterExpression[])"/>
    /// validates by reference.
    /// </summary>
    private static Func<TEntity, object?[]> CompileVectorSelector<TEntity>(
        IReadOnlyList<Expression> elements,
        ParameterExpression entityParam)
    {
        var replacer = new EntityParameterReplacer<TEntity>(entityParam);
        var boxed = new Expression[elements.Count];
        for (var i = 0; i < elements.Count; i++)
            boxed[i] = Expression.Convert(replacer.Visit(elements[i])!, typeof(object));

        var body = Expression.NewArrayInit(typeof(object), boxed);
        return Expression.Lambda<Func<TEntity, object?[]>>(body, entityParam).Compile();
    }

    private static bool VectorEquals(object?[] left, object?[] right)
    {
        if (left.Length != right.Length)
            return false;

        for (var i = 0; i < left.Length; i++)
        {
            if (!Equals(left[i], right[i]))
                return false;
        }

        return true;
    }

    /// <summary>Re-anchors every parameter of type <typeparamref name="TEntity"/> to the projection parameter.</summary>
    private sealed class EntityParameterReplacer<TEntity>(ParameterExpression parameter) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node.Type == typeof(TEntity) ? parameter : base.VisitParameter(node);
    }

    /// <summary>
    /// Fail-closed guard for a <c>SelectGroup</c> expression the in-memory provider is about to execute.
    /// A call is rejected when (1) it carries an effective <see cref="SqlFunctionAttribute"/> (a custom
    /// function has no in-process body), (2) it is an aggregate carrying a trailing
    /// <c>Expression&lt;Func&lt;bool&gt;&gt;</c> FILTER (the fold cannot apply the predicate) or (3) it is
    /// a member of the provider function surface (<see cref="CommonFunctions"/> and its provider
    /// derivations) that is not one of the foldable aggregates in <see cref="InMemoryAggregates"/>. A
    /// provider-only aggregate or window member would otherwise return its marker default instead of
    /// failing, which is exactly the silent "first row" fallback this path must not have.
    /// </summary>
    private sealed class SelectGroupFunctionGuard : ExpressionVisitor
    {
        public static void Reject(Expression expression) => new SelectGroupFunctionGuard().Visit(expression);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var method = node.Method;
            var name = method.Name;

            // The same pinned message the aggregate folder uses: one wording for one limitation.
            if (InMemoryAggregates.HasAggregateFilter(node))
                throw new NotSupportedException("Filtered aggregates are not supported by the in-memory provider.");

            if (ScalarFunctionTranslator.ResolveSqlFunctionAttribute(method) is not null)
                throw new NotSupportedException($"The {name} function is not supported by the in-memory provider.");

            if (IsProviderFunctionSurface(method.DeclaringType) && !InMemoryAggregates.IsAggregate(name))
                throw new NotSupportedException($"The {name} function is not supported by the in-memory provider.");

            return base.VisitMethodCall(node);
        }

        /// <summary>
        /// True for <see cref="CommonFunctions"/> and every provider surface derived from it
        /// (<c>PostgresFunctions</c>, <c>SqlServerFunctions</c>, <c>ClickHouseFunctions</c>,
        /// <c>MySqlFunctions</c>, <c>SqliteFunctions</c>): the SQL marker surfaces whose calls have no
        /// generic CLR body.
        /// </summary>
        private static bool IsProviderFunctionSurface(Type? declaringType)
            => declaringType is not null && declaringType.IsAssignableTo(typeof(CommonFunctions));
    }
}
