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
    /// Creates the closed projection type <c>Projection&lt;TParent, …&gt;</c> for the given parent and the
    /// per-declaration item types, flattened in declaration order.
    /// </summary>
    /// <param name="parentType">The parent entity type (the first item).</param>
    /// <param name="itemTypesByDeclaration">
    /// The projection item types each declaration contributes, in declaration order. A one-to-many or
    /// one-to-one declaration contributes one item (the child); a many-to-many declaration contributes
    /// two (the link and the child).
    /// </param>
    /// <returns>The closed projection type.</returns>
    /// <exception cref="NotSupportedException">More items than the projection arity supports.</exception>
    public static Type Create(Type parentType, IReadOnlyList<IReadOnlyList<Type>> itemTypesByDeclaration)
    {
        var childCount = 0;
        for (var i = 0; i < itemTypesByDeclaration.Count; i++)
            childCount += itemTypesByDeclaration[i].Count;

        var arity = childCount + 1;
        if (arity < 2 || arity > MaxArity)
            throw new NotSupportedException(
                $"JoinInto supports at most {MaxArity - 1} child collections in one query, but {childCount} were declared.");

        var open = typeof(Projection<,>).Assembly.GetType($"NextORM.Core.Projection`{arity}")
            ?? throw new InvalidOperationException($"Cannot create projection type of arity {arity}.");

        var arguments = new Type[arity];
        arguments[0] = parentType;
        var index = 1;
        for (var i = 0; i < itemTypesByDeclaration.Count; i++)
        {
            var itemTypes = itemTypesByDeclaration[i];
            for (var j = 0; j < itemTypes.Count; j++)
                arguments[index++] = itemTypes[j];
        }

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

    /// <summary>
    /// Deduplicates the parents of the denormalized <paramref name="rows"/> and assigns each child
    /// collection. The parent identity is the entity's mapped key, or the row reference when the type has
    /// no key and <paramref name="requireMappedParentKey"/> is <c>false</c>.
    /// </summary>
    /// <param name="builder">The builder whose paging applies to the deduplicated parents.</param>
    /// <param name="specs">The stitching specifications, in declaration order.</param>
    /// <param name="rows">The denormalized parent/child pairs in result order.</param>
    /// <param name="requireMappedParentKey">
    /// Whether the parent type must declare a mapped key. <c>true</c> for single-query eager loading, which
    /// has no split fallback and would otherwise emit one parent per denormalized row; <c>false</c> for
    /// plain <c>JoinInto</c>, which keeps the reference-identity fallback.
    /// </param>
    /// <returns>The deduplicated parents in first-occurrence order.</returns>
    internal static List<TEntity> Stitch<TEntity>(EntityBuilder<TEntity> builder, IReadOnlyList<IJoinIntoSpec<TEntity>> specs, List<object?> rows, bool requireMappedParentKey = false)
    {
        var parents = new List<TEntity>();
        var parentsByKey = new Dictionary<object, TEntity>();
        var perSpec = new List<(TEntity Parent, object? Child)>?[specs.Count];
        var perManyToMany = new List<JoinIntoManyToManyRow<TEntity>>?[specs.Count];
        for (var i = 0; i < specs.Count; i++)
        {
            if (specs[i] is IJoinIntoManyToManySpec)
                perManyToMany[i] = [];
            else
                perSpec[i] = [];
        }

        // Parent identity comes from the entity's own key, not from the first JoinInto spec: the specs may
        // declare different parent keys, and a spec key is not the parent's identity. Resolved before the
        // row scan so a keyless single-query parent is rejected even when the result is empty.
        var parentIdentity = JoinIntoSpecHelpers.BuildIdentitySelector<TEntity>();

        // Single-query loading materializes one parent instance per denormalized row, so without a mapped
        // key the only available identity is the row's reference and the same parent would be emitted once
        // per child. Require a key there rather than silently returning duplicates. Plain JoinInto keeps
        // the documented reference-identity fallback (#105), so the same parent instance repeats.
        if (requireMappedParentKey && parentIdentity is null)
            throw new NotSupportedException(
                $"Single-query eager loading requires the parent type '{typeof(TEntity).Name}' to declare a " +
                "mapped key so the denormalized rows can be deduplicated. Configure a key (for example with " +
                "HasKey), or use split-query loading (LoadWith without EagerLoadMode.SingleQuery), which does not require one.");

        // Each declaration contributes one or more projection items (a many-to-many declaration
        // contributes a link item followed by the child). The child assigned per declaration is the
        // last item of its slice, and the link is the first; the offsets locate that slice in the
        // flattened projection. The many-to-many slice is path-dependent: SQL projects the derived
        // JoinIntoLink, in memory the junction entity.
        var isSql = builder.DataProvider.NeedMapping;
        var itemTypesByDeclaration = new IReadOnlyList<Type>[specs.Count];
        for (var i = 0; i < specs.Count; i++)
            itemTypesByDeclaration[i] = specs[i] is IJoinIntoManyToManySpec manyToMany
                ? manyToMany.GetItemTypes(isSql)
                : specs[i].ItemTypes;

        var projectionType = JoinIntoProjectionFactory.Create(typeof(TEntity), itemTypesByDeclaration);

        var childProperties = new PropertyInfo[specs.Count];
        var linkProperties = new PropertyInfo?[specs.Count];
        var offset = 1;
        for (var i = 0; i < specs.Count; i++)
        {
            var itemCount = itemTypesByDeclaration[i].Count;
            if (itemCount == 0)
                throw new InvalidOperationException(
                    $"The JoinInto declaration for '{specs[i].ChildEntityType.Name}' contributes no projection items.");

            if (specs[i] is IJoinIntoManyToManySpec)
                linkProperties[i] = ResolveItemPropertyNumber(projectionType, offset + 1);

            childProperties[i] = ResolveItemProperty(i, projectionType, offset, itemCount);
            offset += itemCount;
        }

        var properties = ResolveItemProperties(rows);
        if (properties is not null)
        {
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
                {
                    if (specs[i] is IJoinIntoManyToManySpec)
                    {
                        perManyToMany[i]!.Add(new JoinIntoManyToManyRow<TEntity>(
                            canonical,
                            linkProperties[i]!.GetValue(row),
                            childProperties[i].GetValue(row)));
                    }
                    else
                    {
                        perSpec[i]!.Add((canonical, childProperties[i].GetValue(row)));
                    }
                }
            }
        }

        // The in-memory provider pages after deduplication; the SQL provider already paged the parent
        // subquery, so the limit is not applied again here.
        if (!isSql && !builder.Paging.IsEmpty)
            parents = ApplyPaging(parents, builder.Paging);

        for (var i = 0; i < specs.Count; i++)
        {
            if (specs[i] is IJoinIntoManyToManySpec manyToMany)
                manyToMany.AssignChildrenFromLinks(parents, perManyToMany[i]!);
            else
                specs[i].AssignChildren(parents, perSpec[i]!);
        }

        return parents;
    }

    /// <summary>
    /// Resolves the parameterless <c>ItemN</c> accessor of the denormalized projection that carries the
    /// child item of a declaration: the last item of the declaration's flattened slice.
    /// </summary>
    /// <param name="specIndex">The declaration index, used for diagnostics.</param>
    /// <param name="projectionType">The closed denormalized projection type.</param>
    /// <param name="offset">The 1-based item number immediately before the declaration's slice.</param>
    /// <param name="itemCount">The number of projection items the declaration contributes.</param>
    /// <returns>The <c>ItemN</c> property that holds the joined child.</returns>
    private static PropertyInfo ResolveItemProperty(int specIndex, Type projectionType, int offset, int itemCount)
        => ResolveItemPropertyNumber(projectionType, offset + itemCount)
            ?? throw new InvalidOperationException(
                $"The denormalized projection for the JoinInto declaration {specIndex} has no 'Item{offset + itemCount}' property.");

    /// <summary>Resolves the 1-based <c>ItemN</c> accessor of the denormalized projection, or <c>null</c> when absent.</summary>
    private static PropertyInfo? ResolveItemPropertyNumber(Type projectionType, int itemNumber)
        => projectionType.GetProperty($"Item{itemNumber}");

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
