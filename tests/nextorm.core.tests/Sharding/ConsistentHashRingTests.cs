using System.Buffers.Binary;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.Sharding;

public sealed class ConsistentHashRingTests
{
    [Fact]
    public void RingInstances_ShouldRouteTheSameKeysDeterministically()
    {
        var shardIds = new[] { "north", "south", "west" };
        var first = new ConsistentHashRing(shardIds);
        var second = new ConsistentHashRing(shardIds);

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
        var ring = new ConsistentHashRing(["aa", "bb", "cc"]);
        var actual = new string[10];
        for (var key = 0; key < actual.Length; key++)
            actual[key] = ring.GetShard(ring.GetBucket(KeyBytes(key)));

        actual.Should().Equal("bb", "bb", "bb", "aa", "bb", "bb", "bb", "cc", "bb", "cc");
    }

    [Fact]
    public void Ring_ShouldReachEveryConfiguredShard()
    {
        var ring = new ConsistentHashRing(["a", "b", "c", "d"]);
        var reached = new HashSet<string>(StringComparer.Ordinal);

        for (var key = 0; key < 10_000; key++)
            reached.Add(ring.GetShard(ring.GetBucket(KeyBytes(key))));

        reached.Should().BeEquivalentTo(ring.ShardIds);
    }

    [Fact]
    public void AddingAShard_ShouldMoveAtMostTwoOverShardCountKeysOnUniformSample()
    {
        const int existingShardCount = 4;
        const int sampleSize = 10_000;
        var before = new ConsistentHashRing(["a", "b", "c", "d"]);
        var after = new ConsistentHashRing(["a", "b", "c", "d"]);
        after.AddShard("e");
        var moved = 0;

        for (var key = 0; key < sampleSize; key++)
        {
            var bytes = KeyBytes(key);
            var bucket = before.GetBucket(bytes);
            var updatedBucket = after.GetBucket(bytes);
            if (before.GetShard(bucket) != after.GetShard(updatedBucket))
                moved++;
        }

        ((double)moved / sampleSize).Should().BeLessThanOrEqualTo(2d / (existingShardCount + 1));
    }

    [Fact]
    public void MembershipChanges_ShouldBumpVersionAndPreserveSortedShardIds()
    {
        var ring = new ConsistentHashRing(["b", "a"]);

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
        FluentActions.Invoking(() => new ConsistentHashRing(Array.Empty<string>()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ConsistentHashRing(["a", "a"]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ConsistentHashRing([""]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ConsistentHashRing(["a"], virtualNodesPerShard: 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new ConsistentHashRing(["a"], bucketCount: 0))
            .Should().Throw<ArgumentOutOfRangeException>();

        var ring = new ConsistentHashRing(["a"]);
        FluentActions.Invoking(() => ring.AddShard("a")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ring.RemoveShard("missing")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ring.RemoveShard("a")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ring.GetShard(-1)).Should().Throw<ArgumentOutOfRangeException>();
    }

    private static byte[] KeyBytes(int key)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, key);
        return bytes;
    }
}
