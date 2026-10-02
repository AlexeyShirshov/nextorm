using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Flattens the common table expressions carried by a command into a single ordered declaration list.
/// A CTE body built as <c>With(...).From(...)</c> carries a nested <c>WITH</c> of its own; rendering it
/// literally (<c>with o as (with i as (...) ...) ...</c>) is valid on PostgreSQL, SQLite and
/// MySQL/MariaDB but rejected by SQL Server, whose T-SQL forbids <c>WITH</c> inside a derived table.
/// The hoister flattens the declaration tree and emits each dependency before the CTE that consumes
/// it: nested declarations come first structurally, and a declaration that references a sibling by
/// name is ordered after that sibling even when the sibling was declared later. Independent
/// declarations keep their original relative order. A repeated declaration (the same
/// <see cref="CteDefinition"/> instance) is kept once; two different instances under the same name are
/// rejected (a single <c>WITH</c> cannot bind one name to two definitions).
/// </summary>
internal static class CteHoister
{
    /// <summary>
    /// Returns the declarations of <paramref name="ctes"/> flattened into one list. The original list
    /// is returned unchanged when it is already flat (no nested declaration and no duplicate); a new
    /// list is allocated only when the tree actually needs reordering or deduplication.
    /// </summary>
    /// <param name="ctes">The declarations of a command, or <c>null</c>.</param>
    /// <returns>The flattened declarations, or the original value when nothing had to change.</returns>
    /// <exception cref="InvalidOperationException">
    /// Two different declarations share a name, or the declaration tree contains a cycle.
    /// </exception>
    internal static IReadOnlyList<CteDefinition>? Hoist(IReadOnlyList<CteDefinition>? ctes)
    {
        if (ctes is not { Count: > 0 })
            return ctes;

        var walker = new Walker();
        walker.Visit(ctes);
        var flat = walker.Flat;

        if (flat.Count == ctes.Count)
        {
            var identical = true;
            for (var i = 0; i < flat.Count; i++)
            {
                if (!ReferenceEquals(flat[i], ctes[i]))
                {
                    identical = false;
                    break;
                }
            }

            if (identical)
                return ctes;
        }

        return flat;
    }

    /// <summary>
    /// Throws when a command renderable from <paramref name="root"/> still carries a declaration that
    /// was not hoisted. Such a declaration (a CTE declared inside a derived-table subquery, a correlated
    /// reference or a set-operation branch) cannot be moved to the top-level <c>WITH</c> without
    /// changing what the statement renders, so the command fails before emitting non-portable SQL.
    /// </summary>
    /// <param name="root">The command whose prepared tree is checked.</param>
    /// <param name="hoisted">The flattened declarations the root will render.</param>
    /// <exception cref="InvalidOperationException">A nested declaration is not part of the hoisted set.</exception>
    internal static void EnsureNoUnhoistedCtes(QueryCommand root, IReadOnlyCollection<CteDefinition> hoisted)
    {
        var known = new HashSet<CteDefinition>(hoisted, ReferenceEqualityComparer.Instance);
        var visited = new HashSet<QueryCommand>(ReferenceEqualityComparer.Instance);

        Walk(root);

        void Walk(QueryCommand cmd)
        {
            if (!visited.Add(cmd))
                return;

            if (cmd.Ctes is { Count: > 0 } ctes)
            {
                for (var i = 0; i < ctes.Count; i++)
                {
                    if (!known.Contains(ctes[i]))
                    {
                        throw new InvalidOperationException(
                            $"The common table expression '{ctes[i].Name}' is declared inside a nested source and cannot be hoisted into the top-level WITH. Declare it as a top-level CTE instead.");
                    }
                }

                for (var i = 0; i < ctes.Count; i++)
                {
                    var cte = ctes[i];
                    if (cte.Mutation?.Source is { } mutationSource)
                        Walk(mutationSource);
                    else
                        Walk(cte.Query);
                }
            }

            WalkFrom(cmd.From);

            if (cmd.Joins is { Length: > 0 } joins)
            {
                for (var i = 0; i < joins.Length; i++)
                    WalkFrom(joins[i].From);
            }

            if (cmd.UnionQuery is { } union)
                Walk(union);

            if (cmd.ReferencedQueries is { Count: > 0 } referenced)
            {
                for (var i = 0; i < referenced.Count; i++)
                    Walk(referenced[i]);
            }
        }

        void WalkFrom(FromExpression? from)
        {
            if (from is null)
                return;

            if (from.SubQuery is { } subQuery)
                Walk(subQuery);

            // A projection-source marker (a typed/data-modifying CTE read) carries the defining command
            // as ColumnShape; its own declarations must be checked too, or a nested WITH could survive
            // the unhoisted-declaration guard.
            if (from.ColumnShape is { } columnShape)
                Walk(columnShape);

            if (from.Pivot is { } pivot)
                WalkFrom(pivot.Inner);
        }
    }

