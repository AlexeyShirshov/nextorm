using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Cover for <see cref="XxHash32"/> (xxHash32 port). The type seeds itself from
/// <see cref="System.Security.Cryptography.RandomNumberGenerator"/>, so absolute hash values are not
/// stable across processes: the tests only assert stability (equal inputs =&gt; equal hash) and that
/// different inputs are not treated as equal.
/// </summary>
public class XxHash32Tests
{
    [Fact]
    public void Combine_One_ShouldBeStable()
    {
        XxHash32.Combine(1).Should().Be(XxHash32.Combine(1));
        XxHash32.Combine("value").Should().Be(XxHash32.Combine("value"));
        XxHash32.Combine<int?>(null).Should().Be(XxHash32.Combine<int?>(null));
        XxHash32.Combine(1).Should().NotBe(XxHash32.Combine(2));
    }

    [Fact]
    public void Combine_Two_ShouldBeStable()
    {
        XxHash32.Combine(1, "a").Should().Be(XxHash32.Combine(1, "a"));
        XxHash32.Combine<int?, string?>(null, null).Should().Be(XxHash32.Combine<int?, string?>(null, null));
        XxHash32.Combine(1, "a").Should().NotBe(XxHash32.Combine(2, "a"));
    }

    [Fact]
    public void Combine_Three_ShouldBeStable()
    {
        XxHash32.Combine(1, "a", 2L).Should().Be(XxHash32.Combine(1, "a", 2L));
        XxHash32.Combine<int?, string?, long?>(null, "a", null).Should().Be(XxHash32.Combine<int?, string?, long?>(null, "a", null));
        XxHash32.Combine(1, "a", 2L).Should().NotBe(XxHash32.Combine(1, "b", 2L));
    }

    [Fact]
    public void Combine_Four_ShouldBeStable()
    {
        XxHash32.Combine(1, "a", 2L, 3.5).Should().Be(XxHash32.Combine(1, "a", 2L, 3.5));
        XxHash32.Combine<int?, string?, long?, double?>(null, null, null, null)
            .Should().Be(XxHash32.Combine<int?, string?, long?, double?>(null, null, null, null));
        XxHash32.Combine(1, "a", 2L, 3.5).Should().NotBe(XxHash32.Combine(1, "a", 2L, 4.5));
    }

    [Fact]
    public void Combine_Five_ShouldBeStable()
    {
        XxHash32.Combine(1, "a", 2L, 3.5, true).Should().Be(XxHash32.Combine(1, "a", 2L, 3.5, true));
        XxHash32.Combine(1, "a", 2L, 3.5, true).Should().NotBe(XxHash32.Combine(1, "a", 2L, 3.5, false));
    }

    [Fact]
    public void Combine_Six_ShouldBeStable()
    {
        XxHash32.Combine(1, "a", 2L, 3.5, true, 'c').Should().Be(XxHash32.Combine(1, "a", 2L, 3.5, true, 'c'));
        XxHash32.Combine(1, "a", 2L, 3.5, true, 'c').Should().NotBe(XxHash32.Combine(1, "a", 2L, 3.5, true, 'd'));
    }

    [Fact]
    public void Combine_Seven_ShouldBeStable()
    {
        XxHash32.Combine(1, "a", 2L, 3.5, true, 'c', (byte)7).Should().Be(XxHash32.Combine(1, "a", 2L, 3.5, true, 'c', (byte)7));
        XxHash32.Combine(1, "a", 2L, 3.5, true, 'c', (byte)7).Should().NotBe(XxHash32.Combine(1, "a", 2L, 3.5, true, 'c', (byte)8));
    }

    [Fact]
    public void Combine_Eight_ShouldBeStable()
    {
        XxHash32.Combine(1, "a", 2L, 3.5, true, 'c', (byte)7, Guid.Empty).Should().Be(XxHash32.Combine(1, "a", 2L, 3.5, true, 'c', (byte)7, Guid.Empty));
        XxHash32.Combine<int?, string?, long?, double?, bool?, char?, byte?, Guid?>(null, null, null, null, null, null, null, null)
            .Should().Be(XxHash32.Combine<int?, string?, long?, double?, bool?, char?, byte?, Guid?>(null, null, null, null, null, null, null, null));
        XxHash32.Combine(1, "a", 2L, 3.5, true, 'c', (byte)7, "x").Should().NotBe(XxHash32.Combine(1, "a", 2L, 3.5, true, 'c', (byte)7, "y"));
    }

