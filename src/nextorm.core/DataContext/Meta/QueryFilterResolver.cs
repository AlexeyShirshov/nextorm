namespace NextORM.Core;

/// <summary>
/// Resolves the global query filters active for an entity type under a selective
/// <see cref="QueryFilterScope"/>. Shared by the query preparer (which injects the predicates into
/// SELECT conditions) and the DML validator (which checks written values against them), so both agree
/// on exactly which filters apply.
/// </summary>
internal static class QueryFilterResolver
{
    /// <summary>
    /// Returns the filters declared for <paramref name="entityType"/> that the scope does not disable,
    /// in declaration order. An absent entity type or an all-disabling scope yields an empty list.
    /// </summary>
    /// <param name="entityType">The entity type whose filters are resolved, or <see langword="null"/>.</param>
    /// <param name="scope">The scope of filters disabled for the statement.</param>
    /// <returns>The active filters, in declaration order.</returns>
    public static IReadOnlyList<IQueryFilterMetadata> GetFilters(Type? entityType, QueryFilterScope scope)
    {
        if (entityType is null || scope.All)
            return Array.Empty<IQueryFilterMetadata>();

        if (!DataContextCache.Metadata.TryGetValue(entityType, out var metadata))
            return Array.Empty<IQueryFilterMetadata>();

        var declared = metadata.Filters;
        if (declared.Count == 0)
            return declared;

        EnsureSingleForm(declared, entityType);

        if (scope.IsEmpty)
            return declared;

        // Selective scope: drop the filters it disables, keeping declaration order. The list is only
        // allocated once the first filter is actually dropped; an equal scope is not part of the plan
        // key, the injected condition it produces is.
        List<IQueryFilterMetadata>? kept = null;
        for (var (i, cnt) = (0, declared.Count); i < cnt; i++)
        {
            var filter = declared[i];
            if (scope.Ignores(entityType, filter.Key))
            {
                if (kept is null)
                {
                    kept = new List<IQueryFilterMetadata>(declared.Count - 1);
                    for (var j = 0; j < i; j++)
                        kept.Add(declared[j]);
                }

                continue;
            }

            kept?.Add(filter);
        }

        return kept ?? declared;
    }

    // A filter must declare exactly one form: a predicate Lambda or a builder-function Func. The built-in
    // registrations always set one, but a custom IQueryFilterMetadata implementation can report both; the
    // consumers prefer Lambda, so the Func would be silently ignored. Reject the declaration instead.
    private static void EnsureSingleForm(IReadOnlyList<IQueryFilterMetadata> filters, Type entityType)
    {
        for (var i = 0; i < filters.Count; i++)
        {
            var filter = filters[i];
            if (filter.Lambda is not null && filter.Func is not null)
                throw new NotSupportedException(
                    $"The query filter '{filter.Key}' registered for {entityType.Name} reports both a predicate Lambda and a builder-function Func; a filter must declare exactly one form. Return one of them as null.");
        }
    }
}
