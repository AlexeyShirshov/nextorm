namespace NextORM.Core;

/// <summary>
/// Collects the entity types one statement reads or writes (its FROM source, joins, subqueries, CTEs
/// and union branches), so routing can verify that the whole statement belongs to one topology. A
/// statement is a single server side operation: joining two hosts in one SQL text is not something the
/// engine can execute, so a command whose sources resolve to different topologies is rejected instead
/// of silently running half of it on the wrong server.
/// </summary>
internal static class CommandRouteSources
{
    /// <summary>
    /// Collects the distinct entity types reachable from <paramref name="command"/>. The walk is
    /// allocation-light and runs once per prepared command, not per execution.
    /// </summary>
    /// <param name="command">The command about to be prepared.</param>
    /// <returns>The entity types in first-seen order; empty when the command reads no entity source.</returns>
    internal static Type[] Collect(QueryCommand command)
    {
        var sources = new List<Type>(4);
        var known = new HashSet<Type>();
        var visited = new HashSet<QueryCommand>(ReferenceEqualityComparer.Instance);

        Walk(command);

        return sources.Count == 0 ? [] : sources.ToArray();

        void Add(Type? entityType)
        {
            if (entityType is null)
                return;

            // A join is carried as Projection<Item1, Item2, ...>: the routed types are the items, not
            // the accumulated projection itself (which no rule can name).
            if (typeof(IProjection).IsAssignableFrom(entityType))
            {
                foreach (var item in entityType.GetGenericArguments())
                    Add(item);

                return;
            }

            if (known.Add(entityType))
                sources.Add(entityType);
        }

        void Walk(QueryCommand current)
        {
            if (!visited.Add(current))
                return;

            Add(current.RouteEntityType);

            WalkFrom(current.From);

            if (current.Joins is { Length: > 0 } joins)
            {
                for (var i = 0; i < joins.Length; i++)
                    WalkFrom(joins[i].From);
            }

            if (current.UnionQuery is { } union)
                Walk(union);

            if (current.ReferencedQueries is { Count: > 0 } referenced)
            {
                for (var i = 0; i < referenced.Count; i++)
                    Walk(referenced[i]);
            }

            if (current.Ctes is { Count: > 0 } ctes)
            {
                for (var i = 0; i < ctes.Count; i++)
                {
                    var declaration = ctes[i];
                    if (declaration.Mutation is { } mutation)
                    {
                        // A data-modifying CTE writes one entity type and may read another; both are part
                        // of the statement, so both must agree on the topology.
                        Add(mutation.Command.EntityType);
                    }

                    Walk(declaration.Mutation?.Source ?? declaration.Query);
                }
            }
        }

        void WalkFrom(FromExpression? from)
        {
            if (from is null)
                return;

            Add(from.SourceType);
            Add(from.SourceBinding?.EntityType);

            if (from.SubQuery is { } subQuery)
                Walk(subQuery);

            if (from.ColumnShape is { } columnShape)
                Walk(columnShape);

            if (from.Pivot is { } pivot)
                WalkFrom(pivot.Inner);
        }
    }
}
