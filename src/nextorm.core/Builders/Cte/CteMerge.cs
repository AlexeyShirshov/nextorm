namespace NextORM.Core;

/// <summary>
/// Merges the common table expressions carried by the two sides of a join, so a statement built over
/// the join (a <c>SELECT</c> or a multi-table <c>UPDATE</c>/<c>DELETE</c>) can hoist every declaration
/// it references. Only a CTE declared on the joined side is lost by the join itself (the joined source
/// is reduced to its table name), which is the case a multi-table mutation previously could not see.
/// </summary>
internal static class CteMerge
{
    /// <summary>
    /// Concatenates <paramref name="left"/> and <paramref name="right"/> declarations, left-first. A
    /// declaration already present by reference is kept once (the two sides of a self-join over one
    /// scope share the same list); two distinct declarations under the same name are rejected, because
    /// a single <c>WITH</c> cannot bind one name to two definitions.
    /// </summary>
    internal static IReadOnlyList<CteDefinition>? Merge(IReadOnlyList<CteDefinition>? left, IReadOnlyList<CteDefinition>? right)
    {
        if (left is not { Count: > 0 })
            return right is { Count: > 0 } ? right : null;

        if (right is not { Count: > 0 })
            return left;

        List<CteDefinition>? merged = null;
        for (var i = 0; i < right.Count; i++)
        {
            var cte = right[i];
            if (Contains(merged ?? left, cte))
                continue;

            merged ??= [.. left];
            merged.Add(cte);
        }

        return merged ?? left;
    }

    private static bool Contains(IReadOnlyList<CteDefinition> ctes, CteDefinition candidate)
    {
        // Reference equality wins over the name check: a self-join over one scope passes the very same
        // definitions on both sides, including any duplicate name the scope itself carries.
        for (var i = 0; i < ctes.Count; i++)
        {
            if (ReferenceEquals(ctes[i], candidate))
                return true;
        }

        for (var i = 0; i < ctes.Count; i++)
        {
            var cte = ctes[i];
            if (string.Equals(cte.Name, candidate.Name, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"The common table expression '{candidate.Name}' is declared on both sides of the join; a single WITH cannot bind two definitions to one name. Rename one of them.");
        }

        return false;
    }
}
