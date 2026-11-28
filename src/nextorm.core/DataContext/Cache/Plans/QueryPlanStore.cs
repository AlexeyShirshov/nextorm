using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Process-wide store of compatible implicit SQL plans. The context type and normalized structural
/// key determine compatibility; each holder manages execution state by exact connection.
/// </summary>
/// <remarks>
/// Lookups compare immutable bucket snapshots. A publication retries if another writer changed its
/// bucket, so no structural equality callback runs inside a dictionary lock. Clear replaces all
/// three indices together.
/// </remarks>
internal static class QueryPlanStore
{
    private static Store _store = new();

    private sealed class Store
    {
        internal readonly ConcurrentDictionary<(Type ContextType, int Hash), Bucket<CandidateReference<Entry>>> Plans = new();
        internal readonly ConcurrentDictionary<int, Bucket<CandidateReference<RawCandidate>>> Raw = new();
        internal readonly ConcurrentDictionary<int, Bucket<NativeCandidate>> Native = new();
        // Only the live command's memoized key owns the opaque entry, and only that entry owns
        // its raw candidate. Neither hash index roots a caller-owned graph.
        internal readonly ConditionalWeakTable<QueryPlan, Entry> WeakOwners = new();
    }

    private sealed class Bucket<T>
    {
        private T[] _entries = [];

        internal T[] Entries => Volatile.Read(ref _entries);

        internal bool TryReplace(T[] previous, T[] next)
            => ReferenceEquals(Interlocked.CompareExchange(ref _entries, next, previous), previous);
    }

    // A repeated command can memoize the stored key and skip structural comparison.
    private sealed class Entry(Type contextType, QueryPlan plan, IDbCommandHolder holder)
    {
        internal Type ContextType { get; } = contextType;
        internal QueryPlan Plan { get; } = plan;
        internal IDbCommandHolder Holder { get; } = holder;
        internal RawCandidate? Raw;
    }

    private readonly struct CandidateReference<T> where T : class
    {
        private readonly T? _strong;
        private readonly WeakReference<T>? _weak;

        internal CandidateReference(T candidate, bool weak)
        {
            if (weak)
                _weak = new WeakReference<T>(candidate);
            else
                _strong = candidate;
        }

        internal T? Get()
            => _weak is null ? _strong : _weak.TryGetTarget(out var candidate) ? candidate : null;
    }

    /// <summary>
    /// One published pre-preparation candidate: the raw descriptor captured before ordinary
    /// preparation, the normalized plan key, the holder, and the prepared graph to stage. The graph
    /// must be separate when the normalized key uses comparison-only expressions.
    /// </summary>
    internal sealed class RawCandidate(Type contextType, QueryPlan plan, IDbCommandHolder holder, QueryCommand.RawShape shape, QueryCommand recipe)
    {
        internal Type ContextType { get; } = contextType;
        internal QueryPlan Plan { get; } = plan;
        internal IDbCommandHolder Holder { get; } = holder;
        internal QueryCommand.RawShape Shape { get; } = shape;
        internal QueryCommand Recipe { get; } = recipe;
        internal void ApplyTo(QueryCommand command) =>
            command.ApplyPreparationFrom(Recipe, Shape);
    }

