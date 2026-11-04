using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4, batch 1 — the residual arms of <c>src/nextorm.core/Visitors/StringFunctionTranslator.cs</c>
/// on a live SQLite database: the case transforms (culture-less and invariant), the trim family, the
/// substring/replace/remove/insert splices, the index family with its ordinal comparison, padding,
/// the LIKE-based <c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c> forms (constant, wildcard-escaped
/// and runtime-value patterns), <c>string.Equals</c>, <c>Compare</c>/<c>CompareOrdinal</c>,
/// <c>IsNullOrEmpty</c> and the deterministic refusals (culture-sensitive case, character trim,
/// non-constant pad character, <c>LastIndexOf</c>, <c>Split</c>, <c>Join</c> over an array).
/// Every accepted fact asserts the computed value; every refusal asserts the exact type plus the
/// semantic identifier the guard carries.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep4StringFunctionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_str_row (id integer primary key, name text null, note text null);
        delete from s4_str_row;
        insert into s4_str_row (id, name, note) values
            (1, 'Hello World', '  abc  '),
            (2, null, 'xyzxyz'),
            (3, '', 'aXbXc'),
            (4, '50%off', 'off');
        """;

    public SqliteStep4StringFunctionTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>The four culture-less case spellings and the invariant-culture overloads.</summary>
    [Fact]
    public void CaseTransforms_ShouldCompute()
    {
        var r = Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Upper = x.Name!.ToUpper(),
                Lower = x.Name!.ToLower(),
                UpperInvariant = x.Name!.ToUpperInvariant(),
                LowerInvariant = x.Name!.ToLowerInvariant(),
                ConstantUpper = "MiXeD".ToUpper(CultureInfo.InvariantCulture),
                ConstantLower = "MiXeD".ToLower(CultureInfo.InvariantCulture)
            })
            .Single();

        r.Upper.Should().Be("HELLO WORLD");
        r.Lower.Should().Be("hello world");
        r.UpperInvariant.Should().Be("HELLO WORLD");
        r.LowerInvariant.Should().Be("hello world");
        r.ConstantUpper.Should().Be("MIXED");
        r.ConstantLower.Should().Be("mixed");
    }

    /// <summary>A non-invariant culture has no portable SQL form and is refused before SQL.</summary>
    [Fact]
    public void CaseTransform_WithNonInvariantCulture_ShouldBeRefused()
    {
        var culture = new CultureInfo("tr-TR");

        var act = () => Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = x.Name!.ToUpper(culture) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*ToUpper(CultureInfo)*InvariantCulture*");
    }

    /// <summary>The three whitespace trims keep the C# semantics on the stored padded value.</summary>
    [Fact]
    public void TrimFamily_ShouldCompute()
    {
        var r = Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Both = x.Note!.Trim(),
                Start = x.Note!.TrimStart(),
                End = x.Note!.TrimEnd()
            })
            .Single();

        r.Both.Should().Be("abc");
        r.Start.Should().Be("abc  ");
        r.End.Should().Be("  abc");
    }

    /// <summary>A character-argument trim has no portable SQL form and is refused before SQL.</summary>
    [Fact]
    public void TrimWithCharacter_ShouldBeRefused()
    {
        var act = () => Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = x.Name!.Trim('*') })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Trim with a character argument*SQL trims whitespace only*");
    }

    /// <summary>Both substring overloads and the two-argument replace compute the C# result.</summary>
    [Fact]
    public void SubstringAndReplace_ShouldCompute()
    {
        var r = Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Tail = x.Note!.Substring(2),
                Middle = x.Note!.Substring(2, 3),
                Replaced = x.Note!.Replace("a", "A")
            })
            .Single();

        r.Tail.Should().Be("abc  ");
        r.Middle.Should().Be("abc");
        r.Replaced.Should().Be("  Abc  ");
    }

    /// <summary>Remove truncates or splices; Insert splices without removing anything.</summary>
    [Fact]
    public void RemoveAndInsert_ShouldCompute()
    {
        var r = Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                FromIndex = x.Note!.Remove(2),
                Slice = x.Note!.Remove(2, 3),
                Inserted = x.Note!.Insert(2, "Z")
            })
            .Single();

        r.FromIndex.Should().Be("  ");
        r.Slice.Should().Be("    ");
        r.Inserted.Should().Be("  Zabc  ");
    }

    /// <summary>The index family: plain, from a start index, and both ordinal comparison modes.</summary>
    [Fact]
    public void IndexOfVariants_ShouldCompute()
    {
        var r = Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Plain = x.Note!.IndexOf("b"),
                FromStart = x.Note!.IndexOf("b", 1),
                MissingFromStart = x.Note!.IndexOf("a", 4),
                Ordinal = x.Note!.IndexOf("B", StringComparison.Ordinal),
                OrdinalIgnoreCase = x.Note!.IndexOf("B", StringComparison.OrdinalIgnoreCase)
            })
            .Single();

        r.Plain.Should().Be(3);
        r.FromStart.Should().Be(3);
        r.MissingFromStart.Should().Be(-1);
        r.Ordinal.Should().Be(-1);
        r.OrdinalIgnoreCase.Should().Be(3);
    }

    /// <summary>SQLite has no reverse, so the last-index form is a named refusal.</summary>
    [Fact]
    public void LastIndexOf_OnSqlite_ShouldBeRefusedByName()
    {
        var act = () => Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = x.Note!.LastIndexOf("a") })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*String reversal is not supported*");
    }

    /// <summary>Both padding forms pad to the requested width without truncating a longer value.</summary>
    [Fact]
    public void PadFamily_ShouldCompute()
    {
        var r = Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                LeftDefault = x.Note!.PadLeft(8),
                RightDefault = x.Note!.PadRight(8),
                LeftStar = x.Note!.PadLeft(8, '*'),
                NoTruncation = x.Note!.PadRight(3),
                Null = x.Name!.Substring(0, 0)
            })
            .Single();

        r.LeftDefault.Should().Be("   abc  ");
        r.RightDefault.Should().Be("  abc   ");
        r.LeftStar.Should().Be("*  abc  ");
        r.NoTruncation.Should().Be("  abc  ");
        r.Null.Should().BeEmpty();
    }

    /// <summary>A runtime padding character cannot be rendered as a literal and is refused.</summary>
    [Fact]
    public void PadWithRuntimeCharacter_ShouldBeRefused()
    {
        var pad = '*';

        var act = () => Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = x.Note!.PadLeft(8, pad) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*PadLeft/PadRight padding character must be a constant*");
    }

    /// <summary>The constant LIKE forms match, and a wildcard in the constant is escaped.</summary>
    [Fact]
    public void LikeForms_WithConstantPattern_ShouldEscapeWildcards()
    {
        Context.From<S4StrRow>().Where(x => x.Name!.Contains("o W")).Count().Should().Be(1);
        Context.From<S4StrRow>().Where(x => x.Name!.StartsWith("Hell")).Count().Should().Be(1);
        Context.From<S4StrRow>().Where(x => x.Name!.EndsWith("rld")).Count().Should().Be(1);
        Context.From<S4StrRow>().Where(x => x.Name!.Contains("%")).Count().Should().Be(1, "only the literal percent row matches");
        Context.From<S4StrRow>().Where(x => x.Name!.Contains("_")).Count().Should().Be(0);
    }

    /// <summary>A runtime pattern is wrapped in percent signs and left unescaped.</summary>
    [Fact]
    public void LikeForms_WithRuntimePattern_ShouldCompareColumns()
    {
        Context.From<S4StrRow>().Where(x => x.Name!.Contains(x.Note!)).Count().Should().Be(1, "only '50%off' contains 'off'");
        Context.From<S4StrRow>().Where(x => x.Name!.StartsWith(x.Name!)).Count().Should().Be(3, "non-null names match themselves");
    }

    /// <summary>SQLite cannot force a byte-wise LIKE, so the case-sensitive ordinal form is refused.</summary>
    [Fact]
    public void CaseSensitiveOrdinalLike_OnSqlite_ShouldBeRefused()
    {
        var act = () => Context.From<S4StrRow>()
            .Where(x => x.Name!.Contains("World", StringComparison.Ordinal))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*case-sensitive ordinal Contains/StartsWith/EndsWith*collation-based overload*");
    }

    /// <summary>The case-insensitive ordinal form is expressible through lower() and stays correct.</summary>
    [Fact]
    public void OrdinalIgnoreCaseLike_ShouldCompute()
    {
        var rows = Context.From<S4StrRow>()
            .Where(x => x.Name!.StartsWith("hello", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToList();

        rows.Should().Equal(1);
    }

    /// <summary>SQLite has no scalar array-split form, so Split is refused with the provider marker.</summary>
    [Fact]
    public void Split_OnSqlite_ShouldBeRefusedByName()
    {
        var act = () => Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = x.Note!.Split('a') })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*string.Split is not supported by this provider*");
    }

    /// <summary>Static and instance Equals compare ordinally, with and without a comparison argument.</summary>
    [Fact]
    public void EqualsVariants_ShouldCompute()
    {
        var r = Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Static = string.Equals(x.Name, "Hello World"),
                StaticWithComparison = string.Equals(x.Name, "HELLO WORLD", StringComparison.OrdinalIgnoreCase),
                Instance = x.Name!.Equals("Hello World"),
                InstanceWithComparison = x.Name!.Equals("hello world", StringComparison.OrdinalIgnoreCase)
            })
            .Single();

        r.Static.Should().BeTrue();
        r.StaticWithComparison.Should().BeTrue();
        r.Instance.Should().BeTrue();
        r.InstanceWithComparison.Should().BeTrue();
    }

    /// <summary>CompareOrdinal and the ordinal Compare overload keep their sign inside a predicate.</summary>
    [Fact]
    public void CompareVariants_ShouldCompute()
    {
        var ordinal = Context.From<S4StrRow>()
            .Where(x => string.CompareOrdinal(x.Name, "Z") < 0)
            .Select(x => x.Id)
            .ToList();

        ordinal.Should().Equal(1, 3, 4);

        var compared = Context.From<S4StrRow>()
            .Where(x => string.Compare(x.Name, "Z", StringComparison.Ordinal) < 0)
            .Select(x => x.Id)
            .ToList();

        compared.Should().Equal(1, 3, 4);
    }

    /// <summary>The culture-sensitive Compare overloads are refused instead of silently using the server locale.</summary>
    [Fact]
    public void CultureSensitiveCompare_ShouldBeRefused()
    {
        Action noComparison = () => Context.From<S4StrRow>()
            .Where(x => string.Compare(x.Name, "Z") < 0)
            .ToList();

        noComparison.Should().Throw<NotSupportedException>()
            .WithMessage("*string.Compare without a StringComparison is culture-sensitive*");

        Action booleanComparison = () => Context.From<S4StrRow>()
            .Where(x => string.Compare(x.Name, "Z", false) < 0)
            .ToList();

        booleanComparison.Should().Throw<NotSupportedException>()
            .WithMessage("*string.Compare(a, b, bool) is culture-sensitive*");
    }

    /// <summary>IsNullOrEmpty folds both the NULL and the empty-string case into one predicate.</summary>
    [Fact]
    public void IsNullOrEmpty_ShouldCompute()
    {
        var ids = Context.From<S4StrRow>()
            .Where(x => string.IsNullOrEmpty(x.Name))
            .Select(x => x.Id)
            .ToList();

        ids.Should().BeEquivalentTo(new[] { 2, 3 });
    }

    /// <summary>Join over a row-dependent array needs native arrays, which SQLite does not have.</summary>
    [Fact]
    public void JoinOverArray_OnSqlite_ShouldBeRefusedByName()
    {
        var act = () => Context.From<S4StrRow>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = string.Join(",", new[] { x.Name, x.Note }) })
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*string.Join over an array requires a provider with native arrays (PostgreSQL)*");
    }

    /// <summary>Marker source of the string-translation facts.</summary>
    [SqlTable("s4_str_row")]
    public sealed class S4StrRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("note")]
        public string? Note { get; set; }
    }
}
