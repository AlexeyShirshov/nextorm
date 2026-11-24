using System.Collections.Generic;

namespace NextORM.Core;

public partial class QueryCommand
{
    /// <summary>
    /// Whether this command reads a deferred temporary table or view directly, in its joins, in its set operation or
    /// in one of its referenced queries (for example the subquery of an <c>EXISTS</c> built by
    /// <c>Any</c>). Allocation-free, so the query-preparation hot path can cheaply rule out the batch
    /// path before collecting the sources.
    /// </summary>
    /// <returns><see langword="true"/> when at least one temporary source is reachable.</returns>
    internal bool HasTemporarySource()
    {
        if (_from is not null && FromHasTemporaryTableSource(_from))
            return true;

        if (_union is not null && _union.HasTemporarySource())
            return true;

        if (_referencedQueries is { Count: > 0 } referenced)
        {
            for (var i = 0; i < referenced.Count; i++)
            {
                if (referenced[i].HasTemporarySource())
                    return true;
            }
        }

        if (_ctes is { Count: > 0 } ctes)
        {
            for (var i = 0; i < ctes.Count; i++)
            {
                if (ctes[i].Query.HasTemporarySource())
                    return true;
            }
        }

        if (_joins is { Length: > 0 } joins)
        {
            for (var i = 0; i < joins.Length; i++)
            {
                if (FromHasTemporaryTableSource(joins[i].From))
                    return true;
            }
        }

        return false;
    }

    private static bool FromHasTemporaryTableSource(FromExpression from)
    {
        if (from.TemporarySource is not null)
            return true;

        if (from.SubQuery is { } subQuery && subQuery.HasTemporarySource())
            return true;

        return from.Pivot is { } pivot && FromHasTemporaryTableSource(pivot.Inner);
    }

    /// <summary>
    /// Collects the deferred temporary sources this command reads, so one creation step can be
    /// emitted per source before the command runs. The walk covers the command's own <c>FROM</c>, its
    /// joins, its set operation and its referenced queries (for example the subquery of an
    /// <c>EXISTS</c> built by <c>Any</c>), so a temp table read inside a predicate is materialised too.
    /// </summary>
    /// <param name="result">The list the distinct sources are appended to, in discovery order.</param>
    internal void CollectTemporarySources(List<ITemporarySource> result)
    {
        CollectTempTableSources(result, new HashSet<QueryCommand>(ReferenceEqualityComparer.Instance));
    }

    private void CollectTempTableSources(List<ITemporarySource> result, HashSet<QueryCommand> visited)
    {
        if (!visited.Add(this))
            return;

        CollectFrom(_from, result, visited);

        if (_union is not null)
            _union.CollectTempTableSources(result, visited);

        if (_referencedQueries is { Count: > 0 } referenced)
        {
            for (var i = 0; i < referenced.Count; i++)
                referenced[i].CollectTempTableSources(result, visited);
        }

        if (_ctes is { Count: > 0 } ctes)
        {
            for (var i = 0; i < ctes.Count; i++)
                ctes[i].Query.CollectTempTableSources(result, visited);
        }

        if (_joins is { Length: > 0 } joins)
        {
            for (var i = 0; i < joins.Length; i++)
                CollectFrom(joins[i].From, result, visited);
        }
    }

    private static void CollectFrom(FromExpression? from, List<ITemporarySource> result, HashSet<QueryCommand> visited)
    {
        if (from is null)
            return;

        if (from.TemporarySource is { } temporarySource)
        {
            // Dependencies first: a temp source may itself read another temporary object, which must exist
            // before this one is created. Stops before adding when the name is already collected, so a
            // repeated source does not duplicate its step.
            temporarySource.Source.CollectTempTableSources(result, visited);

            var exists = false;
            for (var i = 0; i < result.Count; i++)
            {
                if (string.Equals(result[i].Name, temporarySource.Name, StringComparison.Ordinal))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                result.Add(temporarySource);
        }

        if (from.SubQuery is { } subQuery)
            subQuery.CollectTempTableSources(result, visited);

        if (from.Pivot is { } pivot)
            CollectFrom(pivot.Inner, result, visited);
    }
}
