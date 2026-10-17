using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// #237 (T237) identifier safety on SQL Server. Three observable contracts, each pinned by a full-SQL
/// oracle over a placeholder connection string (no connection is opened):
/// <list type="number">
/// <item><see cref="SqlServerDialect.Escape"/> bracket-quotes and doubles an embedded <c>]</c>, so a
/// value like <c>a]b</c> stays one identifier instead of splitting into two;</item>
/// <item><c>SqlFunctions.Column&lt;T&gt;</c> and the PIVOT/UNPIVOT markers emit their raw name through
/// exactly one escaping stage, independent of the command's <c>QuoteIdentifiers</c> option, so a name
/// with SQL-looking punctuation is an identifier and not SQL text;</item>
/// <item>the typed PIVOT/UNPIVOT declaration and its projection reference derive from the same raw
/// input and produce the same safe representation, with no double quoting in quoted mode and no silent
/// reinterpretation of a literal bracket name.</item>
/// </list>
/// </summary>
public class SqlServerIdentifierSafetyTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    // ------------------------------------------------------------------ Escape (R237-01)

    /// <summary>
    /// The ordinary, the embedded and the repeated bracket: every emitted form is a single bracket-quoted
    /// identifier. The expectations are literals, never the dialect's own <c>QuoteIdentifier</c>.
    /// </summary>
    [Fact]
    public void Escape_DoublesAnEmbeddedBracket_AndKeepsOrdinaryNames()
    {
        var dialect = SqlServerDialect.Instance;

        dialect.Escape("a]b").Should().Be("[a]]b]");
        dialect.Escape("]").Should().Be("[]]]");
        dialect.Escape("a]]b").Should().Be("[a]]]]b]");
        dialect.Escape("name").Should().Be("[name]");
        dialect.Escape("1").Should().Be("[1]");
    }

    // ------------------------------------------------------------------ Column<T> (R237-04)

    /// <summary>
    /// With identifier quoting off, <c>Column&lt;T&gt;</c> still emits one safely quoted identifier:
    /// the option no longer decides whether the raw name reaches the SQL text.
    /// </summary>
    [Fact]
    public void Column_UnquotedMode_EmitsOneAlwaysQuotedIdentifier()
    {
        using var ctx = SqlServerTestContext.Create();

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "name") }))
            .Should().Be("select [name] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "a]b") }))
            .Should().Be("select [a]]b] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "a\\b") }))
            .Should().Be("select [a\\b] as [C]\n from sales");
    }

    /// <summary>
    /// A dot, SQL-looking punctuation and literal brackets all stay inside one identifier: the name is
    /// never split into a qualified path, never interpreted as SQL, and never stripped of its brackets.
    /// </summary>
    [Fact]
    public void Column_UnquotedMode_KeepsPunctuationInsideOneIdentifier()
    {
        using var ctx = SqlServerTestContext.Create();

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "a.b") }))
            .Should().Be("select [a.b] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "0+(SELECT COUNT(*) FROM sys.tables)") }))
            .Should().Be("select [0+(SELECT COUNT(*) FROM sys.tables)] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "[1]") }))
            .Should().Be("select [[1]]] as [C]\n from sales");
    }

    /// <summary>With identifier quoting on the name is still quoted exactly once (no second pass).</summary>
    [Fact]
    public void Column_QuotedMode_QuotesTheNameExactlyOnce()
    {
        using var ctx = SqlServerTestContext.CreateQuoted();

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "a]b") }))
            .Should().Be("select [a]]b] as [C]\n from [sales]");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "name") }))
            .Should().Be("select [name] as [C]\n from [sales]");
    }

    // ------------------------------------------------------------------ PIVOT/UNPIVOT (R237-01..03)

    /// <summary>An untyped PIVOT value with an embedded <c>]</c> is one safely escaped IN-list item.</summary>
    [Fact]
    public void UntypedPivot_ValueWithBracket_IsEscapedOnce()
    {
        using var ctx = SqlServerTestContext.Create();

        SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("a]b"), PivotValue.Create("1"))
            .Select(t => new { C = SqlFunctions.Column<string>(t, "category") }))
            .Should().Be("select [category] as [C]\n from sales pivot (sum(margin) for quarter in ([a]]b], [1])) as [t1]");
    }

    /// <summary>
    /// The typed PIVOT declaration and its projection reference derive from the same raw input: both
    /// render <c>[a]]b]</c>, so there is no second escaping stage anywhere in the name pipeline.
    /// </summary>
    [Fact]
    public void TypedPivot_RawName_DeclarationAndReferenceAgree()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("a]b") })
            .Select(x => new { x.Category, x.V }));

        sql.Should().Be(
            "select [category], [V]\n" +
            " from (select [category], [a]]b] as [V]\n" +
            " from sales pivot (sum(margin) for quarter in ([a]]b])) as [t1]) as [t2]");
    }

    /// <summary>
    /// A literal bracket name is not a prequoted form: <c>Value("[1]")</c> keeps the data value
    /// <c>[1]</c> and renders the identifier <c>[[1]]]</c>, never a silent reinterpretation as <c>1</c>.
    /// </summary>
    [Fact]
    public void TypedPivot_LiteralBracketValue_IsNotReinterpreted()
    {
        using var ctx = SqlServerTestContext.Create();

        PivotValue.Create("[1]").Value.Should().Be("[1]");

        var sql = SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("[1]") })
            .Select(x => new { x.Category, x.V }));

        sql.Should().Be(
            "select [category], [V]\n" +
            " from (select [category], [[1]]] as [V]\n" +
            " from sales pivot (sum(margin) for quarter in ([[1]]])) as [t1]) as [t2]");
    }

    /// <summary>
    /// In quoted mode the typed PIVOT declaration and reference are still quoted exactly once each: the
    /// pre-escape stage that produced triple brackets is gone.
    /// </summary>
    [Fact]
    public void TypedPivot_QuotedMode_HasNoDoubleEscaping()
    {
        using var ctx = SqlServerTestContext.CreateQuoted();

        SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("[1]") })
            .Select(x => new { x.Category, x.V }))
            .Should().Be(
                "select [category], [V]\n" +
                " from (select [category], [[1]]] as [V]\n" +
                " from [sales] pivot (sum([margin]) for [quarter] in ([[1]]])) as [t1]) as [t2]");

        SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, (m, pv) => new { m.Category, V = pv.Value<decimal?>("a]b") })
            .Select(x => new { x.Category, x.V }))
            .Should().Be(
                "select [category], [V]\n" +
                " from (select [category], [a]]b] as [V]\n" +
                " from [sales] pivot (sum([margin]) for [quarter] in ([a]]b])) as [t1]) as [t2]");
    }

    /// <summary>
    /// The direct marker route (a captured <see cref="PivotValues"/> in an ordinary <c>Select</c>, before
    /// the projection rewriter can replace it) is always-quoted too: a payload name is one identifier.
    /// </summary>
    [Fact]
    public void MarkerRoute_IsAlwaysQuoted_EvenWithQuotingOff()
    {
        using var ctx = SqlServerTestContext.Create();
        var pivot = new PivotValues();

        SqlOf(ctx, ctx.From<ISalesEntity>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"))
            .Select(t => new { Q1 = pivot.Value<decimal?>("0+(SELECT COUNT(*) FROM sys.tables)") }))
            .Should().Be("select [0+(SELECT COUNT(*) FROM sys.tables)] as [Q1]\n from sales pivot (sum(margin) for quarter in ([1])) as [t1]");
    }

    /// <summary>
    /// A typed UNPIVOT role identifier with SQL-looking punctuation is one identifier in the declaration
    /// and in the projection reference, both derived from the same raw role string.
    /// </summary>
    [Fact]
    public void TypedUnpivot_RolePayload_DeclarationAndReferenceAgree()
    {
        using var ctx = SqlServerTestContext.Create();

        SqlOf(ctx, ctx.From<IQuarterlyEntity>()
            .Unpivot(
                (m, u) => new { m.Category, N = u.Name<string>("0+(SELECT COUNT(*) FROM sys.tables)"), V = u.Value<decimal?>("val") },
                UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(x => new { x.Category, x.N, x.V }))
            .Should().Be(
                "select [category], [N], [V]\n" +
                " from (select [category], [0+(SELECT COUNT(*) FROM sys.tables)] as [N], [val] as [V]\n" +
                " from quarterly unpivot ([val] for [0+(SELECT COUNT(*) FROM sys.tables)] in ([q1], [q2])) as [t1]) as [t2]");

        SqlOf(ctx, ctx.From<IQuarterlyEntity>()
            .Unpivot(
                (m, u) => new { m.Category, N = u.Name<string>("a]b"), V = u.Value<decimal?>("val") },
                UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(x => new { x.Category, x.N, x.V }))
            .Should().Be(
                "select [category], [N], [V]\n" +
                " from (select [category], [a]]b] as [N], [val] as [V]\n" +
                " from quarterly unpivot ([val] for [a]]b] in ([q1], [q2])) as [t1]) as [t2]");
    }
}
