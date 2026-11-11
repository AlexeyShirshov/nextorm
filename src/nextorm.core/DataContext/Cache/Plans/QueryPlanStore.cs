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

    /// <summary>
    /// Native candidate index (I7/M2): the value-agnostic shape of a raw query mapped to the plan that
    /// was compiled for it. It shares the store's generation transition, so <see cref="Clear"/> leaves
    /// no usable alias, and it is thread-local for the same reason the normalized dictionary is.
    /// </summary>
    [ThreadStatic]
    private static Dictionary<int, List<NativeCandidate>>? _nativeIndex;

    private static int _nativeHits;
    private static int _nativePublishes;
    private static int _nativeMisses;

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

    private static Dictionary<int, List<NativeCandidate>> NativeIndex
    {
        get
        {
            var generation = Volatile.Read(ref _generation);
            if (_nativeIndex is null || _cacheGeneration != generation)
                _nativeIndex = [];

            return _nativeIndex;
        }
    }

    /// <summary>Number of native shapes served from the index. Diagnostic only.</summary>
    internal static int NativeHits => Volatile.Read(ref _nativeHits);

    /// <summary>Number of native shapes published after a miss. Diagnostic only.</summary>
    internal static int NativePublishes => Volatile.Read(ref _nativePublishes);

    /// <summary>Number of native probes that found no candidate. Diagnostic only.</summary>
    internal static int NativeMisses => Volatile.Read(ref _nativeMisses);

    /// <summary>Resets the native-route diagnostic counters. Test seam.</summary>
    internal static void ResetNativeCounters()
    {
        Volatile.Write(ref _nativeHits, 0);
        Volatile.Write(ref _nativePublishes, 0);
        Volatile.Write(ref _nativeMisses, 0);
    }

    /// <summary>
    /// Probes the native index for a plan compiled for the given value-agnostic shape. The hash is only
    /// an index; every candidate in the bucket is verified with a structural comparison, so a collision
    /// cannot select a wrong plan.
    /// </summary>
    /// <param name="header">The non-expression dimensions of the request.</param>
    /// <param name="root">The fresh expression graph.</param>
    /// <param name="candidate">The matching candidate, when one was verified.</param>
    /// <returns><see langword="true"/> when an exact structural match was found.</returns>
    internal static bool TryGetNative(in ShapeHeader header, System.Linq.Expressions.LambdaExpression? root, out NativeCandidate? candidate)
    {
        candidate = null;
        var index = NativeIndex;
        if (index.Count == 0)
            return false;

        if (!ShapeIdentity.TryHash(header, root, out var hash))
            return false;

        if (!index.TryGetValue(hash, out var bucket) || bucket.Count == 0)
        {
            Interlocked.Increment(ref _nativeMisses);
            return false;
        }

        for (var i = 0; i < bucket.Count; i++)
        {
            var entry = bucket[i];
            if (!ShapeIdentity.Matches(entry.Shape, header, root))
                continue;

            Interlocked.Increment(ref _nativeHits);
            candidate = entry;
            return true;
        }

        Interlocked.Increment(ref _nativeMisses);
        return false;
    }

    /// <summary>
    /// Probes the native index for a plan compiled for a three-graph value-agnostic shape (filter,
    /// grouping key, projection). Same contract as the single-graph overload: the hash indexes, the
    /// structural comparison proves, and a collision cannot select a wrong plan.
    /// </summary>
    /// <param name="header">The non-expression dimensions of the request.</param>
    /// <param name="root0">The fresh filter graph, or <see langword="null"/>.</param>
    /// <param name="root1">The fresh grouping graph, or <see langword="null"/>.</param>
    /// <param name="root2">The fresh projection graph, or <see langword="null"/>.</param>
    /// <param name="candidate">The matching candidate, when one was verified.</param>
    /// <returns><see langword="true"/> when an exact structural match was found.</returns>
    internal static bool TryGetNative(
        in ShapeHeader header,
        System.Linq.Expressions.LambdaExpression? root0,
        System.Linq.Expressions.LambdaExpression? root1,
        System.Linq.Expressions.LambdaExpression? root2,
        out NativeCandidate? candidate)
    {
        candidate = null;
        var index = NativeIndex;
        if (index.Count == 0)
            return false;

        if (!ShapeIdentity.TryHash(header, root0, root1, root2, out var hash))
            return false;

        if (!index.TryGetValue(hash, out var bucket) || bucket.Count == 0)
        {
            Interlocked.Increment(ref _nativeMisses);
            return false;
        }

        for (var i = 0; i < bucket.Count; i++)
        {
            var entry = bucket[i];
            if (!ShapeIdentity.Matches(entry.Shape, header, root0, root1, root2))
                continue;

            Interlocked.Increment(ref _nativeHits);
            candidate = entry;
            return true;
        }

        Interlocked.Increment(ref _nativeMisses);
        return false;
    }

    /// <summary>Publishes a compiled holder under its value-agnostic shape (a no-op replace on a collision).</summary>
    /// <param name="shape">The shape the holder was compiled for.</param>
    /// <param name="holder">The compiled holder.</param>
    /// <param name="binder">The value binder for the holder's parameters.</param>
    internal static void SetNative(ShapeKey shape, IDbCommandHolder holder, NativeBinder binder)
    {
        var index = NativeIndex;
        if (!index.TryGetValue(shape.Hash, out var bucket))
        {
            bucket = new List<NativeCandidate>(2);
            index[shape.Hash] = bucket;
        }

        var candidate = new NativeCandidate(shape, holder, binder);
        for (var i = 0; i < bucket.Count; i++)
        {
            if (ReferenceEquals(bucket[i].Shape, shape))
            {
                bucket[i] = candidate;
                Interlocked.Increment(ref _nativePublishes);
                return;
            }
        }

        bucket.Add(candidate);
        Interlocked.Increment(ref _nativePublishes);
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
    /// Looks up a published raw candidate by its descriptor-free fingerprint. The warm probe reads the
    /// live command (no descriptor is materialized), so the candidate is selected by structural
    /// fingerprint alone; the caller's staged normalized-key check remains the correctness gate against
    /// a collision.
    /// </summary>
    /// <param name="contextType">The provider context type the plan belongs to.</param>
    /// <param name="fingerprint">The structural fingerprint of the fresh command graph.</param>
    /// <param name="plan">The stored normalized key of the matched candidate.</param>
    /// <returns><see langword="true"/> when a published candidate with a live owner was found.</returns>
    internal static bool TryGetRawFingerprint(Type contextType, int fingerprint, out QueryPlan? plan)
    {
        plan = null;

        var generation = Volatile.Read(ref _generation);
        var cache = Cache; // performs the shared generation transition for both indexes
        if (cache.Count == 0)
            return false;

        if (!RawIndex.TryGetValue(fingerprint, out var bucket) || bucket.Count == 0)
            return false;

        for (var i = 0; i < bucket.Count; i++)
        {
            var candidate = bucket[i];
            if (candidate.Shape.Fingerprint != fingerprint)
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

    /// <summary>
    /// One published native candidate: the shape it belongs to, the compiled holder that executes it and
    /// the binder that refreshes the holder's parameters from the current closures.
    /// </summary>
    internal sealed class NativeCandidate(ShapeKey shape, IDbCommandHolder holder, NativeBinder binder)
    {
        /// <summary>The published value-agnostic shape.</summary>
        internal ShapeKey Shape { get; } = shape;

        /// <summary>The compiled holder the native route executes.</summary>
        internal IDbCommandHolder Holder { get; } = holder;

        /// <summary>The binder for the holder's parameters.</summary>
        internal NativeBinder Binder { get; } = binder;
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