    private sealed class Walker
    {
        private readonly List<CteDefinition> _flat = [];
        private readonly HashSet<CteDefinition> _seen = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, CteDefinition> _byName = new(StringComparer.Ordinal);
        private readonly HashSet<QueryCommand> _stack = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<CteDefinition, VisitState> _state = new(ReferenceEqualityComparer.Instance);

        internal IReadOnlyList<CteDefinition> Flat => _flat;

        internal void Visit(IReadOnlyList<CteDefinition> list)
        {
            // Phase 1: collect every declaration. A body that itself carries declarations is visited
            // first so a structurally nested dependency is listed before its consumer; duplicates (same
            // instance) are dropped and a conflicting second definition under one name is rejected.
            Collect(list);

            // Phase 2: name-based dependency order. A declaration may reference a sibling declared
            // later (the fluent API builds the body before the referenced CTE is declared), so a plain
            // post-order is not enough. Reorder stably over the collected declarations: every
            // dependency precedes its consumer and independent declarations keep their original order.
            Reorder();
        }

        private void Collect(IReadOnlyList<CteDefinition> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var cte = list[i];

                // A data-modifying CTE's dependencies live on the INSERT source, an ordinary CTE's on
                // its body query; either way they must be declared before the CTE that consumes them.
                var body = cte.Mutation?.Source ?? cte.Query;
                if (body.Ctes is { Count: > 0 } nested)
                {
                    if (!_stack.Add(body))
                        throw new InvalidOperationException(
                            $"The common table expression '{cte.Name}' is part of a declaration cycle and cannot be hoisted.");

                    Collect(nested);
                    _stack.Remove(body);
                }

                Add(cte);
            }
        }

        private void Add(CteDefinition cte)
        {
            if (!_seen.Add(cte))
                return;

            if (_byName.TryGetValue(cte.Name, out _))
            {
                throw new InvalidOperationException(
                    $"The common table expression '{cte.Name}' is declared more than once with different definitions; a single WITH cannot bind two definitions to one name. Rename one of them.");
            }

            _byName.Add(cte.Name, cte);
            _flat.Add(cte);
        }

        /// <summary>
        /// Stable topological sort of <see cref="_flat"/> over the CTE name references found in each
        /// declaration's body. A declaration is appended only after every declaration it references, so
        /// the DFS (run in the collected order) keeps independent declarations in place and moves a
        /// forward-referenced sibling ahead of its consumer.
        /// </summary>
        private void Reorder()
        {
            if (_flat.Count < 2)
                return;

            var ordered = new List<CteDefinition>(_flat.Count);
            for (var i = 0; i < _flat.Count; i++)
                VisitDeclaration(_flat[i], ordered);

            for (var i = 0; i < ordered.Count; i++)
            {
                if (ReferenceEquals(ordered[i], _flat[i]))
                    continue;

                _flat.Clear();
                _flat.AddRange(ordered);
                return;
            }
        }

        private void VisitDeclaration(CteDefinition cte, List<CteDefinition> ordered)
        {
            if (_state.TryGetValue(cte, out var state))
            {
                if (state == VisitState.Visiting)
                    throw new InvalidOperationException(
                        $"The common table expression '{cte.Name}' is part of a declaration cycle and cannot be hoisted.");
                return;
            }

            _state[cte] = VisitState.Visiting;

            foreach (var referenced in ReferencedNames(cte))
            {
                if (_byName.TryGetValue(referenced, out var dependency) && !ReferenceEquals(dependency, cte))
                    VisitDeclaration(dependency, ordered);
            }

            _state[cte] = VisitState.Done;
            ordered.Add(cte);
        }

        /// <summary>
        /// Collects every table/CTE name referenced anywhere in a declaration's body (including nested
        /// subqueries, joins, set-operation branches, correlated subqueries and a data-modifying
        /// INSERT source).
        /// </summary>
        private HashSet<string> ReferencedNames(CteDefinition cte)
            => CollectReferencedNames(cte.Mutation?.Source ?? cte.Query, _byName.Keys);

        internal static HashSet<string> CollectReferencedNames(QueryCommand body, IReadOnlyCollection<string>? knownNames = null)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<QueryCommand>(ReferenceEqualityComparer.Instance);

            Walk(body);

            return names;

            void Walk(QueryCommand cmd)
            {
                if (!visited.Add(cmd))
                    return;

                WalkFrom(cmd, cmd.From);

                if (cmd.Joins is { Length: > 0 } joins)
                {
                    for (var i = 0; i < joins.Length; i++)
                        WalkFrom(cmd, joins[i].From);
                }

                if (cmd.UnionQuery is { } union)
                    Walk(union);

                if (cmd.ReferencedQueries is { Count: > 0 } referenced)
                {
                    for (var i = 0; i < referenced.Count; i++)
                        Walk(referenced[i]);
                }

                if (cmd.Ctes is { Count: > 0 } nested)
                {
                    for (var i = 0; i < nested.Count; i++)
                    {
                        var declaration = nested[i];
                        Walk(declaration.Mutation?.Source ?? declaration.Query);
                    }
                }
            }

