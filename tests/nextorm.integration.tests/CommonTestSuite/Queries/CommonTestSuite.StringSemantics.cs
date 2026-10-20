using System.Globalization;
using System.Linq.Expressions;
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

    /// <summary>
    /// Builds <c>e =&gt; e.RequiredString.ToUpper/ToLower(Expression.Constant(CultureInfo.InvariantCulture))</c>.
    /// The explicitly constant culture is the contract <c>StringFunctionTranslator.TryTranslateCultureCase</c>
    /// accepts; the natural C# spelling <c>e.RequiredString.ToUpper(CultureInfo.InvariantCulture)</c> binds a
    /// static property expression that the query pipeline turns into a query parameter, so it is rejected
    /// there (recorded as the defect <c>C9-STRING-CULTURE-INVARIANT-MEMBER</c>, not pinned as behaviour).
    /// </summary>
    private static Expression<Func<IComplexEntity, string>> CultureCaseProjection(string methodName)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "e");
        var text = Expression.Property(parameter, nameof(IComplexEntity.RequiredString));
        var method = typeof(string).GetMethod(methodName, [typeof(CultureInfo)])!;
        var body = Expression.Call(text, method, Expression.Constant(CultureInfo.InvariantCulture));
        return Expression.Lambda<Func<IComplexEntity, string>>(body, parameter);
    }

    [Fact]
    public void CultureInvariantCase_ShouldUsePortableUpperAndLower()
    {
        // CultureInfo.InvariantCulture is the only culture with a portable SQL form. The seeded value is
        // wholly lower-case, so the lower arm asserts the round trip and the upper arm the transformation.
        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(CultureCaseProjection(nameof(string.ToUpper)))
            .First()
            .Should().Be("SDF");

        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(CultureCaseProjection(nameof(string.ToLower)))
            .First()
            .Should().Be("sdf");
    }

    [Fact]
    public void CultureCase_ShouldRejectEveryNonInvariantCulture()
    {
        var upper = FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(e => e.Id == 1)
                .Select(e => e.RequiredString.ToUpper(new CultureInfo("tr-TR")))
                .ToList());

        upper.Should().Throw<NotSupportedException>()
            .WithMessage("string.ToUpper(CultureInfo) is only supported with CultureInfo.InvariantCulture; other cultures have no portable SQL form.");

        var lower = FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(e => e.Id == 1)
                .Select(e => e.RequiredString.ToLower(new CultureInfo("tr-TR")))
                .ToList());

        lower.Should().Throw<NotSupportedException>()
            .WithMessage("string.ToLower(CultureInfo) is only supported with CultureInfo.InvariantCulture; other cultures have no portable SQL form.");
    }

    [Fact]
    public void EqualsOverloads_ShouldMatchOrdinalValues()
    {
        // Static 2-arg, static 3-arg (Ordinal / OrdinalIgnoreCase), instance 1-arg and instance
        // 2-arg all lower to ordinal equality; the seeded row 2 is "xxx".
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 2)
            .Select(e => new
            {
                StaticTwoArg = string.Equals(e.String, "xxx"),
                StaticOrdinal = string.Equals(e.String, "xxx", StringComparison.Ordinal),
                StaticOrdinalIgnoreCase = string.Equals(e.String, "XXX", StringComparison.OrdinalIgnoreCase),
                StaticOrdinalMismatch = string.Equals(e.String, "XXX", StringComparison.Ordinal),
                InstanceOneArg = e.String!.Equals("xxx"),
                InstanceOrdinalIgnoreCase = e.String!.Equals("XXX", StringComparison.OrdinalIgnoreCase)
            })
            .First();

        value.StaticTwoArg.Should().BeTrue();
        value.StaticOrdinal.Should().BeTrue();
        value.StaticOrdinalIgnoreCase.Should().BeTrue();
        value.StaticOrdinalMismatch.Should().BeFalse();
        value.InstanceOneArg.Should().BeTrue();
        value.InstanceOrdinalIgnoreCase.Should().BeTrue();
    }

    [Fact]
    public void CompareOrdinalForms_ShouldReturnSignedResult()
    {
        // "dadfasd" (row 1) is below "zzz", equal to itself and above "aaa"; the ordinal ignore-case
        // arm compares against the upper-case spelling so a byte-order compare would not return 0.
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                CompareOrdinalLess = string.CompareOrdinal(e.String, "zzz"),
                CompareOrdinalEqual = string.CompareOrdinal(e.String, "dadfasd"),
                CompareOrdinalGreater = string.CompareOrdinal(e.String, "aaa"),
                CompareLess = string.Compare(e.String, "zzz", StringComparison.Ordinal),
                CompareEqual = string.Compare(e.String, "dadfasd", StringComparison.Ordinal),
                CompareGreater = string.Compare(e.String, "aaa", StringComparison.Ordinal),
                CompareOrdinalIgnoreCaseEqual = string.Compare(e.String, "DADFASD", StringComparison.OrdinalIgnoreCase)
            })
            .First();

        value.CompareOrdinalLess.Should().Be(-1);
        value.CompareOrdinalEqual.Should().Be(0);
        value.CompareOrdinalGreater.Should().Be(1);
        value.CompareLess.Should().Be(-1);
        value.CompareEqual.Should().Be(0);
        value.CompareGreater.Should().Be(1);
        value.CompareOrdinalIgnoreCaseEqual.Should().Be(0);
    }

    [Fact]
    public void IndexOfOverloads_ShouldReturnZeroBasedPositions()
    {
        // "dadfasd": d0 a1 d2 f3 a4 s5 d6.
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => new
            {
                Plain = e.String!.IndexOf("fa"),
                Missing = e.String!.IndexOf("zzz"),
                WithStart = e.String!.IndexOf("a", 2),
                WithStartMissing = e.String!.IndexOf("f", 4),
                Ordinal = e.String!.IndexOf("A", StringComparison.Ordinal),
                OrdinalIgnoreCase = e.String!.IndexOf("A", StringComparison.OrdinalIgnoreCase)
            })
            .First();

        value.Plain.Should().Be(3);
        value.Missing.Should().Be(-1);
        value.WithStart.Should().Be(4);
        value.WithStartMissing.Should().Be(-1);
        value.Ordinal.Should().Be(-1);
        value.OrdinalIgnoreCase.Should().Be(1);
    }
}
