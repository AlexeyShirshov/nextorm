namespace NextORM.Core;

/// <summary>Provides the ring and physical placement for configured shards.</summary>
public interface IShardTopology
{
    /// <summary>Gets the topology version, incremented when shard membership changes.</summary>
    int Version { get; }

    /// <summary>Gets the consistent-hash ring used to route keys.</summary>
    IConsistentHashRing Ring { get; }

    /// <summary>Gets the placement for an entity on a configured shard.</summary>
    /// <param name="entityType">The entity type whose mapping is being placed.</param>
    /// <param name="shardId">The configured shard identifier.</param>
    /// <returns>The shard's schema and table override.</returns>
    ShardPlacement GetPlacement(Type entityType, string shardId);

    /// <summary>Gets the configured shard identifiers in ordinal order.</summary>
    IReadOnlyList<string> ShardIds { get; }
}

/// <summary>Stores shard placements and maintains their consistent-hash ring.</summary>
public sealed class ShardTopology : IShardTopology
{
    private readonly object _gate = new();
    private readonly ConsistentHashRing _ring;
    private readonly IConsistentHashRing _ringView;
    private readonly Dictionary<string, ShardPlacement> _placements = new(StringComparer.Ordinal);

    /// <summary>Creates an initially empty topology that can be populated with <see cref="AddShard"/>.</summary>
    /// <param name="virtualNodesPerShard">The number of virtual nodes created for each shard.</param>
    /// <param name="bucketCount">The number of stable buckets exposed by the ring.</param>
    /// <exception cref="ArgumentOutOfRangeException">A configured count is not positive.</exception>
    public ShardTopology(int virtualNodesPerShard = 128, int bucketCount = 4096)
    {
        _ring = new ConsistentHashRing(virtualNodesPerShard, bucketCount);
        _ringView = new RingView(_ring);
    }

    /// <inheritdoc />
    public int Version => _ring.Version;

    /// <inheritdoc />
    public IConsistentHashRing Ring => _ringView;

    /// <inheritdoc />
    public IReadOnlyList<string> ShardIds => _ring.ShardIds;

    /// <summary>Adds a shard and its optional physical schema and table placement.</summary>
    /// <param name="shardId">The SQL-safe shard identifier.</param>
    /// <param name="schema">The schema override, or <see langword="null"/> to retain the entity schema.</param>
    /// <param name="tableName">The table override, or <see langword="null"/> to suffix the base table with the shard id.</param>
    /// <exception cref="ArgumentException">The identifier is invalid or already configured.</exception>
    public void AddShard(string shardId, string? schema = null, string? tableName = null)
    {
        ValidateSqlSafeShardId(shardId);
        lock (_gate)
        {
            if (_placements.ContainsKey(shardId))
                throw new ArgumentException($"Shard '{shardId}' is already configured.", nameof(shardId));

            _ring.AddShard(shardId);
            _placements.Add(shardId, new ShardPlacement(schema, tableName));
        }
    }

    /// <summary>Removes a configured shard and its physical placement.</summary>
    /// <param name="shardId">The SQL-safe shard identifier to remove.</param>
    /// <exception cref="ArgumentException">The identifier is invalid, absent, or its removal would leave no shards.</exception>
    public void RemoveShard(string shardId)
    {
        ValidateSqlSafeShardId(shardId);
        lock (_gate)
        {
            if (!_placements.ContainsKey(shardId))
                throw new ArgumentException($"Shard '{shardId}' is not configured.", nameof(shardId));

            _ring.RemoveShard(shardId);
            _placements.Remove(shardId);
        }
    }

    /// <inheritdoc />
    public ShardPlacement GetPlacement(Type entityType, string shardId)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ValidateSqlSafeShardId(shardId);
        lock (_gate)
        {
            return _placements.TryGetValue(shardId, out var placement)
                ? placement
                : throw new ArgumentException($"Shard '{shardId}' is not configured.", nameof(shardId));
        }
    }

    private static void ValidateSqlSafeShardId(string? shardId)
    {
        if (string.IsNullOrEmpty(shardId))
            throw new ArgumentException("Shard ids must not be empty.", nameof(shardId));

        foreach (var character in shardId)
        {
            if (!(character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_'))
            {
                throw new ArgumentException(
                    "Shard ids may contain only ASCII letters, digits, and underscores.", nameof(shardId));
            }
        }
    }

    private sealed class RingView(ConsistentHashRing ring) : IConsistentHashRing
    {
        public int BucketCount => ring.BucketCount;
        public int Version => ring.Version;
        public IReadOnlyList<string> ShardIds => ring.ShardIds;
        public int GetBucket(ReadOnlySpan<byte> key) => ring.GetBucket(key);
        public string GetShard(int bucket) => ring.GetShard(bucket);
    }
}
