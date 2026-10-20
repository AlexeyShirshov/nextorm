using System.Globalization;
using System.Text.RegularExpressions;
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
    public void LastIndexOfComparisonOverloads_ShouldBeRejectedByTheReversePrimitive()
    {
        // SQLite has no reverse primitive, so both the plain and the StringComparison LastIndexOf
        // arms fail inside the shared MakeStringLastIndexOf renderer.
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.LastIndexOf("x", StringComparison.Ordinal))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("String reversal is not supported by this provider.");

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.LastIndexOf("X", StringComparison.OrdinalIgnoreCase))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("String reversal is not supported by this provider.");
    }

    [Fact]
    public void LastIndexOfStartIndexOverload_ShouldBeRejected()
    {
        // LastIndexOf(string, int startIndex) has no rendering primitive: the non-StringComparison
        // second argument makes the translator decline, and the base visitor reports the method.
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.LastIndexOf("x", 1))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The method String.LastIndexOf is not supported.");
    }

    [Fact]
    public void NonConstantStringComparison_ShouldBeRejected()
    {
        var comparison = StringComparison.Ordinal;

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.Equals("xxx", comparison))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The StringComparison argument of a string method must be a constant.");

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => string.Compare(x.String, "xxx", comparison))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The StringComparison argument of a string method must be a constant.");
    }

    [Fact]
    public void IndexOfStartAndCountOverload_ShouldBeRejected()
    {
        // IndexOf(string, int startIndex, int count) carries two int arguments; the count must not
        // be mistaken for the start index, so the translator declines the call.
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.IndexOf("x", 1, 1))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The method String.IndexOf is not supported.");
    }

    [Fact]
    public void SearchPatternEscaping_ShouldTreatWildcardsLiterally()
    {
        // "x_" is a literal two-character needle. Without wildcard escaping the pattern "%x_%" would
        // match "xxx" (x followed by any character); SQLite is LIKE-only, so the escape arm decides.
        _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => x.String!.Contains("x_"))
            .First()
            .Should().BeFalse();

        _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => x.String!.StartsWith("x_"))
            .First()
            .Should().BeFalse();
    }

    [Fact]
    public void SearchRuntimeNeedle_ShouldMatchThroughConcatenation()
    {
        // A non-constant needle is wrapped in % at render time (MakeConcat) instead of being inlined
        // as an escaped literal.
        var needle = "xx";

        _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => x.String!.Contains(needle))
            .First()
            .Should().BeTrue();

        _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => x.String!.EndsWith(needle))
            .First()
            .Should().BeTrue();
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

    /// <summary>
    /// Every <c>string.Split</c> shape that is not a single constant separator is rejected by the
    /// translation guards before the provider is even asked for a split renderer.
    /// </summary>
    [Fact]
    public void SplitGuardFamily_ShouldRejectUnsupportedShapes()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.Split('x', StringSplitOptions.RemoveEmptyEntries))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("string.Split with StringSplitOptions other than None is not supported.");

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.Split(new[] { 'x', 'y' }))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("string.Split with multiple separators is not supported.");

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.Split('x', 2))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("This string.Split overload is not supported; only a single-separator overload maps to SQL.");
    }

    /// <summary>
    /// <c>PadLeft</c>/<c>PadRight</c> render the padding character as a one-character string literal, so
    /// a runtime character has no SQL shape.
    /// </summary>
    [Fact]
    public void PadWithRuntimeCharacter_ShouldBeRejected()
    {
        var pad = '0';

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.PadLeft(5, pad))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The string.PadLeft/PadRight padding character must be a constant.");

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.PadRight(5, pad))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The string.PadLeft/PadRight padding character must be a constant.");
    }

    /// <summary>
    /// <c>string.Join</c> over an array needs a provider with native arrays; SQLite has none, while the
    /// array still references a query column (a constant-only join folds to a parameter).
    /// </summary>
    [Fact]
    public void JoinOverArray_ShouldRequireNativeArrays()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => string.Join(",", new[] { x.String!, "b" }))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("string.Join over an array requires a provider with native arrays (PostgreSQL).");
    }

    /// <summary>
    /// Overloads outside the portable shape are rejected as unsupported methods: a <c>Replace</c>
    /// with a <see cref="char"/> argument, a string method with no translation, and a
    /// <c>string.Compare</c> overload whose first argument is not a string.
    /// </summary>
    [Fact]
    public void UnsupportedStringShapes_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.Replace('x', 'y'))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The method Replace is not supported.");

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => x.String!.IsNormalized())
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The method String.IsNormalized is not supported.");

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => string.Compare(x.String, 1, "xxx", 0, 3))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The method String.Compare is not supported.");
    }

    /// <summary>
    /// The remaining portable static forms execute: the three-argument static
    /// <c>string.Equals</c> lowers to ordinal equality and <c>string.CompareOrdinal</c> answers the
    /// signed comparison through a <c>CASE</c> expression.
    /// </summary>
    [Fact]
    public void StaticEqualsAndCompareOrdinal_ShouldExecute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                EqualsOrdinal = string.Equals(x.String, "xxx", StringComparison.Ordinal),
                EqualsOrdinalIgnore = string.Equals(x.String, "XXX", StringComparison.OrdinalIgnoreCase),
                EqualsDifferent = string.Equals(x.String, "yyy", StringComparison.Ordinal),
                CompareEqual = string.CompareOrdinal(x.String, "xxx"),
                CompareLess = string.CompareOrdinal(x.String, "zzz"),
                CompareGreater = string.CompareOrdinal(x.String, "aaa")
            })
            .First();

        r.EqualsOrdinal.Should().BeTrue();
        r.EqualsOrdinalIgnore.Should().BeTrue();
        r.EqualsDifferent.Should().BeFalse();
        r.CompareEqual.Should().Be(0);
        r.CompareLess.Should().BeNegative();
        r.CompareGreater.Should().BePositive();
    }

    /// <summary>
    /// The static <see cref="Regex"/> surface of <c>RegexSqlTranslator</c> (:39-77): the two-argument
    /// <c>IsMatch</c>, the options overload with the accepted no-op flags, a case-insensitive match and
    /// both <c>Replace</c> forms execute; the emitted SQL runs over a real column.
    /// </summary>
    [Fact]
    public void RegexStaticSurface_ShouldMatchAndReplace()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Match = Regex.IsMatch(x.String!, "^x"),
                NoMatch = Regex.IsMatch(x.String!, "^z"),
                IgnoringCase = Regex.IsMatch(x.String!, "^X", RegexOptions.IgnoreCase),
                AcceptedNoOps = Regex.IsMatch(x.String!, "^x", RegexOptions.Compiled | RegexOptions.CultureInvariant),
                Replaced = Regex.Replace(x.String!, "x+", "Q"),
                ReplacedIgnoringCase = Regex.Replace(x.String!, "X+", "Q", RegexOptions.IgnoreCase)
            })
            .First();

        r.Match.Should().BeTrue();
        r.NoMatch.Should().BeFalse();
        r.IgnoringCase.Should().BeTrue();
        r.AcceptedNoOps.Should().BeTrue();
        r.Replaced.Should().Be("Q");
        r.ReplacedIgnoringCase.Should().Be("Q");
    }

    /// <summary>
    /// The instance <see cref="Regex"/> surface of <c>RegexSqlTranslator</c> (:79-103 and
    /// <c>TryGetRegex</c> :164-201): an inline <c>new Regex(pattern)</c>, a <c>new Regex(pattern, options)</c>
    /// and a captured parameter-free local (with and without options) are all read at translation time,
    /// for both <c>IsMatch</c> and <c>Replace</c>.
    /// </summary>
    [Fact]
    public void RegexInstanceSurface_ShouldMatchAndReplace()
    {
        var local = new Regex("^x+$");
        var ignoringCase = new Regex("^X+$", RegexOptions.IgnoreCase);

        var r = _sut.ComplexEntity
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                NewPattern = new Regex("^x+$").IsMatch(x.String!),
                NewPatternWithOptions = new Regex("^X+$", RegexOptions.IgnoreCase).IsMatch(x.String!),
                Captured = local.IsMatch(x.String!),
                CapturedOptions = ignoringCase.IsMatch(x.String!),
                Replaced = local.Replace(x.String!, "Q"),
                ReplacedCaptured = ignoringCase.Replace(x.String!, "Q")
            })
            .First();

        r.NewPattern.Should().BeTrue();
        r.NewPatternWithOptions.Should().BeTrue();
        r.Captured.Should().BeTrue();
        r.CapturedOptions.Should().BeTrue();
        r.Replaced.Should().Be("Q");
        r.ReplacedCaptured.Should().Be("Q");
    }

    /// <summary>
    /// The documented rejection surface of <c>RegexSqlTranslator</c>: a method other than
    /// <c>IsMatch</c>/<c>Replace</c> (:30), the unsupported static and instance overloads (:43, :58, :81,
    /// :91), a non-constant pattern/replacement/options (:48, :53, :66, :69, :75, :96), a receiver that is
    /// neither constant nor a <c>new Regex(...)</c> with constant arguments (:181) and an option with no
    /// portable SQL form (:141).
    /// </summary>
    [Fact]
    public void RegexUnsupportedShapes_ShouldBeRejected()
    {
        var pattern = "^x";
        var replacement = "Q";
        var options = RegexOptions.IgnoreCase;
        var compiled = new Regex("^x");
        var timeout = TimeSpan.FromSeconds(1);

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => Regex.Split(x.String!, ","))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("Regex.Split is not supported; only Regex.IsMatch and Regex.Replace are translated.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => Regex.Escape(x.String!))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("Regex.Escape is not supported; only Regex.IsMatch and Regex.Replace are translated.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => Regex.IsMatch(x.String!, "^x", RegexOptions.None, timeout))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("This Regex.IsMatch overload is not supported.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => Regex.IsMatch(x.String!, pattern))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The Regex pattern must be a compile-time constant.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => Regex.IsMatch(x.String!, "^x", options))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The RegexOptions argument must be a constant.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => Regex.Replace(x.String!, "x+", "Q", RegexOptions.None, timeout))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("This Regex.Replace overload is not supported; the replacement must be a string.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => Regex.Replace(x.String!, "x+", replacement))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The Regex replacement must be a compile-time constant.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => Regex.Replace(x.String!, "x+", "Q", options))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The RegexOptions argument must be a constant.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => Regex.IsMatch(x.String!, "^x", RegexOptions.Multiline))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("RegexOptions.Multiline is not supported; only IgnoreCase has a portable SQL form.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => compiled.IsMatch(x.String!, 0))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("This Regex.IsMatch overload is not supported.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => compiled.Replace(x.String!, "Q", 1))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("This Regex.Replace overload is not supported.");

        FluentActions.Invoking(() => _sut.ComplexEntity
                .Where(x => x.Id == 2)
                .Select(x => new Regex("^x", options).IsMatch(x.String!))
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("The Regex instance must be a constant or a new Regex(...) with a constant pattern.");
    }

    /// <summary>
    /// <c>RegexSqlTranslator.Reject</c> (:207-213) leaves a rejected call that references no query
    /// parameter to the caller's constant folding: the unsupported timeout overload over constants is
    /// evaluated client-side and travels as a parameter instead of throwing, while the same overload over
    /// a column is rejected above.
    /// </summary>
    [Fact]
    public void RegexConstantOnlyUnsupportedCall_ShouldBeFolded()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { Folded = Regex.IsMatch("abc", "b", RegexOptions.None, TimeSpan.FromSeconds(1)) })
            .First();

        r.Folded.Should().BeTrue();
    }
}
