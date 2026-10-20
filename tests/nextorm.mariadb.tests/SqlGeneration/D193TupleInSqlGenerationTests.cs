using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// D193.3 — MariaDB rendering of the tuple value-list membership path (#193). MariaDB shares the MySQL
/// dialect, so the row value list is <c>(a, b) IN ((@p0, @p1), (@p2, @p3))</c>.
/// </summary>
public class D193TupleInSqlGenerationTests
{
    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Prepare(ctx, cmd).DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void TupleContains_CapturedList_ShouldRenderRowValueList()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)> { (1, 10), (2, 20) };

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code) in ((@p0, @p1), (@p2, @p3))");
        command.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName)
            .Should().Equal("p0", "p1", "p2", "p3");
    }

    [Fact]
    public void TupleIn_AtInForm_ShouldRenderRowValueList()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<Tuple<long, int>> { Tuple.Create(1L, 10), Tuple.Create(2L, 20) };

        var command = Prepare(ctx, e
            .Where(x => SqlFunctions.Sql.@in(Tuple.Create(x.Id, x.Code), tuples))
            .Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n")
            .Should().Contain("(id, code) in ((@p0, @p1), (@p2, @p3))");
    }

    [Fact]
    public void TupleContains_Empty_ShouldRenderAlwaysFalse()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<ITupleInMembershipEntity>();
        var tuples = new List<(long, int)>();

        var command = Prepare(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int>(x.Id, x.Code))).Select(x => new { x.Id }));

        command.DbCommand.CommandText.Replace("\r\n", "\n").Should().Contain("1 = 0");
        command.DbCommandParams.Cast<DbParameter>().Should().BeEmpty();
    }

    [Fact]
    public void TupleContains_NullableComponent_ShouldGuardAndUseNullArm()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var tuples = new List<(long, int?)> { (2, 1), (1, null) };

        var sql = SqlOf(ctx, e.Where(x => tuples.Contains(new ValueTuple<long, int?>(x.Id, x.Int))).Select(x => new { x.Id }));

        sql.Should().Contain("nullableint is not null and (id, nullableint) in ((@p0, @p1))");
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
