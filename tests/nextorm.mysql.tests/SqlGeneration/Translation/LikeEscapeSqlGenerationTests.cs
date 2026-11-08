using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// #327: a constant <c>Contains</c>/<c>StartsWith</c> pattern has its LIKE wildcards escaped and then
/// needs a grammar-required <c>ESCAPE</c> operand. Both the pattern and the escape character are
/// charset-introduced UTF-8 hex literals, so the statement parses identically with and without
/// <c>NO_BACKSLASH_ESCAPES</c> and never relies on driver string escaping. No database is touched.
/// </summary>
public class LikeEscapeSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    [Trait("Issue", "327")]
    public void Contains_WithWildcardPattern_ShouldEmitHexPatternAndHexEscape()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        // "%a\%b\_c%" as UTF-8: 25 61 5c 25 62 5c 5f 63 25
        var sql = SqlOf(ctx, e.Where(x => x.String!.Contains("a%b_c")).Select(x => new { x.Id }));

        sql.Should().Contain("somestring like _utf8mb4 X'25615c25625c5f6325' escape _utf8mb4 X'5c'",
            "the wildcards are escaped inside a hex pattern and the escape character is the hex byte for the backslash");
        sql.Should().NotContain("escape '\\\\'",
            "a quoted escape token would be re-interpreted by NO_BACKSLASH_ESCAPES");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void Contains_WithoutWildcards_ShouldOmitTheEscapeClause()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String!.Contains("df")).Select(x => new { x.Id }));

        sql.Should().Contain("somestring like _utf8mb4 X'25646625'");
        sql.Should().NotContain("escape", "no wildcard was escaped, so no ESCAPE clause is required");
    }
}
