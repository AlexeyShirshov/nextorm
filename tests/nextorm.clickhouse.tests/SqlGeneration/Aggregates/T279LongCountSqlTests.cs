using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// #279: a selector-free <c>LongCount()</c> in a correlated projection is an aggregate terminal, so it
/// renders an aggregate scalar subquery instead of the receiver's entity projection. ClickHouse keeps its
/// count-result wrapper: <c>toInt64(count(*))</c> for the wide count versus <c>toInt32(count(*))</c> for
/// <c>Count()</c>, with <c>limit 1</c> as the single-row restriction. These tests never open a database
/// connection.
/// </summary>
public class T279LongCountSqlTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void LongCountProjection_ShouldRenderWideAggregateScalarSubquery()
    {
        using var ctx = ClickHouseTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == it.Id).LongCount()
        }));

        sql.Should().Be("select t1.id, (select toInt64(count(*))\n from simple_entity as `t2`\n"
            + " where cast(t2.id as Int64) = t1.id\n"
            + "limit 1) as `c`\n from complex_entity as `t1`");
        sql.Should().Contain("toInt64(count(*))");
        sql.Should().NotContain("toInt32(count(*))");
    }

    /// <summary>
    /// Sibling parity: <c>Count()</c> keeps rendering its own narrow wrapper, so the wide wrapper is
    /// introduced for <c>LongCount()</c> only.
    /// </summary>
    [Fact]
    public void CountProjection_ShouldStillRenderItsOwnAggregate()
    {
        using var ctx = ClickHouseTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var countSql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == it.Id).Count()
        }));

        countSql.Should().Be("select t1.id, (select toInt32(count(*))\n from simple_entity as `t2`\n"
            + " where cast(t2.id as Int64) = t1.id\n"
            + "limit 1) as `c`\n from complex_entity as `t1`");
        countSql.Should().NotContain("toInt64(");
    }

    /// <summary>
    /// The wide count counts rows, not a projected column: the subquery must not fall back to the
    /// receiver's entity projection and must not filter on a column value, so rows whose projected value
    /// is null are still counted.
    /// </summary>
    [Fact]
    public void LongCountProjection_ShouldCountRowsNotNullableColumns()
    {
        using var ctx = ClickHouseTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == it.Id).LongCount()
        }));

        sql.Should().Contain("count(*)");
        sql.Should().NotContain("(select id\n from simple_entity");
        sql.Should().NotContain("count(t2.");
        sql.Should().NotContain("is not null");
    }
}
