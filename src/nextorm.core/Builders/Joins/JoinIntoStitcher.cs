using System.Collections.Concurrent;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Builds the runtime <see cref="Projection{T1, T2}"/>-family type used by a <c>JoinInto</c> list
/// command: one item per joined child, preceded by the parent item.
/// </summary>
internal static class JoinIntoProjectionFactory
{
    private const int MaxArity = 8;

    /// <summary>
    /// Creates the closed projection type <c>Projection&lt;TParent, TChild1, …&gt;</c> for the given
    /// parent and children.
    /// </summary>
    /// <param name="parentType">The parent entity type (the first item).</param>
    /// <param name="childTypes">The joined child entity types, in declaration order.</param>
    /// <returns>The closed projection type.</returns>
    /// <exception cref="NotSupportedException">More children than the projection arity supports.</exception>
    public static Type Create(Type parentType, IReadOnlyList<Type> childTypes)
    {
        var arity = childTypes.Count + 1;
        if (arity < 2 || arity > MaxArity)
            throw new NotSupportedException(
                $"JoinInto supports at most {MaxArity - 1} child collections in one query, but {childTypes.Count} were declared.");

        var open = typeof(Projection<,>).Assembly.GetType($"NextORM.Core.Projection`{arity}")
            ?? throw new InvalidOperationException($"Cannot create projection type of arity {arity}.");

        var arguments = new Type[arity];
        arguments[0] = parentType;
        for (var i = 0; i < childTypes.Count; i++)
            arguments[i + 1] = childTypes[i];

        return open.MakeGenericType(arguments);
    }
}

/// <summary>
/// Executes the denormalized <c>JoinInto</c> command once and stitches the rows: parents are
/// deduplicated by their principal key (first occurrence wins) and each child collection is grouped by
/// the foreign key and assigned through the eager-load assignment rules. Reuses the #95 grouping and
/// assignment so the two features share one contract.
/// </summary>
internal static class JoinIntoStitcher
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ItemPropertyCache = new();

    /// <summary>Executes the pair command synchronously and returns the deduplicated parents.</summary>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <param name="builder">The builder carrying the <c>JoinInto</c> declarations.</param>
    /// <param name="params">Positional parameter values.</param>
    /// <returns>The deduplicated parents in first-occurrence order.</returns>
    public static List<TEntity> Execute<TEntity>(EntityBuilder<TEntity> builder, ReadOnlySpan<object?> @params)
    {
        var specs = builder.JoinIntos!;
        var rows = builder.CreateJoinIntoPairCommand().ToObjectList(@params);
        return Stitch(builder, specs, rows);
    }

    /// <summary>Executes the pair command asynchronously and returns the deduplicated parents.</summary>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <param name="builder">The builder carrying the <c>JoinInto</c> declarations.</param>
    /// <param name="params">Positional parameter values.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>A task producing the deduplicated parents in first-occurrence order.</returns>
    public static async Task<List<TEntity>> ExecuteAsync<TEntity>(EntityBuilder<TEntity> builder, object[] @params, CancellationToken cancellationToken)
    {
        var specs = builder.JoinIntos!;
        var rows = await builder.CreateJoinIntoPairCommand().ToObjectListAsync(@params, cancellationToken).ConfigureAwait(false);
        return Stitch(builder, specs, rows);
    }

    internal static List<TEntity> Stitch<TEntity>(EntityBuilder<TEntity> builder, IReadOnlyList<IJoinIntoSpec<TEntity>> specs, List<object?> rows)
    {
        var parents = new List<TEntity>();
        var parentsByKey = new Dictionary<object, TEntity>();
        var perSpec = new List<(TEntity Parent, object? Child)>[specs.Count];
        for (var i = 0; i < specs.Count; i++)
            perSpec[i] = new List<(TEntity, object?)>();

        var properties = ResolveItemProperties(rows);
        if (properties is not null)
        {
            // Parent identity comes from the entity's own key, not from the first JoinInto spec: the
            // specs may declare different parent keys, and a spec key is not the parent's identity.
            var parentIdentity = JoinIntoSpecHelpers.BuildIdentitySelector<TEntity>();

            foreach (var row in rows)
            {
                if (row is null)
                    continue;

                // One consistent null-row rule: a row whose parent item is null is skipped exactly like a
                // null row, so a null parent in the first materialized row cannot crash the stitcher.
                if (properties[0].GetValue(row) is not TEntity parent)
                    continue;

                var key = parentIdentity is not null ? parentIdentity(parent) ?? (object)parent : (object)parent;
                if (!parentsByKey.TryGetValue(key, out var canonical))
                {
                    canonical = parent;
                    parentsByKey[key] = parent;
                    parents.Add(parent);
                }

                for (var i = 0; i < specs.Count; i++)
                    perSpec[i].Add((canonical, properties[i + 1].GetValue(row)));
            }
        }

        // The in-memory provider pages after deduplication; the SQL provider already paged the parent
        // subquery, so the limit is not applied again here.
        if (!builder.DataProvider.NeedMapping && !builder.Paging.IsEmpty)
            parents = ApplyPaging(parents, builder.Paging);

        for (var i = 0; i < specs.Count; i++)
            specs[i].AssignChildren(parents, perSpec[i]);

        return parents;
    }

    /// <summary>
    /// Resolves the <c>ItemN</c> accessors of the denormalized projection from the first non-null row, or
    /// <c>null</c> when every row is null. Null rows are ignored consistently instead of dereferencing the
    /// first row.
    /// </summary>
    internal static PropertyInfo[]? ResolveItemProperties(IReadOnlyList<object?> rows)
    {
        foreach (var row in rows)
        {
            if (row is null)
                continue;

            return ItemPropertyCache.GetOrAdd(row.GetType(), static type =>
            {
                var count = type.GetGenericArguments().Length;
                var result = new PropertyInfo[count];
                for (var i = 0; i < count; i++)
                    result[i] = type.GetProperty($"Item{i + 1}")!;

                return result;
            });
        }

        return null;
    }

    private static List<TEntity> ApplyPaging<TEntity>(List<TEntity> parents, Paging paging)
    {
        IEnumerable<TEntity> result = parents;
        if (paging.Offset > 0)
            result = result.Skip(paging.Offset);
        if (paging.Limit > 0)
            result = result.Take(paging.Limit);

        return result is List<TEntity> list ? list : [.. result];
    }
}
