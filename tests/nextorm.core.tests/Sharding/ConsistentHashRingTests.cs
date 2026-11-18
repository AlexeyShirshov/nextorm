using System.Buffers.Binary;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.Sharding;

public sealed class ConsistentHashRingTests
{
    private const int BucketCount = 4096;

    [Fact]
    public void RingInstances_ShouldRouteTheSameKeysDeterministically()
    {
        var shardIds = new[] { "north", "south", "west" };
        var first = new ConsistentHashRing(shardIds, BucketCount);
        var second = new ConsistentHashRing(shardIds, BucketCount);

        for (var key = 0; key < 1_000; key++)
        {
            var bytes = KeyBytes(key);
            var bucket = first.GetBucket(bytes);
            second.GetBucket(bytes).Should().Be(bucket);
            second.GetShard(bucket).Should().Be(first.GetShard(bucket));
        }
    }

    [Fact]
    public void Ring_ShouldMatchPinnedCrossProcessShardIds()
    {
        // Golden values: a ring built from the same shard ids must produce these shard ids in every
        // process, which is what makes shard routing stable across application restarts.
        var ring = new ConsistentHashRing(["aa", "bb", "cc"], BucketCount);
        var actual = new string[10];
        for (var key = 0; key < actual.Length; key++)
            actual[key] = ring.GetShard(ring.GetBucket(KeyBytes(key)));

        actual.Should().Equal("aa", "bb", "aa", "cc", "cc", "aa", "bb", "cc", "aa", "aa");
    }

    [Fact]
    public void Ring_ShouldReachEveryConfiguredShard()
    {
        var ring = new ConsistentHashRing(["a", "b", "c", "d"], BucketCount);
        var reached = new HashSet<string>(StringComparer.Ordinal);

        for (var bucket = 0; bucket < ring.BucketCount; bucket++)
            reached.Add(ring.GetShard(bucket));

        reached.Should().BeEquivalentTo(ring.ShardIds);
    }

    [Fact]
    public void GetBucket_ShouldStayWithinTheConfiguredBucketCount()
    {
        var ring = new ConsistentHashRing(["a", "b"], BucketCount);

        for (var key = 0; key < 1_000; key++)
            ring.GetBucket(KeyBytes(key)).Should().BeInRange(0, BucketCount - 1);
    }

    [Fact]
    public void TwoShards_ShouldSplitBucketsEvenly()
    {
        var ring = new ConsistentHashRing(["server-a", "server-b"], BucketCount);

        foreach (var share in Shares(ring).Values)
            share.Should().BeInRange(45, 55);
    }

    [Fact]
    public void ThreeShards_ShouldSplitBucketsEvenly()
    {
        var ring = new ConsistentHashRing(["server-a", "server-b", "server-c"], BucketCount);

        foreach (var share in Shares(ring).Values)
            share.Should().BeInRange(28, 39);
    }

    [Fact]
    public void AddingAShard_ShouldMoveOnlyThatShardsBuckets()
    {
        var ring = new ConsistentHashRing(["a", "b"], BucketCount);
        var before = Snapshot(ring);

        ring.AddShard("c");

        var moved = 0;
        for (var bucket = 0; bucket < ring.BucketCount; bucket++)
        {
            if (ring.GetShard(bucket) == before[bucket])
                continue;

            ring.GetShard(bucket).Should().Be("c");
            moved++;
        }

        moved.Should().BeGreaterThan(0);
        (100.0 * moved / ring.BucketCount).Should().BeLessThanOrEqualTo(100.0 / 3 + 5);
    }

    [Fact]
    public void RemovingAShard_ShouldRestorePreviousOwners()
    {
        var ring = new ConsistentHashRing(["a", "b"], BucketCount);
        var before = Snapshot(ring);

        ring.AddShard("c");
        ring.RemoveShard("c");

        Snapshot(ring).Should().Equal(before);
    }

    [Fact]
    public void AddOrder_ShouldNotAffectTheMapping()
    {
        var ascending = new ConsistentHashRing(["a", "b", "c"], BucketCount);
        var shuffled = new ConsistentHashRing(
            bucketCount: BucketCount,
            bucketHash: null,
            shardHash: null);
        shuffled.AddShard("c");
        shuffled.AddShard("a");
        shuffled.AddShard("b");

        Snapshot(shuffled).Should().Equal(Snapshot(ascending));
    }

