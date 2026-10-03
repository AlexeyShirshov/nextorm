using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// #148-B R2.3: descriptor of an in-memory multi-hop reference-navigation chain. The in-memory
/// correlated evaluator cannot nest a correlated subquery inside another (a chain written as
/// subquery-of-subquery is rejected by <see cref="InMemoryCorrelatedEvaluator"/>), so a chain is
/// evaluated directly against the context's registered datasets: every hop matches the previous key
/// against the hop entity's mapped principal/foreign key, and the final entity (or the trailing scalar
/// members read off it) is yielded. The receiver's CLR navigation graph is never consulted.
/// </summary>
internal sealed class InMemoryNavigationChain
{
    /// <summary>The outer-reference index whose runtime value is the first hop's foreign/principal key.</summary>
    internal required int OuterReferenceIndex { get; init; }

    /// <summary>The ordered reference hops (at least two).</summary>
    internal required IReadOnlyList<InMemoryNavigationChainHop> Hops { get; init; }

    /// <summary>Projects the final matched entity to the chain result (identity for a whole reference).</summary>
    internal required Func<object, object?> Selector { get; init; }
}

/// <summary>One hop of an in-memory reference chain.</summary>
internal sealed class InMemoryNavigationChainHop
{
    /// <summary>The entity type matched at this hop.</summary>
    internal required Type EntityType { get; init; }

    /// <summary>The key on the matched entity compared with the previous key.</summary>
    internal required PropertyInfo MatchKey { get; init; }

    /// <summary>The key on this entity that feeds the next hop, or <see langword="null"/> for the last hop.</summary>
    internal PropertyInfo? NextOuterKey { get; init; }
}

/// <summary>
/// Builds the <see cref="InMemoryCorrelatedPlan"/> for an <see cref="InMemoryNavigationChain"/> and
/// walks the registered datasets at execution time. The plan yields zero rows (absent) when any hop's
/// key does not match, so absence propagates through every intermediate hop.
/// </summary>
internal static class InMemoryNavigationChainEvaluator
{
    /// <summary>Builds the chain plan for <paramref name="command"/>.</summary>
    internal static InMemoryCorrelatedPlan Build(InMemoryDataContext context, QueryCommand command, InMemoryNavigationChain chain)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(chain);

        return new InMemoryCorrelatedPlan
        {
            Command = command,
            Enumerate = values => Evaluate(context, chain, values),
            Single = false,
            DefaultOnEmpty = false,
        };
    }

    private static IEnumerator Evaluate(InMemoryDataContext context, InMemoryNavigationChain chain, object?[] values)
    {
        if (values.Length <= chain.OuterReferenceIndex)
            return Array.Empty<object?>().GetEnumerator();

        object? key = values[chain.OuterReferenceIndex];
        object? entity = null;

        for (var i = 0; i < chain.Hops.Count; i++)
        {
            var hop = chain.Hops[i];
            entity = FindMatch(context, hop.EntityType, hop.MatchKey, key);
            if (entity is null)
                return Array.Empty<object?>().GetEnumerator();

            if (i < chain.Hops.Count - 1)
                key = hop.NextOuterKey?.GetValue(entity);
        }

        var result = new object?[] { chain.Selector(entity!) };
        return ((IEnumerable)result).GetEnumerator();
    }

    /// <summary>
    /// #148-B D-R3-5: resolves the principal key that feeds a collection reached through a reference
    /// prefix, or <see langword="null"/> when any prefix hop is absent. The registered datasets are
    /// authoritative and the receiver's CLR navigation graph is never read; a <see langword="null"/> key
    /// never equals a (default) foreign key, so an absent intermediate reference yields an empty
    /// collection instead of a phantom match.
    /// </summary>
    internal static object? ResolveReferencePrefixKey(
        InMemoryDataContext context,
        InMemoryNavigationChainHop[] hops,
        PropertyInfo firstOuterKey,
        PropertyInfo finalKey,
        object? row)
    {
        if (row is null)
            return null;

        object? key = firstOuterKey.GetValue(row);

        for (var i = 0; i < hops.Length; i++)
        {
            var entity = FindMatch(context, hops[i].EntityType, hops[i].MatchKey, key);
            if (entity is null)
                return null;

            if (i == hops.Length - 1)
                return finalKey.GetValue(entity);

            key = hops[i].NextOuterKey!.GetValue(entity);
        }

        return null;
    }

    private static object? FindMatch(InMemoryDataContext context, Type entityType, PropertyInfo key, object? value)
    {
        if (value is null)
            return null;

        if (!context.Data.TryGetValue(entityType, out var data) || data is not IEnumerable rows)
            return null;

        foreach (var row in rows)
        {
            var candidate = key.GetValue(row);
            if (candidate is not null && InMemoryCorrelatedPlan.ValueEquals(candidate, value))
                return row;
        }

        return null;
    }
}
