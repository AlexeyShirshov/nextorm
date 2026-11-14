using System.Buffers.Binary;
using System.Text;

namespace NextORM.Core;

/// <summary>Maps keys to stable integer buckets and buckets to shard identifiers.</summary>
public interface IConsistentHashRing
{
    /// <summary>Gets the number of stable buckets represented by the ring.</summary>
    int BucketCount { get; }

    /// <summary>Gets the version, incremented whenever shard membership changes.</summary>
    int Version { get; }

    /// <summary>Maps the supplied key bytes to a stable bucket.</summary>
    /// <param name="key">The canonical key bytes.</param>
    /// <returns>A bucket in the range <c>[0, BucketCount)</c>.</returns>
    int GetBucket(ReadOnlySpan<byte> key);

    /// <summary>Gets the shard currently owning a bucket.</summary>
    /// <param name="bucket">A bucket in the range <c>[0, BucketCount)</c>.</param>
    /// <returns>The owning shard identifier.</returns>
    string GetShard(int bucket);

    /// <summary>Gets the configured shard identifiers in ordinal order.</summary>
    IReadOnlyList<string> ShardIds { get; }
}

/// <summary>Implements a deterministic consistent-hash ring with virtual nodes and fixed buckets.</summary>
public sealed class ConsistentHashRing : IConsistentHashRing
{
    private readonly object _gate = new();
    private readonly int _virtualNodesPerShard;
    private string[] _shardIds;
    private IReadOnlyList<string> _readOnlyShardIds;
    private string[] _bucketOwners;
    private int _version;

    /// <summary>Creates a ring over a non-empty set of shard identifiers.</summary>
    /// <param name="shardIds">The initial shard identifiers.</param>
    /// <param name="virtualNodesPerShard">The number of virtual nodes created for each shard.</param>
    /// <param name="bucketCount">The number of stable buckets exposed by the ring.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shardIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="shardIds"/> is empty, contains an empty id, or contains duplicates.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="virtualNodesPerShard"/> or <paramref name="bucketCount"/> is not positive.</exception>
    public ConsistentHashRing(
        IEnumerable<string> shardIds,
        int virtualNodesPerShard = 128,
        int bucketCount = 4096)
    {
        ArgumentNullException.ThrowIfNull(shardIds);
        ValidateConfiguration(virtualNodesPerShard, bucketCount);

        _virtualNodesPerShard = virtualNodesPerShard;
        _shardIds = ValidateShardIds(shardIds, allowEmpty: false);
        _readOnlyShardIds = Array.AsReadOnly(_shardIds);
        BucketCount = bucketCount;
        (_, _bucketOwners) = BuildRingState(_shardIds);
    }

    internal ConsistentHashRing(int virtualNodesPerShard, int bucketCount)
    {
        ValidateConfiguration(virtualNodesPerShard, bucketCount);
        _virtualNodesPerShard = virtualNodesPerShard;
        _shardIds = [];
        _readOnlyShardIds = Array.AsReadOnly(_shardIds);
        _bucketOwners = [];
        BucketCount = bucketCount;
    }

    /// <inheritdoc />
    public int BucketCount { get; }

