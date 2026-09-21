using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// LINQ-source axis of the in-memory provider: builds the compiled <c>SelectMany</c>/<c>GroupJoin</c>
/// enumerator delegate and the shared stream/buffer helpers. The reflection-addressed
/// <c>ApplySelectMany</c>/<c>ApplyGroupJoin</c> stay on <see cref="InMemoryDataContext"/> and are invoked
/// through the <c>MethodInfo</c> handles passed in here.
/// <para>
/// The compiled LINQ selectors are cached in the caller-owned per-instance dictionary (the context's
/// <c>_linqSelectorCache</c>), so this type is stateless. Bodies are moved verbatim from
/// <see cref="InMemoryDataContext"/> (F13 follow-up).
/// </para>
/// </summary>
internal static class InMemoryLinqSource
{
    public static CreateEnumeratorDelegate<TResult> BuildLinqSourceDelegate<TResult>(
        LinqSourceExpression source,
        InMemoryDataContext context,
        MethodInfo miApplySelectMany,
        MethodInfo miApplyGroupJoin,
        MethodInfo miCreateEnumeratorAdapter)
    {
        // SelectMany/GroupJoin produce an IAsyncEnumerator<ResultType> from the outer (and, for
        // GroupJoin, inner) command. The downstream pipeline (WHERE/map/DISTINCT) is then exactly the
        // same as for a subquery source, so it is reused through CreateEnumeratorAdapter.
        var resultType = source.ResultType;
        var apply = source.IsGroupJoin
            ? miApplyGroupJoin.MakeGenericMethod(source.OuterType, source.InnerType!, source.KeyType!, resultType)
            : miApplySelectMany.MakeGenericMethod(source.OuterType, source.CollectionType!, resultType);
        var createAdapter = miCreateEnumeratorAdapter.MakeGenericMethod(typeof(TResult), resultType);

        return (queryCommand, cacheEntry, @params, cancellationToken) =>
        {
            var flattened = apply.Invoke(context, [source, @params, cancellationToken])!;
            return (IAsyncEnumerator<TResult>)createAdapter.Invoke(context, [queryCommand, cacheEntry, flattened])!;
        };
    }

    /// <summary>
    /// Applies <c>SelectMany</c> over the prepared outer command: for every outer row the collection
    /// selector is evaluated and each element is yielded (optionally projected with the result
    /// selector). The result is buffered so the enumerator supports both sync and async terminals.
    /// </summary>
    public static IAsyncEnumerator<TResult> ApplySelectMany<TOuter, TCollection, TResult>(InMemoryDataContext context, LinqSourceExpression source, object[]? @params, CancellationToken cancellationToken)
    {
        var outer = InMemoryQueryBuilder.CreateAsyncEnumerator(context, (QueryCommand<TOuter>)source.OuterCommand, @params, cancellationToken);
        var collectionSelector = GetCompiledLinqSelector<Func<TOuter, IEnumerable<TCollection>>>(source.CollectionSelector!, source.OuterCommand, context.LinqSelectorCache);
        var resultSelector = source.ResultSelector is null
            ? null
            : GetCompiledLinqSelector<Func<TOuter, TCollection, TResult>>(source.ResultSelector, source.OuterCommand, context.LinqSelectorCache);

        var rows = new List<TResult>();
        Enumerate(outer, current =>
        {
            foreach (var item in collectionSelector(current))
                rows.Add(resultSelector is null ? (TResult)(object)item! : resultSelector(current, item));
        });

        return new InMemoryListEnumerator<TResult>(rows, false);
    }

