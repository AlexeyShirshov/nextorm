namespace NextORM.Core;

/// <summary>
/// Thread-local, bounded store of immutable <see cref="RawReaderDescriptor"/> metadata for the
/// <c>ToDataReader</c> terminal. It shares the <see cref="QueryPlanStore"/> process-wide generation
/// lifecycle and <c>Clear</c>, but is an independent partition: it contains no holders, no
/// <c>ResetConnection</c> obligations and no mutable command state. Capacity is
/// <see cref="Capacity"/> entries with LRU eviction; eviction and memory pressure degrade to fallback,
/// never to rejection.
/// </summary>
internal static class ReaderDescriptorStore
{
    public const int Capacity = 64;

    private sealed class Entry
    {
        public RawReaderKey Key;
        public RawReaderDescriptor? Descriptor;
    }

    [ThreadStatic]
    private static Dictionary<RawReaderKey, LinkedListNode<Entry>>? _index;

    [ThreadStatic]
    private static LinkedList<Entry>? _lru;

    [ThreadStatic]
    private static long _cacheGeneration;

    private static void EnsureGeneration()
    {
        var generation = QueryPlanStore.Generation;
        if (_index is null || _cacheGeneration != generation)
        {
            _index = new Dictionary<RawReaderKey, LinkedListNode<Entry>>();
            _lru = new LinkedList<Entry>();
            _cacheGeneration = generation;
        }
    }

    /// <summary>Number of resident entries (test/evidence observability).</summary>
    internal static int EntryCount
    {
        get
        {
            EnsureGeneration();
            return _index!.Count;
        }
    }

    internal static bool TryGet(in RawReaderKey key, out RawReaderDescriptor? descriptor)
    {
        EnsureGeneration();

        if (!_index!.TryGetValue(key, out var node))
        {
            descriptor = null;
            return false;
        }

        // LRU touch.
        if (!ReferenceEquals(node, _lru!.First))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
        }

        var entry = node.Value;

        // Generation staleness: a descriptor published under an older generation is dropped.
        if (entry.Descriptor is null || entry.Descriptor.Generation != QueryPlanStore.Generation)
        {
            _lru.Remove(node);
            _index.Remove(key);
            descriptor = null;
            return false;
        }

        descriptor = entry.Descriptor;
        return true;
    }

    internal static void TryPublish(in RawReaderKey key, RawReaderDescriptor descriptor)
    {
        // Never throws to the caller; stale generations and non-keyable forms are rejected silently.
        try
        {
            if (descriptor is null || string.IsNullOrEmpty(descriptor.SqlText))
                return;

            if (descriptor.Generation != QueryPlanStore.Generation)
                return;

            if (!key.IsKeyable)
                return;

            if (key.ContextType is null || key.DialectType is null)
                return;

            EnsureGeneration();

            if (_index!.TryGetValue(key, out var existing))
            {
                existing.Value.Descriptor = descriptor;
                if (!ReferenceEquals(existing, _lru!.First))
                {
                    _lru.Remove(existing);
                    _lru.AddFirst(existing);
                }
                return;
            }

            var node = _lru!.AddFirst(new Entry { Key = key, Descriptor = descriptor });
            _index[key] = node;

            if (_index.Count > Capacity)
            {
                var last = _lru.Last;
                if (last is not null)
                {
                    _lru.RemoveLast();
                    _index.Remove(last.Value.Key);
                }
            }
        }
        catch
        {
            // Swallow: publication failures degrade to fallback, never propagate.
        }
    }

    internal static void Clear() => QueryPlanStore.Clear();
}
