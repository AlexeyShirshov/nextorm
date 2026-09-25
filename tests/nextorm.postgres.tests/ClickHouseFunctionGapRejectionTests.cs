using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// The ClickHouse-native function gaps (issue #78) are rejected by PostgreSQL through the normal
/// <see cref="NotSupportedException"/> path instead of emitting an invalid native name.
/// </summary>
public class ClickHouseFunctionGapRejectionTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void ClickHouseNativeFunctions_OnPostgres_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var c = ctx.From<IComplexEntity>();
        var a = ctx.From<IArrayEntity>();

        var checks = new Action[]
        {
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.lower_utf8(x.String) })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.trim_left(x.String) })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.replace_regexp_all(x.String, "a", "b") })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.match(x.String, "a") })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.extract(x.String, "a") })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.split_by_string(",", x.String) })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.format_date_time(x.Datetime, "%Y") })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.now() })),
            () => SqlOf(ctx, a.Select(x => new { V = SqlFunctions.ClickHouse.array_concat(x.Tags, x.Tags) })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L)))) })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.group_bitmap(x.Id) })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.sum_map(x.String, x.Int))) })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.md5(x.String) })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.sha256(x.String) })),
            () => SqlOf(ctx, c.Select(x => new { V = SqlFunctions.ClickHouse.generate_ulid() }))
        };

        foreach (var check in checks)
            check.Should().Throw<NotSupportedException>();
    }
}
