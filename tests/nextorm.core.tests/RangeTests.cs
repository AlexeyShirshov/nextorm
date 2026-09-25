using FluentAssertions;
using System.Globalization;

namespace NextORM.Core.Tests;

/// <summary>Value semantics of the provider-agnostic <see cref="Range{T}"/>.</summary>
public class RangeTests
{
    [Fact]
    public void Empty_ShouldBeEmptyWithoutInfiniteBounds()
    {
        var empty = Range<int>.Empty;

        empty.IsEmpty.Should().BeTrue();
        empty.LowerInfinite.Should().BeFalse();
        empty.UpperInfinite.Should().BeFalse();
        empty.Lower.Should().BeNull();
        empty.Upper.Should().BeNull();
    }

    [Fact]
    public void Default_ShouldBeEmpty()
    {
        default(Range<int>).IsEmpty.Should().BeTrue();
        default(Range<int>).LowerInfinite.Should().BeFalse();
        default(Range<int>).UpperInfinite.Should().BeFalse();
        default(Range<int>).Should().Be(Range<int>.Empty);
    }

    [Fact]
    public void Unbounded_ShouldBeNonEmptyWithInfiniteBounds()
    {
        var unbounded = new Range<int>(0, 0, lowerInclusive: true, upperInclusive: false, lowerInfinite: true, upperInfinite: true);

        unbounded.IsEmpty.Should().BeFalse();
        unbounded.LowerInfinite.Should().BeTrue();
        unbounded.UpperInfinite.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldDeriveEmptinessFromBounds()
    {
        new Range<int>(5, 5).IsEmpty.Should().BeTrue();
        new Range<int>(5, 5, lowerInclusive: true, upperInclusive: true).IsEmpty.Should().BeFalse();
        new Range<int>(10, 5).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Bounds_ShouldPreserveInclusivityAndInfinite()
    {
        var range = new Range<int>(1, 10, lowerInclusive: false, upperInclusive: true);

        range.Lower.Should().Be(1);
        range.Upper.Should().Be(10);
        range.LowerInclusive.Should().BeFalse();
        range.UpperInclusive.Should().BeTrue();
        range.LowerInfinite.Should().BeFalse();
        range.UpperInfinite.Should().BeFalse();

        var unbounded = new Range<int>(1, 10, lowerInclusive: true, upperInclusive: false, lowerInfinite: true, upperInfinite: false);

        unbounded.Lower.Should().BeNull();
        unbounded.LowerInfinite.Should().BeTrue();
        unbounded.LowerInclusive.Should().BeFalse();
        unbounded.Upper.Should().Be(10);
        unbounded.UpperInfinite.Should().BeFalse();
    }

    [Fact]
    public void Equality_ShouldCompareCharacteristics()
    {
        var range = new Range<int>(1, 10, lowerInclusive: false, upperInclusive: true);

        range.Should().Be(new Range<int>(1, 10, lowerInclusive: false, upperInclusive: true));
        range.Should().NotBe(new Range<int>(1, 10));
        range.GetHashCode().Should().Be(new Range<int>(1, 10, false, true).GetHashCode());
    }

    [Fact]
    public void ToString_ShouldRenderPostgresNotation()
    {
        new Range<int>(1, 10).ToString().Should().Be("[1,10)");
        new Range<int>(1, 10, true, true).ToString().Should().Be("[1,10]");
        new Range<int>(1, 10, false, false, lowerInfinite: true, upperInfinite: true).ToString().Should().Be("(,)");
        Range<int>.Empty.ToString().Should().Be("empty");
    }

    [Fact]
    public void ToString_ShouldBeCultureInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            new Range<decimal>(1.5m, 2.5m).ToString().Should().Be("[1.5,2.5)");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
