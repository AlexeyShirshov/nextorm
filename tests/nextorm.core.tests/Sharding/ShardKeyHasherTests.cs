using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests.Sharding;

public sealed class ShardKeyHasherTests
{
    [Fact]
    public void Hash_ShouldBeStableForSupportedValues()
    {
        object?[] key = [42, 1234567890123L, (short)7, (byte)9, Guid.Parse("58f89f88-1001-4b86-9546-cb0311406fa9"),
            "tenant", true, new DateTime(638500000000000000, DateTimeKind.Utc),
            new DateTimeOffset(2024, 3, 4, 5, 6, 7, TimeSpan.FromHours(2)), 12.34m];

        ShardKeyHasher.Hash(key).Should().Be(ShardKeyHasher.Hash(key));
        ShardKeyHasher.GetCanonicalBytes(key).Should().Equal(ShardKeyHasher.GetCanonicalBytes(key));
    }

    [Fact]
    public void Hash_ShouldLengthPrefixTupleParts()
    {
        var first = new object?[] { "ab", "c" };
        var second = new object?[] { "a", "bc" };

        ShardKeyHasher.GetCanonicalBytes(first).Should().NotEqual(ShardKeyHasher.GetCanonicalBytes(second));
        ShardKeyHasher.Hash(first).Should().NotBe(ShardKeyHasher.Hash(second));
    }

    [Fact]
    public void Hash_ShouldEncodeNullDeterministicallyAndDistinctFromEmptyString()
    {
        ShardKeyHasher.Hash([null]).Should().Be(ShardKeyHasher.Hash([null]));
        ShardKeyHasher.GetCanonicalBytes([null]).Should().NotEqual(ShardKeyHasher.GetCanonicalBytes([""]));
    }
}
