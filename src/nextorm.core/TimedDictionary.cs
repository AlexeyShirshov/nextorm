using System.Collections;
using System.Collections.Concurrent;

namespace NextORM.Core;

/// <summary>
/// Thread-safe dictionary with optional sliding expiration, used to back the process-wide
/// <see cref="DataContextCache"/> caches. While
/// <see cref="DataContextCache.CacheSlidingExpiration"/> is greater than zero, every read refreshes the
/// entry's timestamp and an entry that has not been read within the window is evicted lazily on the
/// next access or enumeration. <see cref="TimeSpan.Zero"/> (the default) disables expiration and skips
/// the timestamp path entirely.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The cached value type.</typeparam>
internal sealed class TimedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    where TKey : notnull
{
    private sealed class Entry
    {
        internal Entry(TValue value, long stamp, bool timed)
        {
            Value = value;
            Stamp = stamp;
            Timed = timed;
        }

        internal TValue Value { get; }
        internal long Stamp { get; set; }
        internal bool Timed { get; }
    }

    private readonly ConcurrentDictionary<TKey, Entry> _inner = new();

    private static long UtcNowTicks => TimeProvider.System.GetUtcNow().UtcTicks;

    private Entry MakeEntry(TValue value)
    {
        var timed = DataContextCache.CacheSlidingExpiration > TimeSpan.Zero;
        return new Entry(value, timed ? UtcNowTicks : 0, timed);
    }

    private bool TryGetLive(TKey key, out Entry entry)
    {
        if (!_inner.TryGetValue(key, out entry!))
            return false;

        if (!entry.Timed)
            return true;

        var ttl = DataContextCache.CacheSlidingExpiration;
        if (ttl <= TimeSpan.Zero)
            return true;

        var now = UtcNowTicks;
        if (now - entry.Stamp > ttl.Ticks)
        {
            _inner.TryRemove(new KeyValuePair<TKey, Entry>(key, entry));
            entry = null!;
            return false;
        }

        entry.Stamp = now;
        return true;
    }

    private List<KeyValuePair<TKey, TValue>> Snapshot()
    {
        var ttl = DataContextCache.CacheSlidingExpiration;
        var result = new List<KeyValuePair<TKey, TValue>>(_inner.Count);

        foreach (var pair in _inner)
        {
            if (ttl > TimeSpan.Zero && pair.Value.Timed && UtcNowTicks - pair.Value.Stamp > ttl.Ticks)
            {
                _inner.TryRemove(pair);
                continue;
            }

            result.Add(new KeyValuePair<TKey, TValue>(pair.Key, pair.Value.Value));
        }

        return result;
    }

    /// <summary>Gets or sets the value associated with <paramref name="key"/>.</summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The live cached value.</returns>
    /// <exception cref="KeyNotFoundException">The key is absent or its entry has expired.</exception>
    public TValue this[TKey key]
    {
        get => TryGetLive(key, out var entry) ? entry.Value : throw new KeyNotFoundException($"Key '{key}' was not found.");
        set => _inner[key] = MakeEntry(value);
    }

    /// <inheritdoc/>
    public ICollection<TKey> Keys => [.. Snapshot().Select(pair => pair.Key)];
    /// <inheritdoc/>
    public ICollection<TValue> Values => [.. Snapshot().Select(pair => pair.Value)];
    /// <inheritdoc/>
    public int Count => Snapshot().Count;
    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(TKey key, TValue value)
    {
        if (!_inner.TryAdd(key, MakeEntry(value)))
            throw new ArgumentException($"An item with the same key has already been added. Key: {key}", nameof(key));
    }

    /// <inheritdoc/>
    public bool ContainsKey(TKey key) => TryGetLive(key, out _);

    /// <inheritdoc/>
    public bool Remove(TKey key) => _inner.TryRemove(key, out _);

    /// <inheritdoc/>
    public bool TryGetValue(TKey key, out TValue value)
    {
        if (TryGetLive(key, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <inheritdoc/>
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    /// <inheritdoc/>
    public void Clear() => _inner.Clear();

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<TKey, TValue> item)
        => TryGetLive(item.Key, out var entry) && EqualityComparer<TValue>.Default.Equals(entry.Value, item.Value);

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        => Snapshot().CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<TKey, TValue> item)
        => TryGetLive(item.Key, out var entry)
            && EqualityComparer<TValue>.Default.Equals(entry.Value, item.Value)
            && _inner.TryRemove(item.Key, out _);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Snapshot().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
