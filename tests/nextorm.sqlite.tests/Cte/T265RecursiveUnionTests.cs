using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>Row shape of the recursive CTE used by the T265 baseline/compatibility tests.</summary>
public sealed class T265CteRow
{
    public int n { get; set; }
}

/// <summary>
/// Pre-change baselines (#265). A legacy <c>WithRecursive</c> declaration whose body is a distinct
/// <c>UNION</c> stays legal on SQLite; these tests pin the exact emitted SQL so the SQL Server
/// fail-closed guard cannot silently change another dialect. No database is used.
/// </summary>
public class T265RecursiveUnionTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    // Captured before any production edit (#265, D265-A). Newlines are LF here; the produced SQL is
    // normalized from CRLF to LF before the comparison.
    private const string ExpectedDistinctSql =
        "with recursive nums as (select id as 'n'\n from simple_entity\n where id = 1\n union \nselect (n + 1) as 'n'\n from nums\n where (n < 5)) select n\n from nums";

    private static QueryCommand<T265CteRow> BuildLegacyDistinct(IDataContext ctx)
    {
        var e = ctx.From<ISimpleEntity>();
        var anchor = e.Where(s => s.Id == 1).Select(s => new T265CteRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new T265CteRow { n = t["n"].AsInt + 1 });
        return ctx.WithRecursive("nums", anchor.Union(step)).From("nums").Select(t => new T265CteRow { n = t["n"].AsInt });
    }

    private static QueryCommand<T265CteRow> BuildLegacyAll(IDataContext ctx)
    {
        var e = ctx.From<ISimpleEntity>();
        var anchor = e.Where(s => s.Id == 1).Select(s => new T265CteRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new T265CteRow { n = t["n"].AsInt + 1 });
        return ctx.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => new T265CteRow { n = t["n"].AsInt });
    }

    private static QueryCommand<T265CteRow> BuildLegacyWithoutUnion(IDataContext ctx)
    {
        var body = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new T265CteRow { n = t["n"].AsInt + 1 });
        return ctx.WithRecursive("nums", body).From("nums").Select(t => new T265CteRow { n = t["n"].AsInt });
    }

    [Fact]
    public void CompatibilityDistinct_SqlIsByteIdentical()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, BuildLegacyDistinct(ctx));

        sql.Should().Be(ExpectedDistinctSql);
    }

    [Fact]
    public void CompatibilityAllAndNone_RemainUnchanged()
    {
        using var ctx = SqliteTestContext.Create();

        var all = SqlOf(ctx, BuildLegacyAll(ctx));
        all.Should().Contain(" union all ");

        var none = SqlOf(ctx, BuildLegacyWithoutUnion(ctx));
        none.Should().StartWith("with recursive nums as (").And.NotContain(" union ");
    }

    [Fact]
    public void DialectRestriction_DefaultsOff()
    {
        using var ctx = SqliteTestContext.CreateSqlite();

        ctx.Dialect.RequiresRecursiveCteUnionAll.Should().BeFalse();
    }
}
