using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Provider-agnostic behavioural coverage for the C# string-semantics surface (G12): the
/// culture-invariant format subset and ordinal <see cref="StringComparison"/> comparisons. The seeded
/// <c>complex_entity</c> has id 1 / <c>"dadfasd"</c> and id 2 / <c>"xxx"</c>, all lower-case, so an
/// upper-case ordinal search distinguishes a true byte-order comparison from a case-insensitive
/// database collation.
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void ToStringDateFormat_ShouldRenderIsoDate()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.Datetime!.Value.ToString("yyyy-MM-dd"))
            .First();

        value.Should().Be("2023-01-01");
    }

    [Fact]
    public void ToStringIntegerFormat_ShouldZeroPad()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.Id.ToString("D4"))
            .First();

        value.Should().Be("0001");
    }

    [Fact]
    public void ContainsOrdinal_ShouldBeCaseSensitive()
    {
        Assert.SkipUnless(((DataContext)_sut.DataProvider).Dialect.SupportsOrdinalLike, "This provider's LIKE cannot be made byte-wise.");

        // No seeded value contains an upper-case 'X', while id 2 is "xxx": a byte-order comparison
        // must not match, unlike a case-insensitive database collation.
        var ids = _sut.ComplexEntity
            .Where(e => e.String!.Contains("X", StringComparison.Ordinal))
            .Select(e => e.Id)
            .ToList();

        ids.Should().BeEmpty();
    }

    [Fact]
    public void ContainsOrdinalIgnoreCase_ShouldMatchAcrossCase()
    {
        var ids = _sut.ComplexEntity
            .Where(e => e.String!.Contains("X", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(2L);
    }

    [Fact]
    public void CompareOrdinal_ShouldCompareByCodePoint()
    {
        // "dadfasd" (id 1) sorts before "xxx" by code point; "xxx" (id 2) is equal and null (id 3) is
        // excluded by the SQL comparison.
        var ids = _sut.ComplexEntity
            .Where(e => string.CompareOrdinal(e.String, "xxx") < 0)
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(1L);
    }

    [Fact]
    public void EqualsOrdinal_ShouldMatchExactValue()
    {
        var ids = _sut.ComplexEntity
            .Where(e => e.String!.Equals("xxx", StringComparison.Ordinal))
            .Select(e => e.Id)
            .ToList();

        ids.Should().Equal(2L);
    }
}
