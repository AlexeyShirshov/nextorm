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

    /// <summary>
    /// One published pre-preparation candidate: the raw descriptor captured before ordinary
    /// preparation, the normalized plan key that the holder was stored under, and the holder itself.
    /// It owns no command, no closure, no current parameter value and no mutable command collection;
    /// the preparation data is read from the stored plan key (the existing Entry) at apply time.
    /// </summary>
    internal sealed class RawCandidate(QueryPlan plan, IDbCommandHolder holder, QueryCommand.RawShape shape)
    {
        internal QueryPlan Plan { get; } = plan;
        internal IDbCommandHolder Holder { get; } = holder;
        internal QueryCommand.RawShape Shape { get; } = shape;
    }

    /// <summary>
    /// Raw candidate index: buckets by the descriptor's structural fingerprint. It lives beside the
    /// normalized dictionary in the same thread-local scope and is dropped by the same generation
    /// transition, so <see cref="Clear"/> leaves no usable auxiliary alias.
    /// </summary>
    [ThreadStatic]
    private static Dictionary<int, List<RawCandidate>>? _rawIndex;

    private static Dictionary<Key, Entry> Cache
    {
        get
        {
            var generation = Volatile.Read(ref _generation);
            if (_cache is null || _cacheGeneration != generation)
            {
                _cache = [];
                _cacheGeneration = generation;
                // Same transition drops the raw candidate index: Clear must not leave a usable alias.
                _rawIndex = null;
            }

            return _cache;
        }
    }

    private static Dictionary<int, List<RawCandidate>> RawIndex
    {
        get
        {
            // The transition is owned by Cache (it advances _cacheGeneration and clears the normalized
            // dictionary); this getter only re-checks the same generation value, so both indexes are
            // always reset under the one transition and never diverge.
            var generation = Volatile.Read(ref _generation);
            if (_rawIndex is null || _cacheGeneration != generation)
                _rawIndex = [];

            return _rawIndex;
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

    /// <summary>
    /// Looks up a published raw candidate for <paramref name="shape"/> without enumerating the
    /// normalized values: it enters the shared generation transition, reads only the matching bucket,
    /// verifies the raw structure, then requires that the candidate's historical normalized key is
    /// still owned in the live dictionary by the very holder that was published, and rechecks the
    /// generation before reporting a hit. <paramref name="comparerSource"/> supplies the expression
    /// comparers used for raw structural verification.
    /// </summary>
    internal static bool TryGetRaw(Type contextType, QueryCommand.RawShape shape, QueryCommand comparerSource, out QueryPlan? plan)
    {
        plan = null;

        var generation = Volatile.Read(ref _generation);
        var cache = Cache; // performs the shared generation transition for both indexes
        if (cache.Count == 0)
            return false;

        if (!RawIndex.TryGetValue(shape.Fingerprint, out var bucket) || bucket.Count == 0)
            return false;

        for (var i = 0; i < bucket.Count; i++)
        {
            var candidate = bucket[i];
            if (!candidate.Shape.Matches(shape, comparerSource))
                continue;

            // The owner must still be live under the historical normalized key and hold the published holder.
            if (!cache.TryGetValue(new Key(contextType, candidate.Plan), out var entry))
                continue;

            if (!ReferenceEquals(entry.Holder, candidate.Holder))
                continue;

            // Generation recheck before the fast path commits: a Clear observed here rejects the candidate.
            if (Volatile.Read(ref _generation) != generation)
                return false;

            plan = candidate.Plan;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Publishes a raw candidate for a holder that was just stored through <see cref="Set"/>.
    /// Existing candidates whose shape already matches are replaced, so a replaced owner never keeps
    /// a stale alias to a former holder (holder-identity validation rejects it anyway).
    /// </summary>
    internal static void SetRaw(Type contextType, QueryPlan plan, IDbCommandHolder holder, QueryCommand.RawShape shape, QueryCommand comparerSource)
    {
        _ = contextType;
        var index = RawIndex;
        if (!index.TryGetValue(shape.Fingerprint, out var bucket))
        {
            bucket = new List<RawCandidate>(2);
            index[shape.Fingerprint] = bucket;
        }

        for (var i = 0; i < bucket.Count; i++)
        {
            if (bucket[i].Shape.Matches(shape, comparerSource))
            {
                bucket[i] = new RawCandidate(plan, holder, shape);
                return;
            }
        }

        bucket.Add(new RawCandidate(plan, holder, shape));
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
