using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// #327 / AC-R6: ClickHouse's <c>LIKE</c> already treats the backslash as the escape character and its
/// parser rejects an <c>ESCAPE</c> clause in every spelling (measured live), so no clause may be
/// emitted for the escaped wildcards of a constant pattern. A caller-supplied escape character cannot
/// be expressed at all and fails closed instead of producing unparsable SQL. No database is touched.
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
    public void Contains_WithWildcardPattern_ShouldOmitTheEscapeClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.String!.Contains("a%b_c")).Select(x => new { x.Id }));

        sql.Should().Contain("somestring like '%a\\\\%b\\\\_c%'",
            "ClickHouse escapes the backslash inside the literal, so the pattern reaches LIKE as \\% and \\_");
        sql.Should().NotContain("escape",
            "ClickHouse never accepts the ESCAPE clause; the default backslash escaping already applies");
    }

    [Fact]
    [Trait("Issue", "327")]
    public void LikeFunction_WithCustomEscapeChar_ShouldFailClosed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => SqlFunctions.Sql.like(x.String, "%a!%", "!")).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*ClickHouse*")
            .WithMessage("*ESCAPE*");
    }
}
