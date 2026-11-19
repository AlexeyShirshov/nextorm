using System.Buffers.Binary;
using System.Text;

namespace NextORM.Core;

/// <summary>Maps keys to stable buckets and buckets to shard identifiers.</summary>
public interface IConsistentHashRing
{
    /// <summary>Gets the fixed number of buckets; it never depends on the shard count.</summary>
    int BucketCount { get; }

    /// <summary>Gets the ring version, incremented whenever shard membership changes.</summary>
    int Version { get; }

    /// <summary>Gets the configured shard identifiers in ordinal order.</summary>
    IReadOnlyList<string> ShardIds { get; }

    /// <summary>Maps the supplied key bytes to a stable bucket.</summary>
    /// <param name="key">The canonical key bytes.</param>
    /// <returns>A bucket in the range <c>[0, BucketCount)</c>.</returns>
    /// <exception cref="InvalidOperationException">The ring has no configured shards.</exception>
    int GetBucket(ReadOnlySpan<byte> key);

    /// <summary>Gets the shard currently owning a bucket.</summary>
    /// <param name="bucket">A bucket in the range <c>[0, BucketCount)</c>.</param>
    /// <returns>The owning shard identifier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The bucket is outside the configured range.</exception>
    /// <exception cref="InvalidOperationException">The ring has no configured shards.</exception>
    string GetShard(int bucket);
}

/// <summary>Maps canonical shard-key bytes to a bucket index.</summary>
/// <param name="key">The canonical key bytes.</param>
/// <param name="bucketCount">The ring's configured bucket count.</param>
/// <returns>A bucket index in the range <c>[0, bucketCount)</c>.</returns>
public delegate int BucketHash(ReadOnlySpan<byte> key, int bucketCount);

/// <summary>Scores one <paramref name="bucket"/> / <paramref name="shardId"/> pair for rendezvous hashing.</summary>
/// <param name="bucket">The bucket being assigned.</param>
/// <param name="shardId">The candidate shard identifier.</param>
/// <returns>The score; the highest score among the configured shards wins the bucket.</returns>
public delegate ulong ShardHash(int bucket, string shardId);

/// <summary>
/// Maps buckets to shards with rendezvous hashing: adding a shard takes over only the buckets it wins, and
/// removing a shard returns its buckets to their previous owners. No virtual nodes are involved.
/// </summary>
public sealed class ConsistentHashRing : IConsistentHashRing
{
    private readonly object _gate = new();
    private readonly int _bucketCount;
    private readonly BucketHash _bucketHash;
    private readonly ShardHash _shardHash;
    private string[] _shardIds;
    private IReadOnlyList<string> _readOnlyShardIds;
    private string[] _bucketOwners;
    private int _version;

    /// <summary>
    /// Creates a ring over a non-empty set of shard identifiers. The bucket count is chosen by the caller and
    /// never defaulted; a custom hash function may replace either stage, but it must be deterministic across
    /// processes and restarts, because routing is stored in the data and not in the process.
    /// </summary>
    /// <param name="shardIds">The initial shard identifiers.</param>
    /// <param name="bucketCount">The number of stable buckets exposed by the ring.</param>
    /// <param name="bucketHash">The key-to-bucket hash, or <see langword="null"/> for the built-in FNV-1a selector.</param>
    /// <param name="shardHash">The bucket-to-shard score, or <see langword="null"/> for the built-in avalanche score.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shardIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="shardIds"/> is empty, contains an empty id, or contains duplicates.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bucketCount"/> is not positive.</exception>
    public ConsistentHashRing(
        IEnumerable<string> shardIds,
        int bucketCount,
        BucketHash? bucketHash = null,
        ShardHash? shardHash = null)
    {
        ArgumentNullException.ThrowIfNull(shardIds);
        ValidateBucketCount(bucketCount);

        _bucketCount = bucketCount;
        _bucketHash = bucketHash ?? DefaultBucketHash;
        _shardHash = shardHash ?? DefaultShardHash;
        _shardIds = ValidateShardIds(shardIds, allowEmpty: false);
        _readOnlyShardIds = Array.AsReadOnly(_shardIds);
        _bucketOwners = BuildBucketOwners(_shardIds);
    }

