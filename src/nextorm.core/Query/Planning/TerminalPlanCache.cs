using System.Collections.Generic;

namespace NextORM.Core;

/// <summary>
/// Bounded, context-owned cache of compiled terminal-plan delegates. One instance is shared by every
/// terminal kind (JSON row writers and CSV column plans) of a single <see cref="DataContext"/>: entries
/// are stored in a single LRU of capacity <see cref="Capacity"/> and are tagged by a kind-prefixed key,
/// so JSON and CSV never alias and the combined bound is honoured.
/// <para>
/// Capacity is <see cref="Capacity"/> published entries with LRU eviction. Lookup, cold compilation,
/// publication and eviction run under one lock, so a resident entry is compiled exactly once even under
/// concurrent first use; the returned delegates are invoked outside the lock. Boundedness and isolation
/// are both required: entries live per <see cref="DataContext"/> (two contexts never share), and a
/// process-wide generation in <see cref="DataContextCache"/> invalidates every context's entries when
/// the process-wide caches are cleared.
/// </para>
/// </summary>
internal sealed class TerminalPlanCache
{
    /// <summary>Maximum number of published entries across all terminal kinds; the least recently used entry is evicted first.</summary>
    internal const int Capacity = 64;

    private sealed class Entry
    {
        internal Entry(string key, object value)
        {
            Key = key;
            Value = value;
        }

        internal string Key { get; }

        internal object Value { get; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _index = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _lru = new();
    private long _generation = DataContextCache.JsonRowWriterCacheGeneration;
    private int _compilationCount;

    /// <summary>The number of published entries (test/evidence observability).</summary>
    internal int EntryCount
    {
        get
        {
            lock (_gate)
                return _index.Count;
        }
    }

    /// <summary>
    /// The number of cold compilations performed under the lock. A warm hit does not increment it, so it
    /// is the observable proof that a resident entry was built once (test/evidence observability).
    /// </summary>
    internal int CompilationCount => Volatile.Read(ref _compilationCount);

    /// <summary>
    /// Returns the resident value for <paramref name="key"/>, building and publishing it on a miss.
    /// </summary>
    /// <param name="key">The fully-qualified (kind-tagged) cache key.</param>
    /// <param name="build">Compiles the value on a miss.</param>
    /// <returns>The cached or freshly built value.</returns>
    internal object GetOrBuild(string key, Func<object> build)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(build);

        var generation = DataContextCache.JsonRowWriterCacheGeneration;
        lock (_gate)
        {
            if (_generation != generation)
            {
                _index.Clear();
                _lru.Clear();
                _generation = generation;
            }

            if (_index.TryGetValue(key, out var node))
            {
                if (!ReferenceEquals(node, _lru.First))
                {
                    _lru.Remove(node);
                    _lru.AddFirst(node);
                }

                return node.Value.Value;
            }

            var value = build();
            Interlocked.Increment(ref _compilationCount);

            var added = _lru.AddFirst(new Entry(key, value));
            _index.Add(key, added);
            if (_index.Count > Capacity)
            {
                var last = _lru.Last!;
                _lru.RemoveLast();
                _index.Remove(last.Value.Key);
            }

            return value;
        }
    }

    /// <summary>Drops every entry, releasing the delegates it retained.</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            _index.Clear();
            _lru.Clear();
        }
    }
}