using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// #237 (T237) core contract of <see cref="SqlFunctions.Column{T}"/>: its string argument is a
/// <b>raw</b> name, always quoted through the provider regardless of the command's
/// <c>QuoteIdentifiers</c> option, so a name with SQL-looking punctuation is one identifier and never
/// SQL text. These are full-SQL oracles over a placeholder connection string (the SQL Server dialect is
/// used as the concrete provider because the rule itself lives in the core visitor); no connection is
/// opened. The existing validation contracts and the plan-cache behaviour are asserted unchanged.
/// </summary>
public class IdentifierSafetyTests
{
    private const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    private static IDataContext CreateContext()
        => new SqlServerDataContext(PlaceholderConnectionString, new DataContextBuilder());

    private static IDataContext CreateQuotedContext()
        => new SqlServerDataContext(PlaceholderConnectionString, new DataContextBuilder().UseQuotedIdentifiers());

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd, bool cached)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, cached, CancellationToken.None)).DbCommand.CommandText);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => SqlOf(ctx, cmd, false);

    // ------------------------------------------------------------------ name shapes (R237-04)

    /// <summary>
    /// A simple, numeric, whitespace, Unicode and reserved-word name are all emitted as a single quoted
    /// identifier even though identifier quoting is off for this context.
    /// </summary>
    [Fact]
    public void Column_UnquotedMode_QuotesEverySimpleNameShape()
    {
        using var ctx = CreateContext();

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "name") }))
            .Should().Be("select [name] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<int>(t, "1") }))
            .Should().Be("select [1] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "with space") }))
            .Should().Be("select [with space] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "λambda") }))
            .Should().Be("select [λambda] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "order") }))
            .Should().Be("select [order] as [C]\n from sales");
    }

    /// <summary>
    /// The provider delimiter and the SQL-looking shapes: a dot stays one literal name (no qualified
    /// path), a payload is not executed, and a literal <c>[1]</c> is a three-character name.
    /// </summary>
    [Fact]
    public void Column_UnquotedMode_KeepsDelimitersAndPayloadInsideOneIdentifier()
    {
        using var ctx = CreateContext();

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "a.b") }))
            .Should().Be("select [a.b] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "a]b") }))
            .Should().Be("select [a]]b] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "0+(SELECT COUNT(*) FROM sys.tables)") }))
            .Should().Be("select [0+(SELECT COUNT(*) FROM sys.tables)] as [C]\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "[1]") }))
            .Should().Be("select [[1]]] as [C]\n from sales");
    }

    /// <summary>
    /// Turning identifier quoting on changes only the surrounding query, not the number of escaping
    /// passes applied to the by-name column (one, on both settings).
    /// </summary>
    [Fact]
    public void Column_QuotedMode_UsesTheSameOneQuotedIdentifier()
    {
        using var ctx = CreateQuotedContext();

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "name") }))
            .Should().Be("select [name] as [C]\n from [sales]");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "a]b") }))
            .Should().Be("select [a]]b] as [C]\n from [sales]");
    }

    // ------------------------------------------------------------------ reprepare / cache (R237-08)

    /// <summary>
    /// Two by-name queries with different names keep their own identifier through the plan cache: a
    /// cached re-prepare of the first never reuses the second one's name, and a repeated prepare is
    /// stable.
    /// </summary>
    [Fact]
    public void Column_ReprepareAndCache_DoNotReuseAForeignIdentifier()
    {
        using var ctx = CreateContext();

        string Alpha(bool cached) => SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "alpha") }), cached);
        string Beta(bool cached) => SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "beta") }), cached);

        Alpha(false).Should().Be("select [alpha] as [C]\n from sales");
        Beta(false).Should().Be("select [beta] as [C]\n from sales");

        Alpha(true).Should().Be(Alpha(false));
        Beta(true).Should().Be(Beta(false));
        Alpha(true).Should().NotBe(Beta(true));
    }

    // ------------------------------------------------------------------ preserved validation (R237-07)

    /// <summary>A name that is not a compile-time constant is still rejected by the by-name route.</summary>
    [Fact]
    public void Column_NonConstantName_IsStillRejected()
    {
        using var ctx = CreateContext();
        var runtimeName = "alpha";

        var act = () => SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, runtimeName) }));

        act.Should().Throw<Exception>().WithMessage("*Column*");
    }

    /// <summary>A non-source argument is still rejected instead of emitting an unqualified identifier.</summary>
    [Fact]
    public void Column_NonSourceArgument_IsStillRejected()
    {
        using var ctx = CreateContext();

        var act = () => SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<int>("not-a-source", "id") }));

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*entity source parameter*");
    }

    /// <summary>
    /// The in-memory provider has no column-name concept and still rejects the by-name route. It
    /// compiles the projection rather than translating it, so <see cref="SqlFunctions.Column{T}"/>
    /// itself throws once a row is present. Pins exactly the behaviour of
    /// <c>InMemoryTests.ColumnByName_ShouldThrowClearNotSupported</c>
    /// (<c>tests/nextorm.core.tests/InMemory/InMemoryTests.cs:1021</c>) — no product change here.
    /// </summary>
    [Fact]
    public void InMemory_ColumnByName_IsStillRejected()
    {
        using var ctx = new InMemoryDataContext();
        // Seed a row so the in-memory provider actually evaluates the projection. With no data it
        // yields no rows and never invokes SqlFunctions.Column, so no rejection would be observed
        // (the existing InMemoryTests case seeds SimpleEntity the same way in its constructor).
        ctx.From<SimpleEntity>().WithData([new SimpleEntity { Id = 1 }]);

        var act = () => ctx.From<SimpleEntity>()
            .Select(it => new { R = SqlFunctions.Column<int>(it, "id") })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory*");
    }
}