            void WalkFrom(QueryCommand owner, FromExpression? from)
            {
                if (from is null)
                    return;

                // Only a source that actually refers to a declaration participates in the CTE closure.
                // A plain mapped-entity source also exposes a table name (once prepared), and that name
                // may coincidentally equal a sibling declaration's name; including it would drag the
                // unrelated declaration into the WITH and shadow the physical table.
                if (!string.IsNullOrEmpty(from.Table) && IsCteReference(owner, from, knownNames))
                    names.Add(from.Table);

                if (from.SubQuery is { } subQuery)
                    Walk(subQuery);

                // A projection-source marker (a typed/data-modifying CTE read) carries the defining
                // command as ColumnShape; its own references are dependencies of the enclosing
                // declaration and must participate in the closure/ordering.
                if (from.ColumnShape is { } columnShape)
                    Walk(columnShape);

                if (from.Pivot is { } pivot)
                    WalkFrom(owner, pivot.Inner);
            }

            // A source is a CTE reference when it is a projection-source marker (a typed/data-modifying
            // read) or when its table name matches a declaration - either one declared on the enclosing
            // command or one of the candidate declarations being hoisted (a sibling may be declared
            // later, the fluent API builds the body first). A mapped entity source is a physical table
            // even when a declaration carries the same name, so the declaration must not shadow it.
            static bool IsCteReference(QueryCommand owner, FromExpression from, IReadOnlyCollection<string>? knownNames)
            {
                if (from.ColumnShape is not null)
                    return true;

                if (string.IsNullOrEmpty(from.Table))
                    return false;

                if (from.SourceType is not null || from.IsAutoMapped || IsMappedEntityTable(owner, from.Table))
                    return false;

                if (owner.Ctes is { Count: > 0 } ctes)
                {
                    for (var i = 0; i < ctes.Count; i++)
                    {
                        if (string.Equals(ctes[i].Name, from.Table, StringComparison.Ordinal))
                            return true;
                    }
                }

                if (knownNames is not null)
                {
                    foreach (var name in knownNames)
                    {
                        if (string.Equals(name, from.Table, StringComparison.Ordinal))
                            return true;
                    }
                }

                return false;
            }
        }

        // Whether the owning command reads from a mapped entity whose physical table is exactly
        // <paramref name="table"/>. Such a source is a physical table, not a CTE reference, even when a
        // declaration carries the same name. This resolves the owner command's own prepared source type
        // - a per-command fact - instead of scanning the process-wide entity metadata, so the outcome
        // cannot depend on which unrelated entity types happen to be registered, evicted or cleared by
        // another query at hoist time.
        private static bool IsMappedEntityTable(QueryCommand owner, string table)
            => owner.EntityType is { } type
                && DataContextCache.Metadata.TryGetValue(type, out var metadata)
                && !string.IsNullOrEmpty(metadata.TableName)
                && string.Equals(metadata.TableName, table, StringComparison.Ordinal);

        private enum VisitState
        {
            Visiting,
            Done,
        }
    }

    /// <summary>
    /// Returns the subset of <paramref name="candidates"/> reachable from <paramref name="root"/>'s body
    /// through CTE name references, in discovery order. The root is always included. A referenced name
    /// pulls in every candidate declared under it, so a conflicting second definition under one name is
    /// surfaced by the subsequent hoist rather than silently dropped; unreferenced candidates are
    /// omitted. When nothing is omitted the original list is returned.
    /// </summary>
    /// <param name="candidates">The declarations a descriptor carries (its own definition included).</param>
    /// <param name="root">The declaration whose body seeds the closure.</param>
    /// <returns>The reachable declarations, or <paramref name="candidates"/> when nothing is omitted.</returns>
    internal static IReadOnlyList<CteDefinition> Reachable(IReadOnlyList<CteDefinition> candidates, CteDefinition root)
    {
        if (candidates.Count <= 1)
            return candidates;

        var byName = new Dictionary<string, List<CteDefinition>>(StringComparer.Ordinal);
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (!byName.TryGetValue(candidate.Name, out var sameName))
                byName[candidate.Name] = sameName = [];
            sameName.Add(candidate);
        }

        var seen = new HashSet<CteDefinition>(ReferenceEqualityComparer.Instance);
        var reachable = new List<CteDefinition>(candidates.Count);
        var queue = new Queue<CteDefinition>();
        seen.Add(root);
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var cte = queue.Dequeue();
            reachable.Add(cte);

            foreach (var referenced in Walker.CollectReferencedNames(cte.Mutation?.Source ?? cte.Query, byName.Keys))
            {
                if (!byName.TryGetValue(referenced, out var dependencies))
                    continue;

                for (var i = 0; i < dependencies.Count; i++)
                {
                    if (seen.Add(dependencies[i]))
                        queue.Enqueue(dependencies[i]);
                }
            }
        }

        return reachable.Count == candidates.Count ? candidates : reachable;
    }
}