    /// <inheritdoc />
    public int Version
    {
        get
        {
            lock (_gate)
                return _version;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ShardIds
    {
        get
        {
            lock (_gate)
                return _readOnlyShardIds;
        }
    }

    /// <summary>Adds a shard and rebuilds the ring using the same deterministic hash basis.</summary>
    /// <param name="shardId">The identifier to add.</param>
    /// <exception cref="ArgumentException"><paramref name="shardId"/> is empty or already configured.</exception>
    public void AddShard(string shardId)
    {
        ValidateShardId(shardId, nameof(shardId));
        lock (_gate)
        {
            if (Array.IndexOf(_shardIds, shardId) >= 0)
                throw new ArgumentException($"Shard '{shardId}' is already configured.", nameof(shardId));

            var updated = new string[_shardIds.Length + 1];
            Array.Copy(_shardIds, updated, _shardIds.Length);
            updated[^1] = shardId;
            Array.Sort(updated, StringComparer.Ordinal);
            ApplyShardIds(updated);
        }
    }

    /// <summary>Removes a shard and rebuilds the ring.</summary>
    /// <param name="shardId">The identifier to remove.</param>
    /// <exception cref="ArgumentException">The identifier is empty, absent, or its removal would leave no shards.</exception>
    public void RemoveShard(string shardId)
    {
        ValidateShardId(shardId, nameof(shardId));
        lock (_gate)
        {
            var index = Array.IndexOf(_shardIds, shardId);
            if (index < 0)
                throw new ArgumentException($"Shard '{shardId}' is not configured.", nameof(shardId));
            if (_shardIds.Length == 1)
                throw new ArgumentException("A ring must contain at least one shard.", nameof(shardId));

            var updated = new string[_shardIds.Length - 1];
            if (index > 0)
                Array.Copy(_shardIds, 0, updated, 0, index);
            if (index < _shardIds.Length - 1)
                Array.Copy(_shardIds, index + 1, updated, index, _shardIds.Length - index - 1);
            ApplyShardIds(updated);
        }
    }

    /// <inheritdoc />
    public int GetBucket(ReadOnlySpan<byte> key)
    {
        lock (_gate)
        {
            if (_shardIds.Length == 0)
                throw new InvalidOperationException("The ring has no configured shards.");

            return (int)(ShardKeyHasher.HashBytes(key) % (uint)BucketCount);
        }
    }

    /// <inheritdoc />
    public string GetShard(int bucket)
    {
        if ((uint)bucket >= (uint)BucketCount)
            throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Bucket must be within the ring's configured range.");

        lock (_gate)
        {
            if (_shardIds.Length == 0)
                throw new InvalidOperationException("The ring has no configured shards.");
            return _bucketOwners[bucket];
        }
    }

    private void ApplyShardIds(string[] shardIds)
    {
        var (nodes, bucketOwners) = BuildRingState(shardIds);
        _shardIds = shardIds;
        _readOnlyShardIds = Array.AsReadOnly(shardIds);
        _bucketOwners = bucketOwners;
        _version = unchecked(_version + 1);
    }

    private (VirtualNode[] Nodes, string[] BucketOwners) BuildRingState(string[] shardIds)
    {
        if (shardIds.Length == 0)
            return ([], []);

        var nodeCount = checked(shardIds.Length * _virtualNodesPerShard);
        var nodes = new VirtualNode[nodeCount];
        var nodeIndex = 0;
        foreach (var shardId in shardIds)
        {
            var shardBytes = Encoding.UTF8.GetBytes(shardId);
            var input = new byte[sizeof(int) + shardBytes.Length + sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(input, shardBytes.Length);
            shardBytes.CopyTo(input.AsSpan(sizeof(int)));
            for (var virtualNode = 0; virtualNode < _virtualNodesPerShard; virtualNode++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(sizeof(int) + shardBytes.Length), virtualNode);
                nodes[nodeIndex++] = new VirtualNode(
                    ShardKeyHasher.HashBytes(input), shardId, virtualNode);
            }
        }

        Array.Sort(nodes, static (left, right) =>
        {
            var byPosition = left.Position.CompareTo(right.Position);
            if (byPosition != 0)
                return byPosition;
            var byShard = StringComparer.Ordinal.Compare(left.ShardId, right.ShardId);
            return byShard != 0 ? byShard : left.Index.CompareTo(right.Index);
        });

        var bucketOwners = new string[BucketCount];
        for (var bucket = 0; bucket < bucketOwners.Length; bucket++)
        {
            var position = (ulong)(((UInt128)(uint)bucket << 64) / (uint)BucketCount);
            bucketOwners[bucket] = nodes[FindSuccessor(nodes, position)].ShardId;
        }

        return (nodes, bucketOwners);
    }

    private static int FindSuccessor(VirtualNode[] nodes, ulong position)
    {
        var low = 0;
        var high = nodes.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (nodes[middle].Position < position)
                low = middle + 1;
            else
                high = middle;
        }

        return low == nodes.Length ? 0 : low;
    }

    private static string[] ValidateShardIds(IEnumerable<string> shardIds, bool allowEmpty)
    {
        var result = new List<string>();
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        foreach (var shardId in shardIds)
        {
            ValidateShardId(shardId, nameof(shardIds));
            if (!distinct.Add(shardId))
                throw new ArgumentException($"Shard id '{shardId}' is duplicated.", nameof(shardIds));
            result.Add(shardId);
        }

        if (!allowEmpty && result.Count == 0)
            throw new ArgumentException("At least one shard id is required.", nameof(shardIds));

        result.Sort(StringComparer.Ordinal);
        return [.. result];
    }

    private static void ValidateShardId(string? shardId, string parameterName)
    {
        if (string.IsNullOrEmpty(shardId))
            throw new ArgumentException("Shard ids must not be empty.", parameterName);
    }

    private static void ValidateConfiguration(int virtualNodesPerShard, int bucketCount)
    {
        if (virtualNodesPerShard <= 0)
            throw new ArgumentOutOfRangeException(nameof(virtualNodesPerShard), virtualNodesPerShard, "Virtual node count must be positive.");
        if (bucketCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(bucketCount), bucketCount, "Bucket count must be positive.");
    }

    private readonly record struct VirtualNode(ulong Position, string ShardId, int Index);
}
