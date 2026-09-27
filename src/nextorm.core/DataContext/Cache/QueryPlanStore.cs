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
/// <para>
/// Clearing is process-wide even though the dictionary is <see cref="ThreadStaticAttribute"/>: a
/// process-wide generation counter is bumped, and each thread lazily recreates its own dictionary the
/// next time it touches the store. A dictionary owned by another thread can therefore only be dropped
/// when that thread runs again, which is the strongest invalidation possible for thread-static state.
/// </para>
/// </remarks>
internal static class QueryPlanStore
{
    private readonly record struct Key(Type ContextType, QueryPlan Plan);

    // The stored key is kept next to the holder so a lookup can hand back the exact plan instance that
    // is in the cache. A repeated command memoizes that instance and matches it by reference on the
    // next lookup, instead of re-comparing its expression tree against an equivalent plan.
    private readonly record struct Entry(Key Key, IDbCommandHolder Holder);

    private static long _generation;

    [ThreadStatic]
    private static Dictionary<Key, Entry>? _cache;

    [ThreadStatic]
    private static long _cacheGeneration;

    private static Dictionary<Key, Entry> Cache
    {
        get
        {
            var generation = Volatile.Read(ref _generation);
            if (_cache is null || _cacheGeneration != generation)
            {
                _cache = [];
                _cacheGeneration = generation;
            }

            return _cache;
        }
    }

    internal static bool TryGet(Type contextType, QueryPlan plan, out IDbCommandHolder? holder, out QueryPlan? storedPlan)
    {
        if (Cache.TryGetValue(new Key(contextType, plan), out var entry))
        {
            holder = entry.Holder;
            storedPlan = entry.Key.Plan;
            return true;
        }

        holder = null;
        storedPlan = null;
        return false;
    }

    internal static void Set(Type contextType, QueryPlan plan, IDbCommandHolder holder)
    {
        var key = new Key(contextType, plan);
        Cache[key] = new Entry(key, holder);
    }

    internal static void Clear() => Interlocked.Increment(ref _generation);

    internal static IEnumerable<IDbCommandHolder> Values
    {
        get
        {
            foreach (var entry in Cache.Values)
                yield return entry.Holder;
        }
    }
}