    [Fact]
    public void MembershipChanges_ShouldBumpVersionAndPreserveSortedShardIds()
    {
        var ring = new ConsistentHashRing(["b", "a"], BucketCount);

        ring.Version.Should().Be(0);
        ring.ShardIds.Should().Equal("a", "b");
        ring.AddShard("c");
        ring.Version.Should().Be(1);
        ring.RemoveShard("a");
        ring.Version.Should().Be(2);
        ring.ShardIds.Should().Equal("b", "c");
    }

    [Fact]
    public void ConstructorAndMembership_ShouldRejectInvalidInputs()
    {
        FluentActions.Invoking(() => new ConsistentHashRing(Array.Empty<string>(), BucketCount))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ConsistentHashRing(["a", "a"], BucketCount))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ConsistentHashRing([""], BucketCount))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ConsistentHashRing(["a"], bucketCount: 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new ConsistentHashRing(bucketCount: 0))
            .Should().Throw<ArgumentOutOfRangeException>();

        var ring = new ConsistentHashRing(["a"], BucketCount);
        FluentActions.Invoking(() => ring.AddShard("a")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ring.AddShard("")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ring.RemoveShard("missing")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ring.RemoveShard("a")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ring.GetShard(-1)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ring.GetShard(BucketCount)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EmptyRing_ShouldFailBeforeRouting()
    {
        var ring = new ConsistentHashRing(BucketCount);

        FluentActions.Invoking(() => ring.GetBucket(KeyBytes(1)))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ring.GetShard(0))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CustomBucketHash_ShouldDriveTheBucketSelection()
    {
        var ring = new ConsistentHashRing(
            ["server-a"],
            bucketCount: 16,
            bucketHash: static (key, bucketCount) => key[0] % bucketCount);

        ring.GetBucket([7]).Should().Be(7);
        ring.GetBucket([23]).Should().Be(7);
    }

    [Fact]
    public void CustomBucketHash_ShouldRejectResultsOutsideTheRange()
    {
        var ring = new ConsistentHashRing(["server-a"], bucketCount: 16, bucketHash: static (_, _) => 16);

        FluentActions.Invoking(() => ring.GetBucket([1]))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*outside*");
    }

    [Fact]
    public void CustomShardHash_ShouldDriveTheOwnership()
    {
        var ring = new ConsistentHashRing(
            ["even", "odd"],
            bucketCount: 16,
            shardHash: static (bucket, shardId) =>
                shardId == (bucket % 2 == 0 ? "even" : "odd") ? 1UL : 0UL);

        for (var bucket = 0; bucket < ring.BucketCount; bucket++)
            ring.GetShard(bucket).Should().Be(bucket % 2 == 0 ? "even" : "odd");
    }

    [Fact]
    public void CustomHashes_ShouldStayDeterministicAcrossInstances()
    {
        var bucketHash = new BucketHash(static (key, bucketCount) => (key[0] + key.Length) % bucketCount);
        var shardHash = new ShardHash(static (bucket, shardId) => (ulong)(bucket * 31 + shardId.Length));

        var first = new ConsistentHashRing(["server-a", "server-b"], 64, bucketHash, shardHash);
        var second = new ConsistentHashRing(["server-b", "server-a"], 64, bucketHash, shardHash);

        Snapshot(second).Should().Equal(Snapshot(first));
    }

    private static Dictionary<string, double> Shares(ConsistentHashRing ring)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var bucket = 0; bucket < ring.BucketCount; bucket++)
        {
            var owner = ring.GetShard(bucket);
            counts[owner] = counts.GetValueOrDefault(owner) + 1;
        }

        return counts.ToDictionary(
            pair => pair.Key,
            pair => 100.0 * pair.Value / ring.BucketCount,
            StringComparer.Ordinal);
    }

    private static string[] Snapshot(ConsistentHashRing ring)
    {
        var owners = new string[ring.BucketCount];
        for (var bucket = 0; bucket < ring.BucketCount; bucket++)
            owners[bucket] = ring.GetShard(bucket);

        return owners;
    }

    private static byte[] KeyBytes(int key)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, key);
        return bytes;
    }
}
