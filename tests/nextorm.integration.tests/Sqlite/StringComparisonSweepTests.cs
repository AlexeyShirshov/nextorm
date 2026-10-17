using System.Globalization;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// String overload sweep against SQLite: the comparison-aware overloads (<see cref="StringComparison"/>,
/// <see cref="CultureInfo"/>), the search overloads and the mutators. The portable forms execute; the
/// forms with no portable SQL shape (culture-sensitive comparison, case-sensitive ordinal
/// <c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c>, <c>IndexOf</c>/<c>LastIndexOf</c> and character
/// trimming) must be rejected during translation, which is pinned here as well.
/// </summary>
[Collection("Sqlite")]
public sealed class StringComparisonSweepTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void EqualsAndCompare_OrdinalForms_ShouldExecute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                EqOrdinal = x.String!.Equals("xxx", StringComparison.Ordinal),
                EqOrdinalIgnore = x.String!.Equals("XXX", StringComparison.OrdinalIgnoreCase),
                CompareOrdinal = string.Compare(x.String, "xxx", StringComparison.Ordinal),
                CompareOrdinalIgnore = string.Compare(x.String, "XXX", StringComparison.OrdinalIgnoreCase),
            })
            .First();

        r.EqOrdinal.Should().BeTrue();
        r.EqOrdinalIgnore.Should().BeTrue();
        r.CompareOrdinal.Should().Be(0);
        r.CompareOrdinalIgnore.Should().Be(0);
    }

    [Fact]
    public void CultureSensitiveComparison_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.Equals("xxx", StringComparison.CurrentCulture))
                .ToList())
            .Should().Throw<NotSupportedException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.Equals("xxx", StringComparison.InvariantCultureIgnoreCase))
                .ToList())
            .Should().Throw<NotSupportedException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => string.Compare(x.String, "xxx"))
                .ToList())
            .Should().Throw<NotSupportedException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => string.Compare(x.String, "xxx", true))
                .ToList())
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SearchOverloads_PlainForms_ShouldExecute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Contains = x.String!.Contains("xx"),
                StartsWith = x.String!.StartsWith("xx"),
                EndsWith = x.String!.EndsWith("xx")
            })
            .First();

        r.Contains.Should().BeTrue();
        r.StartsWith.Should().BeTrue();
        r.EndsWith.Should().BeTrue();
    }

    [Fact]
    public void SearchOverloads_OrdinalIgnoreCase_ShouldExecute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Contains = x.String!.Contains("XX", StringComparison.OrdinalIgnoreCase),
                StartsWith = x.String!.StartsWith("XX", StringComparison.OrdinalIgnoreCase),
                EndsWith = x.String!.EndsWith("XX", StringComparison.OrdinalIgnoreCase)
            })
            .First();

        r.Contains.Should().BeTrue();
        r.StartsWith.Should().BeTrue();
        r.EndsWith.Should().BeTrue();
    }

    [Fact]
    public void CaseSensitiveOrdinalSearch_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.Contains("xx", StringComparison.Ordinal))
                .ToList())
            .Should().Throw<NotSupportedException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.StartsWith("xx", StringComparison.Ordinal))
                .ToList())
            .Should().Throw<NotSupportedException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.EndsWith("xx", StringComparison.Ordinal))
                .ToList())
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UnsupportedSearchOverloads_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.IndexOf('x'))
                .ToList())
            .Should().Throw<NotSupportedException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.LastIndexOf("x"))
                .ToList())
            .Should().Throw<NotSupportedException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.CompareTo("xxx"))
                .ToList())
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void IndexOfString_ShouldExecute()
    {
        _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => x.String!.IndexOf("x"))
            .First()
            .Should().Be(0);
    }

    [Fact]
    public void MutatorsAndCulture_ShouldExecute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Remove = x.String!.Remove(1),
                RemoveRange = x.String!.Remove(1, 1),
                Insert = x.String!.Insert(1, "y"),
                PadLeft = x.String!.PadLeft(5, '0'),
                PadRight = x.String!.PadRight(5, '0'),
                NullOrEmpty = string.IsNullOrEmpty(x.String),
                Concat = string.Concat(x.String, "!")
            })
            .First();

        r.Remove.Should().Be("x");
        r.RemoveRange.Should().Be("xx");
        r.Insert.Should().Be("xyxx");
        r.PadLeft.Should().Be("00xxx");
        r.PadRight.Should().Be("xxx00");
        r.NullOrEmpty.Should().BeFalse();
        r.Concat.Should().Be("xxx!");
    }

    [Fact]
    public void IsNullOrWhiteSpace_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => string.IsNullOrWhiteSpace(x.String))
                .ToList())
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CharacterTrimAndSplit_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.Trim(new[] { 'x' }))
                .ToList())
            .Should().Throw<NotSupportedException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.TrimStart(new[] { 'x' }))
                .ToList())
            .Should().Throw<NotSupportedException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.Split(new[] { 'x' }))
                .ToList())
            .Should().Throw<NotSupportedException>();
    }
}