    /// <summary>
    /// Applies <c>GroupJoin</c>: the inner command is materialised once into a key lookup, the outer
    /// rows are read and each is projected with the matching inner rows (empty for no match). The
    /// lookup keeps the operator O(outer + inner) rather than O(outer × inner).
    /// </summary>
    public static IAsyncEnumerator<TResult> ApplyGroupJoin<TOuter, TInner, TKey, TResult>(InMemoryDataContext context, LinqSourceExpression source, object[]? @params, CancellationToken cancellationToken)
    {
        var outerKeySelector = GetCompiledLinqSelector<Func<TOuter, TKey>>(source.OuterKeySelector!, source.OuterCommand, context.LinqSelectorCache);
        var innerKeySelector = GetCompiledLinqSelector<Func<TInner, TKey>>(source.InnerKeySelector!, source.InnerCommand!, context.LinqSelectorCache);
        var resultSelector = GetCompiledLinqSelector<Func<TOuter, IEnumerable<TInner>, TResult>>(source.ResultSelector!, source.OuterCommand, context.LinqSelectorCache);

        var innerRows = new List<TInner>();
        Materialize(InMemoryQueryBuilder.CreateAsyncEnumerator(context, (QueryCommand<TInner>)source.InnerCommand!, @params, cancellationToken), innerRows);

        // Null keys cannot be dictionary keys; they are collected separately (a null outer key matches
        // only null inner keys, mirroring EqualityComparer<TKey>.Default used by LINQ's GroupJoin).
        // CS8714: TKey is unconstrained, but null keys never reach the dictionary.
#pragma warning disable CS8714
        var lookup = new Dictionary<TKey, List<TInner>>();
#pragma warning restore CS8714
        List<TInner>? nullKeys = null;
        foreach (var row in innerRows)
        {
            var key = innerKeySelector(row);
            if (key is null)
            {
                (nullKeys ??= []).Add(row);
                continue;
            }

            if (!lookup.TryGetValue(key, out var list))
                lookup[key] = list = [];
            list.Add(row);
        }

        var outer = InMemoryQueryBuilder.CreateAsyncEnumerator(context, (QueryCommand<TOuter>)source.OuterCommand, @params, cancellationToken);
        var rows = new List<TResult>();
        Enumerate(outer, current =>
        {
            var key = outerKeySelector(current);
            List<TInner>? group;
            if (key is null)
                group = nullKeys;
            else if (!lookup.TryGetValue(key, out group))
                group = null;

            rows.Add(resultSelector(current, group ?? (IEnumerable<TInner>)Array.Empty<TInner>()));
        });

        return new InMemoryListEnumerator<TResult>(rows, false);
    }

    /// <summary>
    /// Compiles a <c>SelectMany</c>/<c>GroupJoin</c> selector once and caches it by the expression's
    /// structural (closure-aware) key. Expression compilation is orders of magnitude more expensive
    /// than the per-row work, so the operator must not recompile on every call.
    /// </summary>
    public static TDelegate GetCompiledLinqSelector<TDelegate>(LambdaExpression selector, QueryCommand queryCommand, IDictionary<ExpressionKey, Delegate> linqSelectorCache) where TDelegate : Delegate
    {
        var key = new ExpressionKey(selector, queryCommand);
        if (linqSelectorCache.TryGetValue(key, out var cached))
            return (TDelegate)cached;

        var compiled = (TDelegate)selector.Compile();
        linqSelectorCache[key] = compiled;
        return compiled;
    }

    /// <summary>
    /// Streams an enumerator through <paramref name="body"/>. Prefers the synchronous view when the
    /// source provides one; an async-only source is drained by blocking, matching
    /// <see cref="Materialize{T}"/>.
    /// </summary>
    public static void Enumerate<T>(IAsyncEnumerator<T> enumerator, Action<T> body)
    {
        try
        {
            if (enumerator is IEnumerator<T> sync)
            {
                while (sync.MoveNext())
                    body(sync.Current);
            }
            else
            {
                while (enumerator.MoveNextAsync().GetAwaiter().GetResult())
                    body(enumerator.Current);
            }
        }
        finally
        {
            enumerator.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Buffers an enumerator. Prefers the synchronous view when the source provides one (buffered
    /// in-memory rows do) so no thread is blocked; an async-only source is drained by blocking, which
    /// matches the provider's other buffered operators (grouping, set operations).
    /// </summary>
    public static void Materialize<T>(IAsyncEnumerator<T> enumerator, List<T> rows)
    {
        try
        {
            if (enumerator is IEnumerator<T> sync)
            {
                while (sync.MoveNext())
                    rows.Add(sync.Current);
            }
            else
            {
                while (enumerator.MoveNextAsync().GetAwaiter().GetResult())
                    rows.Add(enumerator.Current);
            }
        }
        finally
        {
            enumerator.DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
