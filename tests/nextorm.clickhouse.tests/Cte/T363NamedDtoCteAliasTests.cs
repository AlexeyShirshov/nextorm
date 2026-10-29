using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// #350: the same named-DTO CTE alias contract on ClickHouse. A missing declaration alias is not a
/// collation issue here — ClickHouse reports <c>UNKNOWN_IDENTIFIER</c> — so the generated SQL is
/// asserted directly and the table-source shape is pinned byte-for-byte.
/// </summary>
[Trait("Issue", "350")]
public class T363NamedDtoCteAliasTests
{
    public sealed class DtoRow
    {
        public long Id { get; init; }
        public int? Int { get; init; }
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void Named_projection_over_a_table_source_should_stay_byte_identical()
    {
        using var ctx = ClickHouseTestContext.Create();

        var inner = ctx.From<IComplexEntity>().Where(x => x.Id > 1)
            .Select(x => new DtoRow { Id = x.Id, Int = x.Int })
            .AsCte("inner");

        var sql = SqlOf(ctx, ctx.From(inner).Select(x => new { x.Id }));

        sql.Should().Be(
            "with inner as (select id as `Id`, nullableint as `Int`\n" +
            " from complex_entity\n" +
            " where (id > 1)) select `Id`\n" +
            " from inner as `t1`");
    }

    [Fact]
    public void Named_projection_over_a_cte_source_should_alias_to_the_property_names()
    {
        using var ctx = ClickHouseTestContext.Create();

        var source = ctx.From<IComplexEntity>().Select(x => new { id = x.Id, num = x.Int }).AsCte("source");
        var named = ctx.From(source).Select(x => new DtoRow { Id = x.id, Int = x.num }).AsCte("named");

        var sql = SqlOf(ctx, ctx.From(named).Select(x => new { x.Id }));

        sql.Should().Contain("named as (select `id` as `Id`, `num` as `Int`");
        sql.Should().Contain("select `Id`\n from named as");
    }
}