    private static int _nativeHits;
    private static int _nativePublishes;
    private static int _nativeMisses;

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
    internal static bool TryGetNative(in ShapeHeader header, LambdaExpression? root, out NativeCandidate? candidate)
    {
        candidate = null;
        var index = Volatile.Read(ref _store).Native;
        if (index.IsEmpty)
            return false;

        if (!ShapeIdentity.TryHash(header, root, out var hash))
            return false;

        if (index.TryGetValue(hash, out var bucket))
        {
            foreach (var entry in bucket.Entries)
            {
                if (!ShapeIdentity.Matches(entry.Shape, header, root))
                    continue;

                Interlocked.Increment(ref _nativeHits);
                candidate = entry;
                return true;
            }
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
        LambdaExpression? root0,
        LambdaExpression? root1,
        LambdaExpression? root2,
        out NativeCandidate? candidate)
    {
        candidate = null;
        var index = Volatile.Read(ref _store).Native;
        if (index.IsEmpty)
            return false;

        if (!ShapeIdentity.TryHash(header, root0, root1, root2, out var hash))
            return false;

        if (index.TryGetValue(hash, out var bucket))
        {
            foreach (var entry in bucket.Entries)
            {
                if (!ShapeIdentity.Matches(entry.Shape, header, root0, root1, root2))
                    continue;

                Interlocked.Increment(ref _nativeHits);
                candidate = entry;
                return true;
            }
        }

        Interlocked.Increment(ref _nativeMisses);
        return false;
    }

    /// <summary>Publishes the normalized plan's holder under its value-agnostic shape.</summary>
    /// <param name="contextType">The context type that published the normalized plan.</param>
    /// <param name="plan">The normalized plan key.</param>
    /// <param name="shape">The shape the holder was compiled for.</param>
    /// <param name="holder">The compiled holder.</param>
    /// <param name="binder">The value binder for the holder's parameters.</param>
    internal static void SetNative(Type contextType, QueryPlan plan, ShapeKey shape, IDbCommandHolder holder, NativeBinder binder)
    {
        var store = Volatile.Read(ref _store);
        if (plan.RequiresWeakOwner || !Owns(store, contextType, plan, holder))
            return;

        var bucket = store.Native.GetOrAdd(shape.Hash, static _ => new Bucket<NativeCandidate>());
        while (true)
        {
            var entries = bucket.Entries;
            foreach (var entry in entries)
            {
                if (shape.Tokens.AsSpan().SequenceEqual(entry.Shape.Tokens))
                    return;
            }

            if (bucket.TryReplace(entries, [.. entries, new NativeCandidate(shape, holder, binder)]))
            {
                Interlocked.Increment(ref _nativePublishes);
                return;
            }
        }
    }

    internal static bool TryGet(Type contextType, QueryPlan plan, out IDbCommandHolder? holder, out QueryPlan? storedPlan)
    {
        var store = Volatile.Read(ref _store);
        if (plan.RequiresWeakOwner && store.WeakOwners.TryGetValue(plan, out var registered)
            && registered.ContextType == contextType)
        {
            holder = registered.Holder;
            storedPlan = registered.Plan;
            return true;
        }

        if (store.Plans.TryGetValue((contextType, plan.GetHashCode()), out var bucket)
            && Find(bucket.Entries, plan) is { } existing)
        {
            var match = RetainMatch(store, bucket, plan, existing);
            holder = match.Holder;
            storedPlan = match.Plan;
            return true;
        }

        holder = null;
        storedPlan = null;
        return false;
    }

    internal static (QueryPlan Plan, IDbCommandHolder Holder) Set(Type contextType, QueryPlan plan, IDbCommandHolder holder)
    {
        var store = Volatile.Read(ref _store);
        var bucket = store.Plans.GetOrAdd((contextType, plan.GetHashCode()),
            static _ => new Bucket<CandidateReference<Entry>>());
        var candidate = new Entry(contextType, plan, holder);
        while (true)
        {
            var entries = bucket.Entries;
            if (Find(entries, plan) is { } existing)
            {
                var match = RetainMatch(store, bucket, plan, existing);
                return (match.Plan, match.Holder);
            }

            // Another writer may publish after this scan. In that case compare against the new
            // bucket rather than replacing the winner and leaving its aliases pointing elsewhere.
            if (TryPublish(store, bucket, entries, candidate))
                return (plan, holder);
        }
    }

    private static Entry? Find(CandidateReference<Entry>[] entries, QueryPlan plan, bool identityOnly = false)
    {
        foreach (var reference in entries)
        {
            if (reference.Get() is { } entry
                && (ReferenceEquals(entry.Plan, plan) || !identityOnly && entry.Plan.Equals(plan)))
                return entry;
        }

        return null;
    }

    private static Entry RetainMatch(Store store, Bucket<CandidateReference<Entry>> bucket, QueryPlan plan, Entry match)
    {
        if (!match.Plan.RequiresWeakOwner || ReferenceEquals(plan, match.Plan))
            return match;

        // Memoizing the winning weak key would retain the previous command's opaque state. Attach
        // the current command's own comparison snapshot to the same holder instead.
        var current = new Entry(match.ContextType, plan.GetSqlCacheVersion(), match.Holder);
        Entry retained;
        while (true)
        {
            var entries = bucket.Entries;
            if (Find(entries, current.Plan, identityOnly: true) is { } existing)
            {
                retained = existing;
                break;
            }
            if (TryPublish(store, bucket, entries, current))
            {
                retained = current;
                break;
            }
        }
        GC.KeepAlive(match);
        return retained;
    }

    private static bool TryPublish(Store store, Bucket<CandidateReference<Entry>> bucket, CandidateReference<Entry>[] previous, Entry entry)
    {
        var next = new List<CandidateReference<Entry>>(previous.Length + 1);
        foreach (var reference in previous)
        {
            if (reference.Get() is not null)
                next.Add(reference);
        }

        var weak = entry.Plan.RequiresWeakOwner;
        next.Add(new CandidateReference<Entry>(entry, weak));
        if (!bucket.TryReplace(previous, next.ToArray()))
            return false;

        if (weak)
            store.WeakOwners.TryAdd(entry.Plan, entry);
        return true;
    }

    /// <summary>
    /// Matches the raw structure within its hash bucket and verifies that the alias still names an
    /// owned normalized holder.
    /// </summary>
    internal static bool TryGetRaw(Type contextType, QueryCommand.RawShape shape, QueryCommand comparerSource, out RawCandidate? match)
    {
        match = null;

        var store = Volatile.Read(ref _store);
        if (!store.Raw.TryGetValue(shape.Fingerprint, out var bucket))
            return false;

        foreach (var reference in bucket.Entries)
        {
            if (reference.Get() is not { } candidate || candidate.ContextType != contextType
                || !candidate.Shape.Matches(shape, comparerSource)
                || !Owns(store, contextType, candidate.Plan, candidate.Holder))
                continue;

            if (!ReferenceEquals(Volatile.Read(ref _store), store))
                return false;

            match = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Publishes a raw alias for the canonical normalized holder.
    /// </summary>
    internal static bool SetRaw(Type contextType, QueryPlan plan, IDbCommandHolder holder, QueryCommand.RawShape shape, QueryCommand comparerSource)
    {
        var store = Volatile.Read(ref _store);
        if (GetOwner(store, contextType, plan) is not { } owner || !ReferenceEquals(owner.Holder, holder)
            || shape.SnapshotForReplay(comparerSource) is not { } snapshot)
            return false;

        var recipe = plan.NeedsSeparateRawRecipe || !ReferenceEquals(snapshot, shape)
            ? comparerSource.CloneForRawRecipe(snapshot) : plan.QueryCommand;
        var candidate = new RawCandidate(contextType, plan, holder, snapshot, recipe);
        var indexed = new CandidateReference<RawCandidate>(candidate, plan.RequiresWeakOwner);
        var bucket = store.Raw.GetOrAdd(shape.Fingerprint, static _ => new Bucket<CandidateReference<RawCandidate>>());

        while (true)
        {
            var entries = bucket.Entries;
            var next = new List<CandidateReference<RawCandidate>>(entries.Length + 1);
            foreach (var reference in entries)
            {
                if (reference.Get() is not { } existing)
                    continue;
                if (existing.ContextType == contextType && existing.Shape.Matches(shape, comparerSource))
                    return false;
                next.Add(reference);
            }

            next.Add(indexed);
            if (bucket.TryReplace(entries, next.ToArray()))
            {
                if (plan.RequiresWeakOwner)
                    Volatile.Write(ref owner.Raw, candidate);
                return true;
            }
        }
    }

    private static bool Owns(Store store, Type contextType, QueryPlan plan, IDbCommandHolder holder)
        => ReferenceEquals(GetOwner(store, contextType, plan)?.Holder, holder);

    private static Entry? GetOwner(Store store, Type contextType, QueryPlan plan)
        => store.Plans.TryGetValue((contextType, plan.GetHashCode()), out var bucket)
            ? Find(bucket.Entries, plan, identityOnly: true) : null;

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

    internal static void Clear() => Interlocked.Exchange(ref _store, new Store());

    internal static IEnumerable<IDbCommandHolder> Values
    {
        get
        {
            var seen = new HashSet<IDbCommandHolder>(ReferenceEqualityComparer.Instance);
            foreach (var bucket in Volatile.Read(ref _store).Plans.Values)
            {
                foreach (var reference in bucket.Entries)
                {
                    if (reference.Get() is { } entry && seen.Add(entry.Holder))
                        yield return entry.Holder;
                }
            }
        }
    }
}
