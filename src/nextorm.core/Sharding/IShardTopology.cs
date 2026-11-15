namespace NextORM.Core;

/// <summary>Provides the bucket ring, the placement mode and the configured shards with their connections.</summary>
public interface IShardTopology
{
    /// <summary>Gets the topology version, incremented when shard membership changes.</summary>
    int Version { get; }

    /// <summary>Gets the consistent-hash ring used to route buckets to shards.</summary>
    IConsistentHashRing Ring { get; }

    /// <summary>Gets the configured shard identifiers in ordinal order.</summary>
    IReadOnlyList<string> ShardIds { get; }

    /// <summary>Gets how a bucket is realized physically.</summary>
    BucketPlacementMode PlacementMode { get; }

    /// <summary>Gets the connection string of a configured shard (server).</summary>
    /// <param name="shardId">The configured shard identifier.</param>
    /// <returns>The connection string that addresses that server.</returns>
    /// <exception cref="ArgumentException">The shard is not configured.</exception>
    string GetConnectionString(string shardId);
}

/// <summary>Stores the shards (servers) with their connections and maintains the bucket ring.</summary>
public sealed class ShardTopology : IShardTopology
{
    private readonly object _gate = new();
    private readonly ConsistentHashRing _ring;
    private readonly IConsistentHashRing _ringView;
    private readonly Dictionary<string, string> _connections = new(StringComparer.Ordinal);

    /// <summary>Creates an initially empty topology that can be populated with <see cref="AddShard"/>.</summary>
    /// <param name="bucketCount">
    /// The number of stable buckets exposed by the ring. It is chosen by the caller (never defaulted) and
    /// never depends on the shard count.
    /// </param>
    /// <param name="placementMode">How each bucket is realized physically; the names depend on the bucket only.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bucketCount"/> is not positive.</exception>
    public ShardTopology(int bucketCount, BucketPlacementMode placementMode = BucketPlacementMode.TableSuffix)
    {
        _ring = new ConsistentHashRing(bucketCount);
        _ringView = new RingView(_ring);
        PlacementMode = placementMode;
    }

    /// <inheritdoc />
    public int Version => _ring.Version;

    /// <inheritdoc />
    public IConsistentHashRing Ring => _ringView;

    /// <inheritdoc />
    public IReadOnlyList<string> ShardIds => _ring.ShardIds;

    /// <inheritdoc />
    public BucketPlacementMode PlacementMode { get; }

    /// <summary>Adds a shard (server) with the connection string that addresses it.</summary>
    /// <param name="shardId">The shard identifier; it never reaches a SQL identifier.</param>
    /// <param name="connectionString">The connection string of that server.</param>
    /// <exception cref="ArgumentException">The identifier or the connection string is empty, or the shard already exists.</exception>
    public void AddShard(string shardId, string connectionString)
    {
        ValidateShardId(shardId);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A shard connection string must not be empty.", nameof(connectionString));

        lock (_gate)
        {
            if (_connections.ContainsKey(shardId))
                throw new ArgumentException($"Shard '{shardId}' is already configured.", nameof(shardId));

            _ring.AddShard(shardId);
            _connections.Add(shardId, connectionString);
        }
    }

    /// <summary>Removes a configured shard; its buckets are routed to the remaining shards.</summary>
    /// <param name="shardId">The shard identifier to remove.</param>
    /// <exception cref="ArgumentException">The shard is not configured, or its removal would leave no shards.</exception>
    public void RemoveShard(string shardId)
    {
        ValidateShardId(shardId);
        lock (_gate)
        {
            if (!_connections.ContainsKey(shardId))
                throw new ArgumentException($"Shard '{shardId}' is not configured.", nameof(shardId));

            _ring.RemoveShard(shardId);
            _connections.Remove(shardId);
        }
    }

    /// <inheritdoc />
    public string GetConnectionString(string shardId)
    {
        ValidateShardId(shardId);
        lock (_gate)
        {
            return _connections.TryGetValue(shardId, out var connectionString)
                ? connectionString
                : throw new ArgumentException($"Shard '{shardId}' is not configured.", nameof(shardId));
        }
    }

    private static void ValidateShardId(string? shardId)
    {
        if (string.IsNullOrWhiteSpace(shardId))
            throw new ArgumentException("Shard ids must not be empty.", nameof(shardId));
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
