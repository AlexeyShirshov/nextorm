namespace NextORM.Core;

/// <summary>
/// Thread-local store of compiled query plans (the "plan cache"). Plans are cached per thread and
/// shared by every context running on that thread, so the provider (context type) is part of the key:
/// the same query produces different SQL and different command implementations for SQLite, PostgreSQL
/// and SQL Server.
/// </summary>
/// <remarks>
/// Per-thread on purpose. Together with <see cref="DataContextCache"/> (process-wide metadata) and the
/// per-instance expression caches, this is the third cache scope in the context: see the remarks on
/// <see cref="DataContextCache"/> for the full picture.
/// </remarks>
internal static class QueryPlanStore
{
    private readonly record struct Key(Type ContextType, QueryPlan Plan);

    [ThreadStatic]
    private static Dictionary<Key, IDbCommandHolder>? _cache;

    private static Dictionary<Key, IDbCommandHolder> Cache => _cache ??= [];

    internal static bool TryGet(Type contextType, QueryPlan plan, out IDbCommandHolder? holder)
        => Cache.TryGetValue(new Key(contextType, plan), out holder);

    internal static void Set(Type contextType, QueryPlan plan, IDbCommandHolder holder)
        => Cache[new Key(contextType, plan)] = holder;

    internal static void Clear() => Cache.Clear();

    internal static IEnumerable<IDbCommandHolder> Values => Cache.Values;
}
