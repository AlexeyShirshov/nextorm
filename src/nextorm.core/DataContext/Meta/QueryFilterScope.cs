namespace NextORM.Core;

/// <summary>
/// The immutable set of global query filters disabled on a builder or command. It is the selective
/// replacement for the former all-or-nothing flag and is the unit accumulated by the
/// <c>IgnoreFilters</c> overloads.
/// </summary>
/// <remarks>
/// A scope is empty (no filter disabled), all (every filter disabled), type-only (every filter of the
/// listed entity types), key-only (the listed filter keys on any entity type), or a combined selector
/// (the listed keys only on the listed types). The three selective selectors are independent: a scope
/// disables a filter when any one of them matches, so type-only and key-only selectors accumulate by
/// union. Only a single combined <c>IgnoreFilters(keys, types)</c> call is an intersection (key and
/// type must both match). The key list is the gate for the combined overload: an empty key list
/// disables nothing, even when entity types are supplied.
/// </remarks>
internal sealed class QueryFilterScope
{
    /// <summary>An empty scope that disables no filter.</summary>
    public static readonly QueryFilterScope None = new(all: false);

    /// <summary>A scope that disables every filter.</summary>
    public static readonly QueryFilterScope AllFilters = new(all: true);

    private readonly HashSet<string> _keys;
    private readonly HashSet<Type> _types;
    private readonly KeyTypeClause[] _clauses;

    private QueryFilterScope(bool all, IEnumerable<string>? keys = null, IEnumerable<Type>? types = null, KeyTypeClause[]? clauses = null)
    {
        All = all;
        _keys = keys is null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(keys, StringComparer.Ordinal);
        _types = types is null ? [] : new HashSet<Type>(types);
        _clauses = clauses ?? [];
    }

    /// <summary>Whether every filter is disabled, regardless of key or entity type.</summary>
    public bool All { get; }

    /// <summary>Whether this scope disables no filter.</summary>
    public bool IsEmpty => !All && _keys.Count == 0 && _types.Count == 0 && _clauses.Length == 0;

    /// <summary>Builds a key-only scope, or <see cref="None"/> when the key list is empty.</summary>
    /// <param name="keys">The filter keys to disable on any entity type.</param>
    /// <returns>The scope, or <see cref="None"/> when no key is supplied.</returns>
    public static QueryFilterScope FromKeys(IEnumerable<string>? keys)
    {
        var keySet = keys is null ? null : new HashSet<string>(keys, StringComparer.Ordinal);
        return keySet is null || keySet.Count == 0 ? None : new QueryFilterScope(all: false, keys: keySet);
    }

    /// <summary>Builds a type-only scope, or <see cref="None"/> when no type is supplied.</summary>
    /// <param name="types">The entity types whose every filter is disabled.</param>
    /// <returns>The scope, or <see cref="None"/> when no type is supplied.</returns>
    public static QueryFilterScope FromTypes(params Type[]? types)
        => types is null || types.Length == 0 ? None : new QueryFilterScope(all: false, types: types);

    /// <summary>
    /// Builds the combined selector for the key-and-type overload: a filter is disabled when its key is
    /// in <paramref name="keys"/> <b>and</b> its entity type is in <paramref name="types"/>. An empty
    /// <paramref name="types"/> means any entity type, which is the key-only scope. The key list is the
    /// gate, so an empty or <see langword="null"/> key list disables nothing even when entity types are
    /// supplied.
    /// </summary>
    /// <param name="keys">The filter keys to disable.</param>
    /// <param name="types">The entity types the disable is scoped to; empty means any entity type.</param>
    /// <returns>The scope, or <see cref="None"/> when no key is supplied.</returns>
    public static QueryFilterScope FromKeysAndTypes(IEnumerable<string>? keys, params Type[]? types)
    {
        if (keys is null)
            return None;

        var keySet = new HashSet<string>(keys, StringComparer.Ordinal);
        if (keySet.Count == 0)
            return None;

        if (types is null || types.Length == 0)
            return new QueryFilterScope(all: false, keys: keySet);

        return new QueryFilterScope(all: false, clauses: [new KeyTypeClause(keySet, new HashSet<Type>(types))]);
    }

    /// <summary>
    /// Combines two scopes by union, so repeated <c>IgnoreFilters</c> calls accumulate. <see cref="All"/>
    /// dominates; an empty operand leaves the other unchanged. Type-only and key-only selectors combine
    /// independently, and each combined selector is kept as its own clause.
    /// </summary>
    /// <param name="other">The scope to add.</param>
    /// <returns>The combined scope.</returns>
    public QueryFilterScope Union(QueryFilterScope other)
    {
        if (All || other.All)
            return AllFilters;
        if (other.IsEmpty)
            return this;
        if (IsEmpty)
            return other;

        var keys = new HashSet<string>(_keys, StringComparer.Ordinal);
        keys.UnionWith(other._keys);

        var types = new HashSet<Type>(_types);
        types.UnionWith(other._types);

        var clauses = new KeyTypeClause[_clauses.Length + other._clauses.Length];
        Array.Copy(_clauses, clauses, _clauses.Length);
        Array.Copy(other._clauses, 0, clauses, _clauses.Length, other._clauses.Length);

        return new QueryFilterScope(all: false, keys: keys, types: types, clauses: clauses);
    }

    /// <summary>
    /// Whether the filter identified by <paramref name="filterKey"/> declared for
    /// <paramref name="entityType"/> is disabled by this scope.
    /// </summary>
    /// <param name="entityType">The entity type the filter is declared for.</param>
    /// <param name="filterKey">The filter key (<see cref="QueryFilters.AnonymousKey"/> when anonymous).</param>
    /// <returns><see langword="true"/> when the filter must be skipped.</returns>
    public bool Ignores(Type entityType, string filterKey)
    {
        if (All)
            return true;

        // Type-only and key-only selectors accumulate independently: either one disables the filter on
        // its own, so a type-only call and a later key-only call are not folded into one intersection.
        if (_types.Contains(entityType))
            return true;

        if (_keys.Contains(filterKey))
            return true;

        for (var i = 0; i < _clauses.Length; i++)
        {
            var clause = _clauses[i];
            if (clause.Keys.Contains(filterKey) && clause.Types.Contains(entityType))
                return true;
        }

        return false;
    }

    /// <summary>An immutable key-and-type selector: a filter is disabled when both sets contain it.</summary>
    private sealed class KeyTypeClause(HashSet<string> keys, HashSet<Type> types)
    {
        public HashSet<string> Keys { get; } = keys;
        public HashSet<Type> Types { get; } = types;
    }
}
