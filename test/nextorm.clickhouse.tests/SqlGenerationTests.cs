using System.Data.Common;
using FluentAssertions;
using nextorm.core;

namespace nextorm.clickhouse.tests;

/// <summary>
/// Verifies the ClickHouse specific SQL dialect. These tests never open a database connection, so
/// they run on every build/CI.
/// </summary>
public class SqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void SelectBasic_ShouldProducePlainSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void StringConcat_ShouldUseConcatFunction()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = x.String + "/" + x.String }));

        sql.Should().Contain("concat(");
        sql.Should().NotContain("||");
    }

    [Fact]
    public void Parameter_ShouldUseAtPrefix()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var command = Prepare(ctx, e.Where(x => x.Id == NORM.Param<int>(0)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("id = @norm_p0");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("norm_p0");
    }

    [Fact]
    public void Paging_WithLimitAndOffset_ShouldUseLimitOffset()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Page(5, 10).Select(x => x.Id)).Should().EndWith("limit 5 offset 10");
    }

    [Fact]
    public void DateTrunc_ShouldUseClickHouseDateTrunc()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { M = NORM.SQL.date_trunc("month", x.Datetime) }))
            .Should().Contain("dateTrunc('month', dt)");
    }

    [Fact]
    public void DateAdd_ShouldUseClickHouseAddFunctions()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = NORM.SQL.date_add("day", 1, x.Datetime) }))
            .Should().Contain("addDays(dt, 1)");

        SqlOf(ctx, e.Select(x => new { D = NORM.SQL.date_add("decade", 2, x.Datetime) }))
            .Should().Contain("addYears(dt, (2) * 10)");
    }

    [Fact]
    public void EndOfMonth_ShouldUseToLastDayOfMonth()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { E = NORM.SQL.end_of_month(x.Datetime) }))
            .Should().Contain("toLastDayOfMonth(dt)");
    }

    [Fact]
    public void DateTimeAddMethods_ShouldUseClickHouseAddFunctions()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = x.Datetime!.Value.AddMonths(2) }))
            .Should().Contain("addMonths(dt, 2)");
    }

    [Fact]
    public void ScalarDateTimeProjection_ShouldPrepareWithoutMaterializerError()
    {
        // A bare (non-anonymous) DateTime projection must take the single-column path; before the fix
        // it fell into RowMaterializerBuilder, which tried to construct DateTime with no arguments.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => x.Datetime!.Value.AddMonths(2)))
            .Should().Contain("addMonths(dt, 2)");
    }

    [Fact]
    public void StringAgg_ShouldUseArrayStringConcatGroupArray()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => NORM.SQL.string_agg(x.String, ",")))
            .Should().Contain("arrayStringConcat(groupArray(somestring), ',')");
    }

    [Fact]
    public void BitAggregates_ShouldUseGroupBitFunctions()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = NORM.PG_SQL.bit_and(x.Id),
            O = NORM.PG_SQL.bit_or(x.Id),
            X = NORM.PG_SQL.bit_xor(x.Id)
        }));

        sql.Should().Contain("groupBitAnd(id)");
        sql.Should().Contain("groupBitOr(id)");
        sql.Should().Contain("groupBitXor(id)");
    }

    [Fact]
    public void StatisticalAggregates_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C = NORM.SQL.corr(x.Id, x.Id),
            P = NORM.SQL.covar_pop(x.Id, x.Id),
            S = NORM.SQL.covar_samp(x.Id, x.Id)
        }));

        sql.Should().Contain("corr(id, id)");
        sql.Should().Contain("covarPop(id, id)");
        sql.Should().Contain("covarSamp(id, id)");
    }

    [Fact]
    public void RegressionAggregates_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Action act = () => SqlOf(ctx, e.Select(x => new { R = NORM.PG_SQL.regr_slope(x.Id, x.Id) }));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ArgMinMax_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Min = NORM.CLK_SQL.arg_min(x.String, x.Datetime),
            Max = NORM.CLK_SQL.arg_max(x.String, x.Datetime)
        }));

        sql.Should().Contain("argMin(somestring, dt)");
        sql.Should().Contain("argMax(somestring, dt)");
    }

    [Fact]
    public void IfAggregates_ShouldUseIfCombinators()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C = NORM.CLK_SQL.count_if(() => x.Id > 0L),
            S = NORM.CLK_SQL.sum_if(x.Id, () => x.Id > 0L),
            A = NORM.CLK_SQL.avg_if(x.Id, () => x.Id > 0L),
            Mi = NORM.CLK_SQL.min_if(x.Id, () => x.Id > 0L),
            Ma = NORM.CLK_SQL.max_if(x.Id, () => x.Id > 0L)
        }));

        sql.Should().Contain("countIf((id > 0))");
        sql.Should().Contain("sumIf(id, (id > 0))");
        sql.Should().Contain("avgIf(id, (id > 0))");
        sql.Should().Contain("minIf(id, (id > 0))");
        sql.Should().Contain("maxIf(id, (id > 0))");
    }

    [Fact]
    public void GroupByCube_ShouldUseWithCube()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupByCube(x => new { x.Int, x.Boolean })
            .Select(x => new { x.Int, x.Boolean }))
            .Should().Contain("group by nullableint, b with cube");
    }

    [Fact]
    public void DateFromParts_ShouldEmitMakeDate()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = NORM.SQL.date_from_parts(2023, 1, 31) }))
            .Should().Contain("makeDate(2023, 1, 31)");
    }

    [Fact]
    public void IndexOfLastIndexOf_ShouldUsePositionUTF8()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b") }))
            .Should().Contain("case when (positionUTF8(somestring, 'b')) = 0 then -1 else (positionUTF8(somestring, 'b')) - 1 end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.IndexOf("b", 1) }))
            .Should().Contain("case when (positionUTF8(somestring, 'b', 1 + 1)) = 0 then -1 else (positionUTF8(somestring, 'b', 1 + 1)) - 1 end");
        SqlOf(ctx, e.Select(x => new { V = x.String!.LastIndexOf("b") }))
            .Should().Contain("case when (positionUTF8(reverseUTF8(somestring), reverseUTF8('b'))) = 0 then -1 else lengthUTF8(somestring) - (positionUTF8(reverseUTF8(somestring), reverseUTF8('b'))) - lengthUTF8('b') + 1 end");
    }

    [Fact]
    public void PadLeftRightAndNewString_ShouldUseRepeat()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.PadRight(5, '0') }))
            .Should().Contain("case when lengthUTF8(somestring) >= (5) then somestring else concat(somestring, repeat('0', (5) - lengthUTF8(somestring))) end");
        SqlOf(ctx, e.Select(x => new { V = new string('*', 4) }))
            .Should().Contain("repeat('*', 4)");
    }

    [Fact]
    public void RemoveInsert_ShouldSpliceWithSubstring()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { V = x.String!.Remove(2, 1) }))
            .Should().Contain("concat(substring(somestring, 0 + 1, 2), '', substring(somestring, (2) + (1) + 1, lengthUTF8(somestring) - ((2) + (1))))");
        SqlOf(ctx, e.Select(x => new { V = x.String!.Insert(2, "x") }))
            .Should().Contain("concat(substring(somestring, 0 + 1, 2), 'x', substring(somestring, 2 + 1, lengthUTF8(somestring) - (2)))");
    }
}
