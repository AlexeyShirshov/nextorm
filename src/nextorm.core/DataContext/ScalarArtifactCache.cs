using System.Globalization;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// The aggregate terminal a scalar artifact describes. Only the terminals whose projection is fully
/// determined by the entity/result types and (for <see cref="Sum"/>) one mapped member are listed; every
/// other terminal keeps the ordinary path.
/// </summary>
internal enum ScalarTerminal : byte
{
    /// <summary><c>Count()</c> — <c>count(*)</c> over the entity source.</summary>
    Count = 0,

    /// <summary><c>LongCount()</c> — the 64-bit <c>count_big(*)</c>/<c>count(*)</c> form.</summary>
    LongCount = 1,

    /// <summary><c>Sum(x =&gt; x.Member)</c> — <c>sum(column)</c> over one mapped member.</summary>
    Sum = 2,
}

/// <summary>
/// Structural identity of one supported scalar terminal invocation: the terminal, the entity and result
/// types, and — for <see cref="ScalarTerminal.Sum"/> — the mapped member the selector reads. A fresh
/// inline selector and an equivalent cached selector normalize to the same key because the key holds the
/// <see cref="MemberInfo"/> identity, never the expression instance.
/// </summary>
internal readonly struct ScalarArtifactKey : IEquatable<ScalarArtifactKey>
{
    internal ScalarArtifactKey(Type entityType, Type resultType, ScalarTerminal terminal, MemberInfo? member)
    {
        EntityType = entityType;
        ResultType = resultType;
        Terminal = terminal;
        Member = member;
    }

    /// <summary>The mapped entity type the terminal aggregates.</summary>
    internal Type EntityType { get; }

    /// <summary>The terminal's result type (for example <see cref="int"/> or a nullable sum type).</summary>
    internal Type ResultType { get; }

    /// <summary>The supported terminal.</summary>
    internal ScalarTerminal Terminal { get; }

    /// <summary>The mapped member a <see cref="ScalarTerminal.Sum"/> selector reads, or <see langword="null"/>.</summary>
    internal MemberInfo? Member { get; }

    /// <summary>Compares two keys. All parts are reference/structural identities, never current values.</summary>
    /// <param name="other">The key to compare with.</param>
    /// <returns><see langword="true"/> when every part of the key matches.</returns>
    public bool Equals(ScalarArtifactKey other)
        => Terminal == other.Terminal
            && ReferenceEquals(EntityType, other.EntityType)
            && ReferenceEquals(ResultType, other.ResultType)
            && Equals(Member, other.Member);

    /// <summary>Determines whether <paramref name="obj"/> is an equal key.</summary>
    /// <param name="obj">The object to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal <see cref="ScalarArtifactKey"/>.</returns>
    public override bool Equals(object? obj) => obj is ScalarArtifactKey other && Equals(other);

    /// <summary>Returns a hash combining the key's type and terminal identities.</summary>
    /// <returns>A stable hash for the key.</returns>
    public override int GetHashCode()
        => HashCode.Combine(
            EntityType.GetHashCode(),
            ResultType.GetHashCode(),
            (int)Terminal,
            Member?.GetHashCode() ?? 0);

    /// <summary>Returns a diagnostic representation of the key.</summary>
    /// <returns>The key parts, for diagnostics only.</returns>
    public override string ToString()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{Terminal}({EntityType.Name} => {ResultType.Name}{(Member is null ? string.Empty : $".{Member.Name}")})");
}

/// <summary>
/// One immutable scalar-shape artifact owned by a single <see cref="DataContext"/>: the normalized key
/// plus the immutable, frozen-hash plan-cache key that key prepared the terminal under.
/// <para>
/// The artifact holds <b>no</b> builder, no mutable command for execution, no closure object, no current
/// argument and no execution result. <see cref="PlanKey"/> is only a structural cache key: its command is
/// the detached cache-side clone the plan store itself keeps, and it is never prepared, mutated or
/// executed by the fast path — execution always goes through the current invocation's arguments against
/// the plan the store returns for that key.
/// </para>
/// </summary>
internal sealed class ScalarArtifact
{
    internal ScalarArtifact(in ScalarArtifactKey key) => Key = key;

    /// <summary>The normalized terminal identity this artifact was created for.</summary>
    internal ScalarArtifactKey Key { get; }

    /// <summary>
    /// The normalized plan-cache key of the warm scalar plan, or <see langword="null"/> when the plan was
    /// not stored (a cache-disabled or otherwise uncacheable preparation) and the artifact is inert.
    /// Published through a volatile reference: a reader either sees the key (a hit) or falls back.
    /// </summary>
    internal QueryPlan? PlanKey { get => _planKey; set => _planKey = value; }

    private volatile QueryPlan? _planKey;
}

/// <summary>
/// Bounded, per-context store of <see cref="ScalarArtifact"/> entries backing the context-owned immutable
/// scalar fast path. Capacity is 16 with FIFO eviction; lookups and inserts are allocation-free (a linear
/// scan over a fixed array), and <see cref="Clear"/> drops every entry so a disposed or purged context
/// releases the plan keys it retained.
/// </summary>
internal sealed class ScalarArtifactCache
{
    /// <summary>Maximum number of retained artifacts; the oldest entry is evicted first.</summary>
    internal const int Capacity = 16;

    private readonly ScalarArtifact?[] _entries = new ScalarArtifact?[Capacity];
    private readonly object _gate = new();
    private int _count;

    /// <summary>Returns the artifact for <paramref name="key"/>, or <see langword="null"/> on a miss.</summary>
    /// <param name="key">The normalized terminal key.</param>
    /// <returns>The artifact, or <see langword="null"/> when the shape was not seen yet.</returns>
    internal ScalarArtifact? Find(in ScalarArtifactKey key)
    {
        lock (_gate)
        {
            for (var i = 0; i < _count; i++)
            {
                if (_entries[i]!.Key.Equals(key))
                    return _entries[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the artifact for <paramref name="key"/>, creating it (and evicting the oldest entry when
    /// the cache is full) on a miss.
    /// </summary>
    /// <param name="key">The normalized terminal key.</param>
    /// <returns>The existing or newly created artifact.</returns>
    internal ScalarArtifact GetOrAdd(in ScalarArtifactKey key)
    {
        lock (_gate)
        {
            for (var i = 0; i < _count; i++)
            {
                if (_entries[i]!.Key.Equals(key))
                    return _entries[i]!;
            }

            var artifact = new ScalarArtifact(key);
            if (_count == Capacity)
            {
                // FIFO: drop the oldest entry and shift the window; the array is fixed, so no allocation.
                Array.Copy(_entries, 1, _entries, 0, Capacity - 1);
                _entries[Capacity - 1] = artifact;
            }
            else
            {
                _entries[_count++] = artifact;
            }

            return artifact;
        }
    }

    /// <summary>Drops every retained artifact, releasing the plan keys it referenced.</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_entries);
            _count = 0;
        }
    }
}
