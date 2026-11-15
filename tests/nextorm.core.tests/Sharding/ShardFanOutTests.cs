using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests.Sharding;

/// <summary>
/// Covers the bucket fan-out surface that does not need a database: configuration guards, the per-bucket
/// result shape and the per-bucket scalar terminals.
/// </summary>
public sealed class ShardFanOutTests
{
    [Fact]
    public void AcrossBuckets_WithoutShardRouter_ShouldThrow()
    {
        using var context = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

        FluentActions.Invoking(() => context.From<UnshardedRow>().AcrossBuckets())
            .Should().Throw<InvalidOperationException>().WithMessage("*shard router*");
    }

    [Fact]
    public void AcrossBuckets_WithEmptyTopology_ShouldThrow()
    {
        using var context = CreateContext(new ShardTopology(8));

        FluentActions.Invoking(() => context.From<UnshardedRow>().AcrossBuckets())
            .Should().Throw<InvalidOperationException>().WithMessage("*no configured shards*");
    }

    [Fact]
    public void AcrossBuckets_ShouldExposeEveryBucket()
    {
        using var context = CreateContext(CreateTopology());

        var query = context.From<UnshardedRow>().AcrossBuckets();

        query.Buckets.Should().Equal(0, 1, 2, 3, 4, 5, 6, 7);
    }

    [Fact]
    public void UseSharding_WithCustomHashes_ShouldRouteThroughTheCustomRing()
    {
        // A custom topology is the injection point for user hash functions: the key always lands in bucket 7
        // and bucket 7 is always owned by "server-b", whatever the built-in functions would have decided.
        var ring = new ConsistentHashRing(
            shardIds: ["server-a", "server-b"],
            bucketCount: 8,
            bucketHash: static (_, _) => 7,
            shardHash: static (_, shardId) => shardId == "server-b" ? 1UL : 0UL);
        var topology = new CustomTopology(ring, "server-a", "server-b");
        using var context = CreateContext(topology);

        var router = new ShardRouter(topology);

        router.ResolveShard(typeof(ShardedRow), ["any-key"]).Key.Should().Be("7");
        router.GetShard(7).Should().Be("server-b");
        context.From<ShardedRow>().AcrossBuckets().Buckets.Should().HaveCount(8);
    }

    private static ShardTopology CreateTopology()
    {
        var topology = new ShardTopology(8);
        topology.AddShard("server-a", "Server=server-a");
        topology.AddShard("server-b", "Server=server-b");
        return topology;
    }

    private static SqliteDataContext CreateContext(IShardTopology topology)
        => new("Data Source=:memory:", new DataContextBuilder().UseSharding(topology));

    private sealed class UnshardedRow
    {
        public int Id { get; set; }
    }

    [ShardKey(nameof(Key))]
    private sealed class ShardedRow
    {
        public string Key { get; set; } = string.Empty;
    }

    /// <summary>
    /// A topology over a caller-supplied ring: the seam through which a user plugs in custom hash functions.
    /// </summary>
    private sealed class CustomTopology(IConsistentHashRing ring, params string[] shardIds) : IShardTopology
    {
        public string Name => "custom";

        public int Version => ring.Version;

        public IConsistentHashRing Ring => ring;

        public IReadOnlyList<string> ShardIds { get; } = shardIds;

        public BucketPlacementMode PlacementMode => BucketPlacementMode.TableSuffix;

        public string GetConnectionString(string shardId) => $"Server={shardId}";

        public bool TryGetHost(in TopologyContext context, out HostTarget host)
        {
            host = default;
            if (context.Scope == global::NextORM.Core.MappingScope.Default || string.IsNullOrEmpty(context.Scope.Key))
                return false;
            if (!int.TryParse(context.Scope.Key, out var bucket))
                return false;

            var shardId = ring.GetShard(bucket);
            host = new HostTarget(shardId, GetConnectionString(shardId));
            return true;
        }
    }
}