    [Fact]
    public void Add_Generic_ShouldBeStableAndNullSafe()
    {
        var x = new XxHash32();
        x.Add(1);
        x.Add("a");
        x.Add<int?>(null);

        var y = new XxHash32();
        y.Add(1);
        y.Add("a");
        y.Add<int?>(null);

        x.ToHashCode().Should().Be(y.ToHashCode());

        var z = new XxHash32();
        z.Add(1);
        z.Add("a");
        z.Add(2);

        x.ToHashCode().Should().NotBe(z.ToHashCode());
    }

    [Fact]
    public void Add_WithComparer_ShouldUseComparer()
    {
        var comparer = new ConstHashComparer();

        var x = new XxHash32();
        x.Add<string>("a", comparer);
        x.Add(2);

        var y = new XxHash32();
        y.Add<string>("b", comparer);
        y.Add(2);

        x.ToHashCode().Should().Be(y.ToHashCode(), "the comparer maps every value to the same hash");

        var withNullComparer = new XxHash32();
        withNullComparer.Add<string>("a", null);
        var expected = new XxHash32();
        expected.Add("a");

        withNullComparer.ToHashCode().Should().Be(expected.ToHashCode());

        var nullValue = new XxHash32();
        nullValue.Add<string>(null, comparer);

        var nullExpected = new XxHash32();
        nullExpected.Add(0);

        nullValue.ToHashCode().Should().Be(nullExpected.ToHashCode());
    }

    [Fact]
    public void AddBytes_ShortSpans_ShouldBeStable()
    {
        for (var length = 0; length < 16; length++)
        {
            var bytes = CreateBytes(length);

            var x = new XxHash32();
            x.AddBytes(bytes);

            var y = new XxHash32();
            y.AddBytes(CreateBytes(length));

            x.ToHashCode().Should().Be(y.ToHashCode(), "spans of {0} bytes must hash equally", length);
        }
    }

    [Fact]
    public void AddBytes_LongSpans_ShouldBeStable()
    {
        foreach (var length in new[] { 16, 17, 20, 31, 32, 33, 63, 64, 65, 128 })
        {
            var x = new XxHash32();
            x.AddBytes(CreateBytes(length));

            var y = new XxHash32();
            y.AddBytes(CreateBytes(length));

            x.ToHashCode().Should().Be(y.ToHashCode(), "spans of {0} bytes must hash equally", length);
        }
    }

    [Fact]
    public void AddBytes_AfterAdd_ShouldFlushQueuedValues()
    {
        // Every remainder of the internal queue length (0..3) must be flushed before the 16-byte blocks.
        for (var prelude = 0; prelude < 4; prelude++)
        {
            var x = new XxHash32();
            var y = new XxHash32();

            for (var i = 0; i < prelude; i++)
            {
                x.Add(i);
                y.Add(i);
            }

            x.AddBytes(CreateBytes(20));
            y.AddBytes(CreateBytes(20));

            x.ToHashCode().Should().Be(y.ToHashCode(), "queue length {0} must be flushed", prelude);
        }
    }

    [Fact]
    public void AddBytes_ShouldDifferForDifferentContent()
    {
        var x = new XxHash32();
        x.AddBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });

        var y = new XxHash32();
        y.AddBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 17 });

        x.ToHashCode().Should().NotBe(y.ToHashCode());
    }

    [Fact]
    public void Add_Int_MultipleValues_ShouldBeStable()
    {
        var x = new XxHash32();
        var y = new XxHash32();

        for (var i = 0; i < 9; i++)
        {
            x.Add(i);
            y.Add(i);
        }

        x.ToHashCode().Should().Be(y.ToHashCode());
        x.ToHashCode().Should().NotBe(new XxHash32().ToHashCode());
    }

    [Fact]
    public void ToHashCode_BeforeAnyAdd_ShouldBeStable()
    {
        new XxHash32().ToHashCode().Should().Be(new XxHash32().ToHashCode());
    }

    private static byte[] CreateBytes(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)i;
        }

        return bytes;
    }

    private sealed class ConstHashComparer : IEqualityComparer<string?>
    {
        public bool Equals(string? x, string? y) => true;

        public int GetHashCode(string? obj) => 42;
    }
}