    internal ConsistentHashRing(int bucketCount, BucketHash? bucketHash = null, ShardHash? shardHash = null)
    {
        ValidateBucketCount(bucketCount);

        _bucketCount = bucketCount;
        _bucketHash = bucketHash ?? DefaultBucketHash;
        _shardHash = shardHash ?? DefaultShardHash;
        _shardIds = [];
        _readOnlyShardIds = Array.AsReadOnly(_shardIds);
        _bucketOwners = [];
    }

    /// <inheritdoc />
    public int BucketCount => _bucketCount;

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

    /// <summary>Adds a shard; it takes over only the buckets it wins in the rendezvous hash.</summary>
    /// <param name="shardId">The identifier to add.</param>
    /// <exception cref="ArgumentException">The identifier is empty or already configured.</exception>
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

    /// <summary>Removes a shard; its buckets return to the shards that owned them before it was added.</summary>
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

            var bucket = _bucketHash(key, _bucketCount);
            if ((uint)bucket >= (uint)_bucketCount)
            {
                throw new InvalidOperationException(
                    $"The configured bucket hash returned {bucket}, which is outside [0, {_bucketCount}).");
            }

            return bucket;
        }
    }

    /// <inheritdoc />
    public string GetShard(int bucket)
    {
        if ((uint)bucket >= (uint)_bucketCount)
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
        var bucketOwners = BuildBucketOwners(shardIds);
        _shardIds = shardIds;
        _readOnlyShardIds = Array.AsReadOnly(shardIds);
        _bucketOwners = bucketOwners;
        _version = unchecked(_version + 1);
    }

    private string[] BuildBucketOwners(string[] shardIds)
    {
        if (shardIds.Length == 0)
            return [];

        var owners = new string[_bucketCount];
        for (var bucket = 0; bucket < _bucketCount; bucket++)
        {
            var bestShard = shardIds[0];
            var bestScore = _shardHash(bucket, bestShard);
            for (var index = 1; index < shardIds.Length; index++)
            {
                var score = _shardHash(bucket, shardIds[index]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestShard = shardIds[index];
                }
            }

            owners[bucket] = bestShard;
        }

        return owners;
    }

    private static int DefaultBucketHash(ReadOnlySpan<byte> key, int bucketCount)
        => (int)(ShardKeyHasher.HashBytes(key) % (uint)bucketCount);

    private static ulong DefaultShardHash(int bucket, string shardId)
    {
        var byteCount = Encoding.UTF8.GetByteCount(shardId);
        Span<byte> input = byteCount <= 256
            ? stackalloc byte[sizeof(int) + byteCount]
            : new byte[sizeof(int) + byteCount];
        BinaryPrimitives.WriteInt32LittleEndian(input, bucket);
        Encoding.UTF8.GetBytes(shardId, input[sizeof(int)..]);
        return Mix(ShardKeyHasher.HashBytes(input));
    }

    /// <summary>
    /// Finalizes the hash with a 64-bit avalanche (murmur3 <c>fmix64</c>). Rendezvous hashing compares scores
    /// across shard ids for the same bucket, so weak diffusion in the raw hash would let one shard win
    /// systematically and skew the ownership split.
    /// </summary>
    private static ulong Mix(ulong value)
    {
        value ^= value >> 33;
        value *= 0xFF51AFD7ED558CCDUL;
        value ^= value >> 33;
        value *= 0xC4CEB9FE1A85EC53UL;
        value ^= value >> 33;
        return value;
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

    private static void ValidateBucketCount(int bucketCount)
    {
        if (bucketCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(bucketCount), bucketCount, "Bucket count must be positive.");
    }
}
