using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// #237 (T237) always-quoted contract of <see cref="SqlFunctions.Column{T}"/> on MariaDB: the raw name
/// is quoted with backticks regardless of the command's <c>QuoteIdentifiers</c> option (the MariaDB
/// dialect reuses the MySQL quoting rules; these are its own no-database oracles). Full-SQL oracles over
/// a placeholder connection string; no connection is opened.
/// </summary>
public class MariaDbIdentifierSafetyTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    /// <summary>Identifier quoting off: the by-name column is still one backtick-quoted identifier.</summary>
    [Fact]
    public void Column_UnquotedMode_QuotesTheRawName()
    {
        using var ctx = MariaDbTestContext.Create();

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "name") }))
            .Should().Be("select `name` as `C`\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<int>(t, "1") }))
            .Should().Be("select `1` as `C`\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "with space") }))
            .Should().Be("select `with space` as `C`\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "order") }))
            .Should().Be("select `order` as `C`\n from sales");
    }

    /// <summary>The dot, the provider delimiter and a SQL-looking payload stay inside one identifier.</summary>
    [Fact]
    public void Column_UnquotedMode_KeepsPunctuationInsideOneIdentifier()
    {
        using var ctx = MariaDbTestContext.Create();

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "a.b") }))
            .Should().Be("select `a.b` as `C`\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "a`b") }))
            .Should().Be("select `a``b` as `C`\n from sales");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "0+(SELECT COUNT(*) FROM sys.tables)") }))
            .Should().Be("select `0+(SELECT COUNT(*) FROM sys.tables)` as `C`\n from sales");
    }

    /// <summary>Identifier quoting on: the same single escaping pass for the column identifier.</summary>
    [Fact]
    public void Column_QuotedMode_QuotesTheNameExactlyOnce()
    {
        using var ctx = MariaDbTestContext.CreateQuoted();

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "name") }))
            .Should().Be("select `name` as `C`\n from `sales`");

        SqlOf(ctx, ctx.From("sales").Select(t => new { C = SqlFunctions.Column<string>(t, "a`b") }))
            .Should().Be("select `a``b` as `C`\n from `sales`");
    }
}
