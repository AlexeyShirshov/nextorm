using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// D193.3 — ClickHouse rendering of the tuple value-list membership path (#193):
/// <c>tuple(a, b) IN (tuple(@p0, @p1), tuple(@p2, @p3))</c>, plus the distributed <c>GLOBAL IN</c>
/// variant through <see cref="ClickHouseFunctions.global_in{T}(T, IEnumerable{T})"/>.
/// </summary>
public class D193TupleInSqlGenerationTests
{
    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Prepare(ctx, cmd).DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void TupleContains_CapturedList_ShouldRenderTupleValueList()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)> { (1, 10), (2, 20) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("tuple(id, code) in (tuple(@p0, @p1), tuple(@p2, @p3))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3");
    }

    [Fact]
    public void TupleGlobalIn_CapturedList_ShouldRenderGlobalIn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)> { (1, 10), (2, 20) };

        var command = Prepare(ctx, e
            .Where(x => SqlFunctions.ClickHouse.global_in(new ValueTuple<long, int>(x.Id, x.Code), tuples))
            .Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("tuple(id, code) global in (tuple(@p0, @p1), tuple(@p2, @p3))");
    }

    [Fact]
    public void TupleContains_Empty_ShouldRenderAlwaysFalse()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)>();

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n").Should().Contain("1 = 0");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void TupleContains_NullableComponent_ShouldGuardAndUseNullArm()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var tuples = new List<(long, int?)> { (2, 1), (1, null) };

        var sql = SqlOf(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int?>(x.Id, x.Int))).Select(x => new { x.Id }));

        sql.Should().Contain("nullableint is not null and tuple(id, nullableint) in (tuple(@p0, @p1))");
        sql.Should().Contain("(id is not null and id = @p2 and nullableint is null)");
    }
}

[SqlTable("tuple_in_entity")]
public interface ITupleInMembershipEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("code")]
    int Code { get; set; }
}
