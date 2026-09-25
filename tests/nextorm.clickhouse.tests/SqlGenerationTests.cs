using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

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
    public void Pivot_ShouldThrowBecauseNotSupported()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e
            .Pivot(PivotAggregate.Count, s => s.Id, s => s.Id, PivotValue.Create("1"))
            .Select(t => new { V = t.GetNullableInt32("1") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*PIVOT*");
    }

    [Fact]
    public void IndexHint_ShouldThrowBecauseNotSupported()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.WithIndex("idx_id").Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Index hints*");
    }

    [Fact]
    public void ForUpdate_ShouldThrowBecauseClickHouseHasNoRowLocking()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.ForUpdate(LockWaitMode.SkipLocked).Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR UPDATE*");
    }

    [Fact]
    public void CrossApply_ToCorrelatedSubquery_ShouldThrowBecauseNoLateral()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>()
            .CrossApply(s => ctx.From<IComplexEntity>().Where(c => c.Id == s.Id).Select(c => new { c.Id, c.String }))
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        act.Should().Throw<NotSupportedException>().WithMessage("*CrossApply*");
    }

    [Fact]
    public void SelectBasic_ShouldProducePlainSelect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select id from simple_entity");
    }

    [Fact]
    public void QuotedIdentifiers_ShouldUseBackticks()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id })).Should().Be("select `id` from `simple_entity`");
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

        var command = Prepare(ctx, e.Where(x => x.Id == SqlFunctions.Parameter<int>(0)).Select(x => new { x.Id }));

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

        SqlOf(ctx, e.Select(x => new { M = SqlFunctions.Sql.date_trunc("month", x.Datetime) }))
            .Should().Contain("dateTrunc('month', dt)");
    }

    [Fact]
    public void DateAdd_ShouldUseClickHouseAddFunctions()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("day", 1, x.Datetime) }))
            .Should().Contain("addDays(dt, 1)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("decade", 2, x.Datetime) }))
            .Should().Contain("addYears(dt, (2) * 10)");

        // A sub-day part promotes the operand to DateTime64 so a Date-only value keeps its time.
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("milliseconds", 5, x.Datetime) }))
            .Should().Contain("addMilliseconds(toDateTime64(dt, 3), 5)");
    }

    [Fact]
    public void DateDiff_ShouldUseClickHouseDateDiff()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_diff("milliseconds", x.Datetime, x.Datetime) }))
            .Should().Contain("dateDiff('millisecond', dt, dt)");

        // The 64-bit variant keeps the same SQL; the result is read as Int64.
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_diff_big("milliseconds", x.Datetime, x.Datetime) }))
            .Should().Contain("dateDiff('millisecond', dt, dt)");
    }

    [Fact]
    public void EndOfMonth_ShouldUseToLastDayOfMonth()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { E = SqlFunctions.Sql.end_of_month(x.Datetime) }))
            .Should().Contain("toLastDayOfMonth(dt)");
    }

    [Fact]
    public void DateTimeAddMethods_ShouldUseClickHouseAddFunctions()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = x.Datetime!.Value.AddMonths(2) }))
            .Should().Contain("addMonths(dt, 2)");

        // AddMilliseconds promotes the operand through the timestamp-promotion hook.
        SqlOf(ctx, e.Select(x => new { D = x.Datetime!.Value.AddMilliseconds(500) }))
            .Should().Contain("addMilliseconds(toDateTime64(dt, 3), 500)");
    }

    [Fact]
    public void DateTimePart_ShouldUseToDayOfYear()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { DOY = x.Datetime!.Value.DayOfYear }))
            .Should().Contain("toInt32(toDayOfYear(dt))");
    }

    [Fact]
    public void DateTimeParts_ShouldUseToAccessors()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Y = x.Datetime!.Value.Year,
            M = x.Datetime!.Value.Month,
            D = x.Datetime!.Value.Day,
            H = x.Datetime!.Value.Hour
        }));

        sql.Should().Contain("toInt32(toYear(dt))");
        sql.Should().Contain("toInt32(toMonth(dt))");
        sql.Should().Contain("toInt32(toDayOfMonth(dt))");
        sql.Should().Contain("toInt32(toHour(dt))");
    }

    [Fact]
    public void DateConversionFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            D = SqlFunctions.ClickHouse.to_date(x.Datetime),
            DT = SqlFunctions.ClickHouse.to_date_time(x.String),
            D32 = SqlFunctions.ClickHouse.to_date32(x.String),
            Y = SqlFunctions.ClickHouse.to_year(x.Datetime),
            Q = SqlFunctions.ClickHouse.to_quarter(x.Datetime),
            M = SqlFunctions.ClickHouse.to_month(x.Datetime),
            DOM = SqlFunctions.ClickHouse.to_day_of_month(x.Datetime),
            DOW = SqlFunctions.ClickHouse.to_day_of_week(x.Datetime),
            DOY = SqlFunctions.ClickHouse.to_day_of_year(x.Datetime),
            H = SqlFunctions.ClickHouse.to_hour(x.Datetime),
            Mi = SqlFunctions.ClickHouse.to_minute(x.Datetime),
            S = SqlFunctions.ClickHouse.to_second(x.Datetime),
            SY = SqlFunctions.ClickHouse.to_start_of_year(x.Datetime),
            SQ = SqlFunctions.ClickHouse.to_start_of_quarter(x.Datetime),
            SM = SqlFunctions.ClickHouse.to_start_of_month(x.Datetime),
            SW = SqlFunctions.ClickHouse.to_start_of_week(x.Datetime),
            SD = SqlFunctions.ClickHouse.to_start_of_day(x.Datetime),
            SH = SqlFunctions.ClickHouse.to_start_of_hour(x.Datetime),
            SMin = SqlFunctions.ClickHouse.to_start_of_minute(x.Datetime),
            SSec = SqlFunctions.ClickHouse.to_start_of_second(x.Datetime),
            Mon = SqlFunctions.ClickHouse.to_monday(x.Datetime),
            YM = SqlFunctions.ClickHouse.to_yyyymm(x.Datetime),
            YMD = SqlFunctions.ClickHouse.to_yyyymmdd(x.Datetime),
            U = SqlFunctions.ClickHouse.to_unix_timestamp(x.Datetime)
        }));

        sql.Should().Contain("toDate(dt)");
        sql.Should().Contain("toDateTime(somestring)");
        sql.Should().Contain("toDate32(somestring)");
        sql.Should().Contain("toYear(dt)");
        sql.Should().Contain("toQuarter(dt)");
        sql.Should().Contain("toMonth(dt)");
        sql.Should().Contain("toDayOfMonth(dt)");
        sql.Should().Contain("toDayOfWeek(dt)");
        sql.Should().Contain("toDayOfYear(dt)");
        sql.Should().Contain("toHour(dt)");
        sql.Should().Contain("toMinute(dt)");
        sql.Should().Contain("toSecond(dt)");
        sql.Should().Contain("toStartOfYear(dt)");
        sql.Should().Contain("toStartOfQuarter(dt)");
        sql.Should().Contain("toStartOfMonth(dt)");
        sql.Should().Contain("toStartOfWeek(dt)");
        sql.Should().Contain("toStartOfDay(dt)");
        sql.Should().Contain("toStartOfHour(dt)");
        sql.Should().Contain("toStartOfMinute(dt)");
        sql.Should().Contain("toStartOfSecond(dt)");
        sql.Should().Contain("toMonday(dt)");
        sql.Should().Contain("toInt32(toYYYYMM(dt))");
        sql.Should().Contain("toInt32(toYYYYMMDD(dt))");
        sql.Should().Contain("toInt64(toUnixTimestamp(dt))");
    }

    [Fact]
    public void Extract_ShouldUseClickHouseDatePartForms()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { Q = SqlFunctions.Sql.extract("quarter", x.Datetime) }))
            .Should().Contain("toQuarter(dt)");
        SqlOf(ctx, e.Select(x => new { W = SqlFunctions.Sql.extract("week", x.Datetime) }))
            .Should().Contain("toISOWeek(dt)");
        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.extract("dow", x.Datetime) }))
            .Should().Contain("(toDayOfWeek(dt) % 7)");
        SqlOf(ctx, e.Select(x => new { I = SqlFunctions.Sql.extract("isodow", x.Datetime) }))
            .Should().Contain("toDayOfWeek(dt)");
        SqlOf(ctx, e.Select(x => new { E = SqlFunctions.Sql.date_part("epoch", x.Datetime) }))
            .Should().Contain("toFloat64(toUnixTimestamp(dt))");
    }

    [Fact]
    public void SetSeed_ShouldThrowBecauseClickHouseHasNoStandaloneSeed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { S = SqlFunctions.Postgres.setseed(0.5) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*setseed*");
    }

    [Fact]
    public void CryptoHash_ShouldThrowBecauseOnlyPostgresHasIt()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { H = SqlFunctions.Postgres.digest("abc", "sha256") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*digest/sha256*");
    }

    [Fact]
    public void PostgresTableFunctions_ShouldThrowBecauseOnlyPostgresHasThem()
    {
        using var ctx = ClickHouseTestContext.Create();
        var json = """{"a":1}""";
        var source = "a,b";
        var pattern = ",";

        var arrayElements = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Postgres.jsonb_array_elements(json))
            .Select(r => new { r.Value }));
        arrayElements.Should().Throw<NotSupportedException>().WithMessage("*jsonb_array_elements*");

        var each = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Postgres.jsonb_each(json))
            .Select(r => new { r.Key }));
        each.Should().Throw<NotSupportedException>().WithMessage("*jsonb_each*");

        var split = () => SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Postgres.regexp_split_to_table(source, pattern))
            .Select(r => new { r.Value }));
        split.Should().Throw<NotSupportedException>().WithMessage("*regexp_split_to_table*");
    }

    [Fact]
    public void ArrayShuffle_ShouldThrowBecausePostgresArraySurfaceIsGated()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e
            .Where(x => SqlFunctions.Postgres.array_shuffle(SqlFunctions.Parameter<long[]>(0)) != null)
            .Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Arrays are not supported*");
    }

    [Fact]
    public void PercentRankCumeDist_ShouldEmitOverWithOrder()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            pr = SqlFunctions.Sql.percent_rank().Over(SqlFunctions.Sql.asc(() => x.Id)),
            cd = SqlFunctions.Sql.cume_dist().Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Contain("percent_rank() over (order by id)");
        sql.Should().Contain("cume_dist() over (order by id)");
    }

    [Fact]
    public void Iif_ShouldUseIfFunction()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.iif(x.Id > 0L, "yes", "no") }));

        sql.Should().Contain("if(");
        sql.Should().Contain("'yes', 'no')");
    }

    [Fact]
    public void MultiIf_ShouldRenderConditionValuePairsWithElseLast()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            B = SqlFunctions.ClickHouse.multi_if(
                SqlFunctions.ClickHouse.when(x.Id == 1L, "one"),
                SqlFunctions.ClickHouse.when(x.Id == 2L, "two"),
                SqlFunctions.ClickHouse.otherwise("many"))
        }));

        sql.Should().Contain("multiIf((id = 1), 'one', (id = 2), 'two', 'many')");
    }

    [Fact]
    public void MultiIf_WithCapturedArray_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var branches = new ClickHouseFunctions.MultiIfBranch<string>[2];

        Action act = () => SqlOf(ctx, e.Select(x => new { B = SqlFunctions.ClickHouse.multi_if(branches) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*inline*");
    }

    [Fact]
    public void MultiIf_WithoutOtherwise_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Action act = () => SqlOf(ctx, e.Select(x => new
        {
            B = SqlFunctions.ClickHouse.multi_if(
                SqlFunctions.ClickHouse.when(x.Id == 1L, "one"))
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*otherwise*");
    }

    [Fact]
    public void LagInFrame_ShouldUseClickHouseNameAndRespectFrame()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            P = SqlFunctions.ClickHouse.lag_in_frame(x.Id, 1).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.Rows(WindowFrameBound.CurrentRow, WindowFrameBound.Following(1)))
        }));

        sql.Should().Contain("lagInFrame(id, 1) over (order by id rows between current row and 1 following)");
    }

    [Fact]
    public void LeadInFrame_ShouldUseClickHouseNameWithDefault()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            N = SqlFunctions.ClickHouse.lead_in_frame(x.Id, 1, 0L).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.Rows(WindowFrameBound.Preceding(1), WindowFrameBound.CurrentRow))
        }));

        sql.Should().Contain("leadInFrame(id, 1, 0) over (order by id rows between 1 preceding and current row)");
    }

    [Fact]
    public void InFrameWindowFunction_WithoutOver_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Action act = () => SqlOf(ctx, e.Select(x => new { P = SqlFunctions.ClickHouse.lag_in_frame(x.Id) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Over*");
    }


    [Fact]
    public void NthValue_ShouldEmitOverWithOrder()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            V = SqlFunctions.Sql.nth_value(x.Id, 2).Over(SqlFunctions.Sql.asc(() => x.Id))
        }));

        sql.Should().Contain("nth_value(id, 2) over (order by id)");
    }

    [Fact]
    public void WindowFrameGroups_ShouldEmitGroupsUnit()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.Groups(1, 1))
        }));

        sql.Should().Contain("sum(id) over (order by id groups between 1 preceding and 1 following)");
    }

    [Fact]
    public void NamedWindow_ShouldEmitWindowClauseAndOverReference()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var b = e.Window("w", partitionBy: [x => x.Int], orderBy: [e.Asc(x => x.Id)]);
        var sql = SqlOf(ctx, b.Select(x => new
        {
            x.Id,
            rn = SqlFunctions.Sql.row_number().Over("w")
        }));

        sql.Should().Contain("row_number() over w");
        sql.Should().Contain("window w as (partition by nullableint order by id)");
    }

    [Fact]
    public void WindowFrameExclusion_ShouldThrowBecauseClickHouseHasNoExclude()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            v = SqlFunctions.Sql.sum_over(x.Id).Over(
                SqlFunctions.Sql.asc(() => x.Id),
                WindowFrame.RowsUnboundedPrecedingToCurrentRow.WithExclusion(WindowFrameExclusion.CurrentRow))
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*EXCLUDE*");
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
    public void DateTimeConstructorLiteral_ShouldBindAsParameterNotConcatenate()
    {
        // new DateTime(2014, 3, 20) is a compile-time constant. Before the fix it fell through to
        // base.VisitNew, concatenating the constructor-argument literals into a meaningless numeric
        // literal ("2014320"); a DateTime column compared to that is not the intended predicate.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Datetime == new DateTime(2014, 3, 20)).Select(x => new { x.Id }));

        sql.Should().Contain(" where dt = @");
        sql.Should().NotContain("2014320");
    }

    [Fact]
    public void StringAgg_ShouldUseArrayStringConcatGroupArray()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.string_agg(x.String, ",")))
            .Should().Contain("arrayStringConcat(groupArray(somestring), ',')");
    }

    [Fact]
    public void FilteredStringAgg_ShouldUseGroupArrayIf()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.string_agg(x.String, ",", () => x.Id > 0L)))
            .Should().Contain("arrayStringConcat(groupArrayIf(somestring, (id > 0)), ',')");
    }

    [Fact]
    public void GroupArray_ShouldRenderGroupArray()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.group_array(x.Id) }))
            .Should().Contain("groupArray(id)");
    }

    [Fact]
    public void GroupUniqArray_ShouldRenderGroupUniqArray()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.group_uniq_array(x.Id) }))
            .Should().Contain("groupUniqArray(id)");
    }

    [Fact]
    public void ArrayColumn_ShouldProjectDirectly()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => x.Nums)).Should().Be("select nums from array_entity");
    }

    [Fact]
    public void ArrayExpression_ShouldProjectWithoutWrapper()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_sort(x.Nums)))
            .Should().Contain("arraySort(nums)");
    }

    [Fact]
    public void ArrayMap_ShouldRenderArrayMap()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_map(v => -v, x.Nums)))
            .Should().Contain("arrayMap(v -> -(v), nums)");
    }

    [Fact]
    public void ArrayFilter_ShouldRenderArrayFilter()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_filter(v => v > 2, x.Nums)))
            .Should().Contain("arrayFilter(v -> (v > 2), nums)");
    }

    [Fact]
    public void ArrayExists_ShouldRenderArrayExists()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_exists(v => v > 2, x.Nums)))
            .Should().Contain("arrayExists(v -> (v > 2), nums)");
    }

    [Fact]
    public void ArrayAll_ShouldRenderArrayAll()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_all(v => v > 2, x.Nums)))
            .Should().Contain("arrayAll(v -> (v > 2), nums)");
    }

    [Fact]
    public void ArrayCount_ShouldRenderArrayCount()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_count(v => v > 2, x.Nums)))
            .Should().Contain("arrayCount(v -> (v > 2), nums)");
    }

    [Fact]
    public void ArrayFirstAndLast_ShouldRenderArrayFirstAndLast()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.array_first(v => v > 2, x.Nums),
            FI = SqlFunctions.ClickHouse.array_first_index(v => v > 2, x.Nums),
            L = SqlFunctions.ClickHouse.array_last(v => v > 2, x.Nums),
            LI = SqlFunctions.ClickHouse.array_last_index(v => v > 2, x.Nums)
        }));

        sql.Should().Contain("arrayFirst(v -> (v > 2), nums)");
        sql.Should().Contain("arrayFirstIndex(v -> (v > 2), nums)");
        sql.Should().Contain("arrayLast(v -> (v > 2), nums)");
        sql.Should().Contain("arrayLastIndex(v -> (v > 2), nums)");
    }

    [Fact]
    public void NestedHigherOrderLambda_ShouldReferenceOuterParameter()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_map(
                a => SqlFunctions.ClickHouse.array_exists(b => a == b, x.Nums),
                x.Nums)))
            .Should().Contain("arrayMap(a -> arrayExists(b -> (a = b), nums), nums)");
    }

    [Fact]
    public void HigherOrderLambda_WithMemberAccessOnParameter_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_filter(s => s.Length > 0, x.Tags)));

        act.Should().Throw<NotSupportedException>().WithMessage("*Member access*");
    }

    [Fact]
    public void HigherOrderLambda_NotInline_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();
        System.Linq.Expressions.Expression<Func<long, long>> function = v => -v;

        var act = () => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_map<long, long>(function, x.Nums)));

        act.Should().Throw<NotSupportedException>().WithMessage("*inline lambda*");
    }

    [Fact]
    public void ArrayFilter_ShouldNestInsideAnotherArrayFunction()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_sort(
                SqlFunctions.ClickHouse.array_filter(v => v > 2, x.Nums))))
            .Should().Contain("arraySort(arrayFilter(v -> (v > 2), nums))");
    }

    [Fact]
    public void BitAggregates_ShouldUseGroupBitFunctions()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Postgres.bit_and(x.Id),
            O = SqlFunctions.Postgres.bit_or(x.Id),
            X = SqlFunctions.Postgres.bit_xor(x.Id)
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
            C = SqlFunctions.Sql.corr(x.Id, x.Id),
            P = SqlFunctions.Sql.covar_pop(x.Id, x.Id),
            S = SqlFunctions.Sql.covar_samp(x.Id, x.Id)
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

        Action act = () => SqlOf(ctx, e.Select(x => new { R = SqlFunctions.Postgres.regr_slope(x.Id, x.Id) }));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SessionInfoFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            CU = SqlFunctions.Sql.current_user(),
            CD = SqlFunctions.Sql.current_database(),
            Ver = SqlFunctions.Sql.version()
        }));

        sql.Should().Contain("currentUser()");
        sql.Should().Contain("currentDatabase()");
        sql.Should().Contain("version()");
    }

    [Fact]
    public void UuidGenerators_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            U = SqlFunctions.Sql.gen_random_uuid(),
            V7 = SqlFunctions.Sql.uuidv7()
        }));

        sql.Should().Contain("generateUUIDv4()");
        sql.Should().Contain("generateUUIDv7()");
    }

    [Fact]
    public void SessionInfoFunctions_ShouldThrowForSessionUserAndSchema()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Action user = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.session_user() }));
        Action schema = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.current_schema() }));

        user.Should().Throw<NotSupportedException>().WithMessage("*session_user*");
        schema.Should().Throw<NotSupportedException>().WithMessage("*current_schema*");
    }

    [Fact]
    public void ArgMinMax_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Min = SqlFunctions.ClickHouse.arg_min(x.String, x.Datetime),
            Max = SqlFunctions.ClickHouse.arg_max(x.String, x.Datetime)
        }));

        sql.Should().Contain("argMin(somestring, dt)");
        sql.Should().Contain("argMax(somestring, dt)");
    }

    [Fact]
    public void UniqAggregates_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            U = SqlFunctions.ClickHouse.uniq(x.String),
            E = SqlFunctions.ClickHouse.uniq_exact(x.String),
            C = SqlFunctions.ClickHouse.uniq_combined(x.String),
            H = SqlFunctions.ClickHouse.uniq_hll12(x.String)
        }));

        sql.Should().Contain("toInt64(uniq(somestring))");
        sql.Should().Contain("toInt64(uniqExact(somestring))");
        sql.Should().Contain("toInt64(uniqCombined(somestring))");
        sql.Should().Contain("toInt64(uniqHLL12(somestring))");
    }

    [Fact]
    public void CountAggregates_ShouldCastToClrInteger()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            N = SqlFunctions.Sql.count(),
            Big = SqlFunctions.Sql.count_big(),
            D = SqlFunctions.Sql.count_distinct(x.Int),
            BigD = SqlFunctions.Sql.count_big_distinct(x.Int),
            F = SqlFunctions.Sql.count(() => x.Id > 0L)
        }));

        sql.Should().Contain("toInt32(count(*))");
        sql.Should().Contain("toInt64(count(*))");
        sql.Should().Contain("toInt32(count(distinct nullableint))");
        sql.Should().Contain("toInt64(count(distinct nullableint))");
        sql.Should().Contain("toInt32(countIf((id > 0)))");
    }

    [Fact]
    public void WindowCount_ShouldCastWholeWindowExpression()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            n = SqlFunctions.Sql.count_over().Over(partitionBy: () => x.Int)
        }));

        sql.Should().Contain("toInt32(count(*) over (partition by nullableint))");
    }

    [Fact]
    public void QuantileAggregates_ShouldUseDoubleParentheses()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Q = SqlFunctions.ClickHouse.quantile(0.5, x.Id),
            E = SqlFunctions.ClickHouse.quantile_exact(0.9, x.Id),
            T = SqlFunctions.ClickHouse.quantile_timing(0.5, x.Id),
            M = SqlFunctions.ClickHouse.median(x.Id),
            D = SqlFunctions.ClickHouse.median(x.Datetime!.Value)
        }));

        sql.Should().Contain("toFloat64(quantile(0.5)(id))");
        sql.Should().Contain("toFloat64(quantileExact(0.9)(id))");
        sql.Should().Contain("toFloat64(quantileTiming(0.5)(id))");
        sql.Should().Contain("toFloat64(median(id))");
        sql.Should().Contain("toFloat64(median(dt))");
    }

    [Fact]
    public void TopK_ShouldRenderTopK()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            T = SqlFunctions.ClickHouse.top_k(3, x.Id),
            W = SqlFunctions.ClickHouse.top_k_weighted(2, x.Id, x.Int)
        }));

        sql.Should().Contain("topK(3)(id)");
        sql.Should().Contain("topKWeighted(2)(id, nullableint)");
    }

    [Fact]
    public void Quantiles_ShouldRenderQuantiles()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Q = SqlFunctions.ClickHouse.quantiles(new[] { 0.25, 0.5, 0.75 }, x.Id)
        }));

        sql.Should().Contain("quantiles(0.25, 0.5, 0.75)(id)");
    }

    [Fact]
    public void Quantiles_WithCapturedLevels_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var levels = new[] { 0.25, 0.5 };

        var act = () => SqlOf(ctx, e.Select(x => new
        {
            Q = SqlFunctions.ClickHouse.quantiles(levels, x.Id)
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*inline*");
    }

    [Fact]
    public void TopK_WithCapturedK_ShouldParameteriseK()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var k = 3L;

        var sql = SqlOf(ctx, e.Select(x => new
        {
            T = SqlFunctions.ClickHouse.top_k(k, x.Id)
        }));

        sql.Should().Contain("topK(@k)(id)");
    }

    [Fact]
    public void TopK_WithCapturedK_ShouldRefreshParamsOnCachedPlan()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var k = 2L;

        QueryCommand<long[]> Build() => e.Select(x => SqlFunctions.ClickHouse.top_k(k, x.Id));

        _ = ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);
        var second = (DbPreparedQueryCommand<long[]>)ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);

        Normalize(second.DbCommand.CommandText).Should().Contain("topK(@k)(id)");
    }

    [Fact]
    public void Quantiles_WithCapturedFilter_ShouldRefreshParamsOnCachedPlan()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var min = 1L;

        QueryCommand<double[]> Build() => e
            .Where(x => x.Id > min)
            .Select(x => SqlFunctions.ClickHouse.quantiles(new[] { 0.5 }, x.Id));

        _ = ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);
        var second = (DbPreparedQueryCommand<double[]>)ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);

        Normalize(second.DbCommand.CommandText).Should().Contain("quantiles(0.5)(id)");
    }

    [Fact]
    public void AnyAggregates_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            F = SqlFunctions.Sql.any_agg(x.String),
            L = SqlFunctions.ClickHouse.any_last(x.String)
        }));

        sql.Should().Contain("any(somestring)");
        sql.Should().Contain("anyLast(somestring)");
    }

    [Fact]
    public void WindowFunnel_ShouldUseDoubleParenthesesAndCast()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            F = SqlFunctions.ClickHouse.window_funnel(3600, x.Datetime, x.Id <= 2, x.Id <= 5)
        }));

        sql.Should().Contain("toInt32(windowFunnel(3600)(dt, (id <= 2), (id <= 5)))");
    }

    [Fact]
    public void SequenceMatch_ShouldUseDoubleParenthesesAndCast()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            M = SqlFunctions.ClickHouse.sequence_match("(?1)(?2)", x.Datetime, x.Id <= 1, x.Id <= 2)
        }));

        sql.Should().Contain("toInt32(sequenceMatch('(?1)(?2)')(dt, (id <= 1), (id <= 2)))");
    }

    [Fact]
    public void Retention_ShouldRenderConditionMaskNestedInLength()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            N = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.retention(x.Id <= 2, x.Id <= 5))
        }));

        sql.Should().Contain("toInt64(length(retention((id <= 2), (id <= 5))))");
    }

    [Fact]
    public void AnyValueAggregate_ShouldUseClickHouseAny()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sql.any_agg(x.String) }));

        sql.Should().Contain("any(somestring)");
    }

    [Fact]
    public void JsonExtract_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.ClickHouse.json_extract_string(x.String, "s"),
            I = SqlFunctions.ClickHouse.json_extract_int(x.String, "n"),
            F = SqlFunctions.ClickHouse.json_extract_float(x.String, "f"),
            B = SqlFunctions.ClickHouse.json_extract_bool(x.String, "b"),
            R = SqlFunctions.ClickHouse.json_extract_raw(x.String, "o"),
            H = SqlFunctions.ClickHouse.json_has(x.String, "s"),
            L = SqlFunctions.ClickHouse.json_length(x.String, "arr"),
            T = SqlFunctions.ClickHouse.json_type(x.String, "s")
        }));

        sql.Should().Contain("JSONExtractString(somestring, 's')");
        sql.Should().Contain("JSONExtractInt(somestring, 'n')");
        sql.Should().Contain("JSONExtractFloat(somestring, 'f')");
        sql.Should().Contain("JSONExtractBool(somestring, 'b')");
        sql.Should().Contain("JSONExtractRaw(somestring, 'o')");
        sql.Should().Contain("JSONHas(somestring, 's')");
        sql.Should().Contain("toInt64(JSONLength(somestring, 'arr'))");
        sql.Should().Contain("JSONType(somestring, 's')");
    }

    [Fact]
    public void JsonArrayExtractFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            K = SqlFunctions.ClickHouse.json_extract_keys(x.String),
            KP = SqlFunctions.ClickHouse.json_extract_keys(x.String, "o"),
            A = SqlFunctions.ClickHouse.json_extract_array_raw(x.String),
            AP = SqlFunctions.ClickHouse.json_extract_array_raw(x.String, "o"),
            V = SqlFunctions.ClickHouse.json_extract_keys_and_values<int>(x.String),
            VP = SqlFunctions.ClickHouse.json_extract_keys_and_values<long>(x.String, "o")
        }));

        sql.Should().Contain("JSONExtractKeys(somestring)");
        sql.Should().Contain("JSONExtractKeys(somestring, 'o')");
        sql.Should().Contain("JSONExtractArrayRaw(somestring)");
        sql.Should().Contain("JSONExtractArrayRaw(somestring, 'o')");
        sql.Should().Contain("JSONExtractKeysAndValues(somestring, 'Int32')");
        sql.Should().Contain("JSONExtractKeysAndValues(somestring, 'o', 'Int64')");
    }

    [Fact]
    public void JsonExtractKeysAndValues_WithNullableType_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.json_extract_keys_and_values<int?>(x.String) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*non-nullable*");
    }

    [Fact]
    public void JsonPathFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.json_value(x.String, "$.a"),
            Q = SqlFunctions.ClickHouse.json_query(x.String, "$.b"),
            E = SqlFunctions.ClickHouse.json_exists(x.String, "$.c")
        }));

        sql.Should().Contain("JSON_VALUE(somestring, '$.a')");
        sql.Should().Contain("JSON_QUERY(somestring, '$.b')");
        sql.Should().Contain("JSON_EXISTS(somestring, '$.c')");
    }

    [Fact]
    public void NativeJsonFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Paths = SqlFunctions.ClickHouse.json_all_paths(x.String),
            PathsWithTypes = SqlFunctions.ClickHouse.json_all_paths_with_types(x.String),
            Text = SqlFunctions.ClickHouse.to_json_string(x.String)
        }));

        sql.Should().Contain("JSONAllPaths(somestring)");
        sql.Should().Contain("JSONAllPathsWithTypes(somestring)");
        sql.Should().Contain("toJSONString(somestring)");
    }

    [Fact]
    public void VisitParamExtract_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.ClickHouse.visit_param_extract_string(x.String, "s"),
            I = SqlFunctions.ClickHouse.visit_param_extract_int(x.String, "n"),
            F = SqlFunctions.ClickHouse.visit_param_extract_float(x.String, "f"),
            B = SqlFunctions.ClickHouse.visit_param_extract_bool(x.String, "b"),
            R = SqlFunctions.ClickHouse.visit_param_extract_raw(x.String, "o")
        }));

        sql.Should().Contain("visitParamExtractString(somestring, 's')");
        sql.Should().Contain("visitParamExtractInt(somestring, 'n')");
        sql.Should().Contain("visitParamExtractFloat(somestring, 'f')");
        sql.Should().Contain("visitParamExtractBool(somestring, 'b')");
        sql.Should().Contain("visitParamExtractRaw(somestring, 'o')");
    }

    [Fact]
    public void DictFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.dict_get<string, long>("dict", "attr", x.Id),
            D = SqlFunctions.ClickHouse.dict_get_or_default<string, long>("dict", "attr", x.Id, "n/a"),
            H = SqlFunctions.ClickHouse.dict_has<long>("dict", x.Id)
        }));

        sql.Should().Contain("dictGet('dict', 'attr', id)");
        sql.Should().Contain("dictGetOrDefault('dict', 'attr', id, 'n/a')");
        sql.Should().Contain("dictHas('dict', id)");
    }

    [Fact]
    public void HierarchicalDictFunctions_ShouldUseClickHouseNames()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            H = SqlFunctions.ClickHouse.dict_get_hierarchy<long>("dict", x.Id),
            C = SqlFunctions.ClickHouse.dict_get_children<long>("dict", x.Id),
            I = SqlFunctions.ClickHouse.dict_is_in<long>("dict", x.Id, 3L)
        }));

        sql.Should().Contain("dictGetHierarchy('dict', id)");
        sql.Should().Contain("dictGetChildren('dict', id)");
        sql.Should().Contain("dictIsIn('dict', id, 3)");
    }

    [Fact]
    public void HierarchicalDictFunctions_WithCapturedDict_ShouldRefreshParamsOnCachedPlan()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var dict = "dict";

        QueryCommand<ulong[]> Build() => e.Select(x => SqlFunctions.ClickHouse.dict_get_hierarchy<long>(dict, x.Id));

        _ = ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);
        dict = "dict2";
        var second = (DbPreparedQueryCommand<ulong[]>)ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);

        Normalize(second.DbCommand.CommandText).Should().Contain("dictGetHierarchy(@dict, id)");
        second.DbCommand.Parameters.Count.Should().Be(1);
        second.DbCommand.Parameters["dict"].Value.Should().Be("dict2");
    }

    [Fact]
    public void IfAggregates_ShouldUseIfCombinators()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C = SqlFunctions.Sql.count(() => x.Id > 0L),
            Big = SqlFunctions.Sql.count_big(() => x.Id > 0L),
            S = SqlFunctions.Sql.sum(x.Id, () => x.Id > 0L),
            A = SqlFunctions.Sql.avg(x.Id, () => x.Id > 0L),
            Mi = SqlFunctions.Sql.min(x.Id, () => x.Id > 0L),
            Ma = SqlFunctions.Sql.max(x.Id, () => x.Id > 0L)
        }));

        sql.Should().Contain("toInt32(countIf((id > 0)))");
        sql.Should().Contain("toInt64(countIf((id > 0)))");
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
    public void GroupByWithTotals_ShouldAppendModifier()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupBy(x => new { x.Int })
            .WithTotals()
            .Select(x => new { x.Int }))
            .Should().Contain("group by nullableint with totals");
    }

    [Fact]
    public void GroupByRollupWithTotals_ShouldAppendBothModifiers()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .GroupByRollup(x => new { x.Int })
            .WithTotals()
            .Select(x => new { x.Int }))
            .Should().Contain("group by nullableint with rollup with totals");
    }

    [Fact]
    public void TableFunction_Numbers_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var count = 3L;

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.numbers(count))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("from (select toInt64(number) as number from numbers(@count)) as `t1`");
    }

    [Fact]
    public void TableFunction_NumbersMt_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var count = 3L;

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.numbers_mt(count))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText).Should().Contain("from numbers_mt(@count)");
    }

    [Fact]
    public void TableFunction_Zeros_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var count = 3L;

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.zeros(count))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText).Should().Contain("from zeros(@count)");
    }

    [Fact]
    public void TableFunction_ZerosMt_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var count = 3L;

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.zeros_mt(count))
            .Select(r => new { r.Value }));

        Normalize(command.DbCommand.CommandText).Should().Contain("from zeros_mt(@count)");
    }

    [Fact]
    public void TableFunction_GenerateRandom_ShouldEmitWrappedCall()
    {
        using var ctx = ClickHouseTestContext.Create();

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.generate_random())
            .Page(3, 0)
            .Select(r => new { r.Id, r.Value, r.Name }));

        Normalize(command.DbCommand.CommandText).Should().Contain(
            "from (select toInt64(id) as id, value, name from generateRandom('id UInt64, value Float64, name String')) as `t1`");
    }

    [Fact]
    public void TableFunction_GenerateRandomWithSeed_ShouldPassSeed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var seed = 42L;

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.generate_random(seed))
            .Page(3, 0)
            .Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain(
            "from generateRandom('id UInt64, value Float64, name String', @seed)");
    }

    [Fact]
    public void TableFunction_Url_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var location = "http://127.0.0.1/data.csv";
        var format = "CSV";
        var structure = "id UInt64, name String";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.url<IServerTableRow>(location, format, structure))
            .Select(r => new { r.Id, r.Name }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("from url(@location, @format, @structure) as `t1`");
    }

    [Fact]
    public void TableFunction_S3_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var location = "s3://bucket/data.csv";
        var format = "CSV";
        var structure = "id UInt64, name String";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.s3<IServerTableRow>(location, format, structure))
            .Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("from s3(@location, @format, @structure) as `t1`");
    }

    [Fact]
    public void TableFunction_File_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var path = "data.csv";
        var format = "CSV";
        var structure = "id UInt64, name String";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.file<IServerTableRow>(path, format, structure))
            .Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("from file(@path, @format, @structure) as `t1`");
    }

    [Fact]
    public void TableFunction_Remote_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var addresses = "127.0.0.1:9000";
        var database = "default";
        var table = "hits";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.remote<IServerTableRow>(addresses, database, table))
            .Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("from remote(@addresses, @database, @table) as `t1`");
    }

    [Fact]
    public void TableFunction_RemoteSecure_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var addresses = "remote.example.com:9440";
        var database = "default";
        var table = "hits";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.remote_secure<IServerTableRow>(addresses, database, table))
            .Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("from remoteSecure(@addresses, @database, @table) as `t1`");
    }

    [Fact]
    public void TableFunction_Cluster_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var cluster = "my_cluster";
        var database = "default";
        var table = "hits";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.cluster<IServerTableRow>(cluster, database, table))
            .Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("from cluster(@cluster, @database, @table) as `t1`");
    }

    [Fact]
    public void TableFunction_ClusterAllReplicas_ShouldEmitCall()
    {
        using var ctx = ClickHouseTestContext.Create();
        var cluster = "my_cluster";
        var database = "default";
        var table = "hits";

        var command = Prepare(ctx, ctx
            .FromTableFunction(() => SqlFunctions.ClickHouse.cluster_all_replicas<IServerTableRow>(cluster, database, table))
            .Select(r => new { r.Id }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("from clusterAllReplicas(@cluster, @database, @table) as `t1`");
    }

    [Fact]
    public void GlobalIn_Subquery_ShouldRenderGlobalIn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.global_in(
                x.Id,
                ctx.From<ISimpleEntity>().Where(y => y.Id > 1).Select(y => y.Id)))
            .Select(x => new { x.Id }));

        sql.Should().Contain("global in (");
        sql.Should().NotContain("not global in");
    }

    [Fact]
    public void GlobalIn_Values_ShouldRenderGlobalIn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();
        var values = new List<int> { 1, 2 };

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.global_in(x.Id, values))
            .Select(x => new { x.Id }));

        sql.Should().Contain("global in (");
    }

    [Fact]
    public void Final_ShouldAppendModifier()
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlOf(ctx, ctx.From<IComplexEntity>().Final().Select(x => new { x.Id }))
            .Should().Contain("from complex_entity final");
    }

    [Fact]
    public void Sample_ShouldAppendRatioAndOffset()
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlOf(ctx, ctx.From<IComplexEntity>(o => o.Sample(0.1, 0.5)).Select(x => new { x.Id }))
            .Should().Contain("from complex_entity sample 0.1 offset 0.5");
    }

    [Fact]
    public void KeywordCase_Upper_ShouldUppercaseDialectClauses()
    {
        using var ctx = ClickHouseTestContext.CreateUppercase();
        var e = ctx.From<IComplexEntity>();
        var a = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Final().Select(x => new { x.Id }))
            .Should().Contain("FROM complex_entity FINAL");

        SqlOf(ctx, ctx.From<IComplexEntity>(o => o.Sample(0.1, 0.5)).Select(x => new { x.Id }))
            .Should().Contain("FROM complex_entity SAMPLE 0.1 OFFSET 0.5");

        SqlOf(ctx, e.Settings(("max_threads", "2")).Select(x => new { x.Id }))
            .Should().Contain("SETTINGS max_threads = 2");

        SqlOf(ctx, e.LimitBy(2, x => x.Int).Select(x => new { x.Int }))
            .Should().Contain("LIMIT 2 BY nullableint");

        SqlOf(ctx, e
            .GroupByRollup(x => new { x.Int, x.Boolean })
            .WithTotals()
            .Select(x => new { x.Int, x.Boolean }))
            .Should().Contain("GROUP BY nullableint, b WITH ROLLUP WITH TOTALS");

        SqlOf(ctx, a.ArrayJoin(x => x.Tags).Select(x => new { x.Id }))
            .Should().Contain("ARRAY JOIN tags");
    }

    [Fact]
    public void PreWhere_ShouldEmitBeforeWhere()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .PreWhere(x => x.Id > 1L)
            .Where(x => x.Boolean == true)
            .Select(x => new { x.Id }));

        var preWhereAt = sql.IndexOf(" prewhere ", StringComparison.Ordinal);
        var whereAt = sql.IndexOf(" where ", StringComparison.Ordinal);

        preWhereAt.Should().BeGreaterThan(-1);
        whereAt.Should().BeGreaterThan(preWhereAt);
    }

    [Fact]
    public void PreWhere_Repeated_ShouldCombineWithAnd()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .PreWhere(x => x.Id > 1L)
            .PreWhere(x => x.Boolean == true)
            .Select(x => new { x.Id }));

        sql.Should().Contain(" prewhere ");
        sql.Should().Contain(" and ");
    }

    [Fact]
    public void Settings_ShouldAppendTrailingClause()
    {
        using var ctx = ClickHouseTestContext.Create();

        SqlOf(ctx, ctx.From<IComplexEntity>().Settings(("max_threads", "2")).Select(x => new { x.Id }))
            .Should().Contain(" settings max_threads = 2");
    }

    [Fact]
    public void LimitBy_ShouldAppendClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e
            .LimitBy(2, x => x.Int)
            .Select(x => new { x.Int }))
            .Should().Contain("limit 2 by nullableint");
    }

    [Fact]
    public void LimitBy_WithOffsetAndOrderBy_ShouldOrderThenLimitBy()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .OrderBy(x => x.Id)
            .LimitBy(3, 1, x => new { x.Int, x.Boolean })
            .Select(x => new { x.Id }));

        sql.Should().Contain("order by id");
        sql.Should().Contain("limit 1, 3 by nullableint, b");
    }

    [Fact]
    public void LimitBy_WithComputedKey_ShouldNotAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e
            .LimitBy(2, x => x.Id + 1L)
            .Select(x => new { x.Id }));

        sql.Should().Contain("limit 2 by ");
        sql.Should().NotContain(" as ");
    }

    [Fact]
    public void DateFromParts_ShouldEmitMakeDate()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_from_parts(2023, 1, 31) }))
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
            .Should().Contain("case when (positionUTF8(reverseUTF8(somestring), reverseUTF8('b'))) = 0 then -1 else lengthUTF8(somestring) - (positionUTF8(reverseUTF8(somestring), reverseUTF8('b'))) - (lengthUTF8('b')) + 1 end");
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

    [Fact]
    public void JoinWithStrictness_ShouldRenderClickHouseModifier()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        SqlOf(ctx,
            simple.LeftJoin(complex, (s, c) => s.Id == c.Id)
                .WithStrictness(JoinStrictness.Any)
                .Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" left any join ").And.Contain(" on ");

        SqlOf(ctx,
            simple.Join(complex, (s, c) => s.Id == c.Id)
                .WithStrictness(JoinStrictness.All)
                .Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" all join ");

        SqlOf(ctx,
            simple.LeftJoin(complex, (s, c) => s.Id == c.Id)
                .WithStrictness(JoinStrictness.Asof)
                .Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" left asof join ");
    }

    [Fact]
    public void JoinStrictness_OnCrossJoin_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple.CrossJoin(complex)
            .WithStrictness(JoinStrictness.Any)
            .Select(p => new { p.Item1.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot be applied to a Cross join*");
    }

    [Fact]
    public void WithStrictness_WithoutJoin_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();

        var act = () => simple.WithStrictness(JoinStrictness.Any);

        act.Should().Throw<InvalidOperationException>().WithMessage("*preceding join*");
    }

    [Fact]
    public void WithStrictness_ShouldNotMutateSourceBuilder()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var joined = simple.LeftJoin(complex, (s, c) => s.Id == c.Id);
        var any = joined.WithStrictness(JoinStrictness.Any);

        SqlOf(ctx, joined.Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" left join ").And.NotContain(" left any join ");
        SqlOf(ctx, any.Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" left any join ");
    }

    [Fact]
    public void WithStrictness_ThenJoin_ShouldKeepStrictnessOnFirstJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id)
            .WithStrictness(JoinStrictness.Any)
            .Join(simple, (p, s) => p.Item2.Id == s.Id)
            .Select(p => new { p.Item1.Id }));

        sql.Should().Contain(" left any join ");
    }

    [Fact]
    public void GlobalJoin_ShouldRenderGlobalModifier()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id)
            .Global()
            .Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" global left join ");

        SqlOf(ctx, simple.Join(complex, (s, c) => s.Id == c.Id)
            .Global()
            .WithStrictness(JoinStrictness.Any)
            .Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" global any join ");
    }

    [Fact]
    public void GlobalJoin_ShouldNotMutateSourceBuilder()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var joined = simple.LeftJoin(complex, (s, c) => s.Id == c.Id);
        var global = joined.Global();

        SqlOf(ctx, joined.Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" left join ").And.NotContain(" global ");
        SqlOf(ctx, global.Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" global left join ");
    }

    [Fact]
    public void WithStrictness_ThenGlobal_ShouldComposeModifiers()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id)
            .WithStrictness(JoinStrictness.Any)
            .Global()
            .Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" global left any join ");
    }

    [Fact]
    public void Global_WithoutJoin_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();

        var act = () => simple.Global();

        act.Should().Throw<InvalidOperationException>().WithMessage("*preceding join*");
    }

    [Fact]
    public void SemiJoin_ShouldRenderLeftSemiJoinAndKeepLeftColumns()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.SemiJoin(complex, (s, c) => s.Id == c.Id).Select(s => new { s.Id }));

        sql.Should().Contain(" left semi join ").And.Contain(" on ");
        sql.Should().NotContain("somestring");
    }

    [Fact]
    public void AntiJoin_ShouldRenderLeftAntiJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        SqlOf(ctx, simple.AntiJoin(complex, (s, c) => s.Id == c.Id).Select(s => new { s.Id }))
            .Should().Contain(" left anti join ").And.Contain(" on ");
    }

    [Fact]
    public void PasteJoin_ShouldRenderPasteJoinWithoutOnAndExposeBothSides()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.PasteJoin(complex).Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" paste join ").And.NotContain(" on ");
        sql.Should().Contain("somestring");
    }

    [Fact]
    public void SemiAntiPasteJoin_FromQueryCommand_ShouldRender()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>().Where(c => c.Id > 0);
        var complexQuery = (QueryCommand<IComplexEntity>)complex;

        SqlOf(ctx, simple.SemiJoin(complexQuery, (s, c) => s.Id == (int)c.Id).Select(s => new { s.Id }))
            .Should().Contain(" left semi join ");
        SqlOf(ctx, simple.AntiJoin(complexQuery, (s, c) => s.Id == (int)c.Id).Select(s => new { s.Id }))
            .Should().Contain(" left anti join ");
        SqlOf(ctx, simple.PasteJoin(complexQuery).Select(p => new { p.Item1.Id, p.Item2.String }))
            .Should().Contain(" paste join ").And.Contain("somestring");
    }

    [Fact]
    public void AntiJoin_AfterJoin_ShouldKeepBothJoins()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id)
            .AntiJoin(simple, (p, s2) => p.Item1.Id == s2.Id)
            .Select(p => new { p.Item1.Id, p.Item2.String }));

        sql.Should().Contain(" left join ").And.Contain(" left anti join ");
    }

    [Fact]
    public void PasteJoin_AfterJoin_ShouldExtendProjectionToThree()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id)
            .PasteJoin(simple)
            .Select(p => new { First = p.Item1.Id, p.Item2.String, Third = p.Item3.Id }));

        sql.Should().Contain(" left join ").And.Contain(" paste join ");
    }

    [Fact]
    public void SemiJoin_WithStrictness_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple.SemiJoin(complex, (s, c) => s.Id == c.Id)
            .WithStrictness(JoinStrictness.Any)
            .Select(s => new { s.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot be applied to a Semi join*");
    }

    [Fact]
    public void SemiJoin_ShouldNotMutateSourceBuilder()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var semi = simple.SemiJoin(complex, (s, c) => s.Id == c.Id);

        SqlOf(ctx, simple.Select(s => new { s.Id })).Should().NotContain(" join ");
        SqlOf(ctx, semi.Select(s => new { s.Id })).Should().Contain(" left semi join ");
    }

    [Fact]
    public void SemiAntiPasteJoin_OnNamedTable_ShouldRender()
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();
        var t1 = ctx.From("simple_entity");
        var t2 = ctx.From("complex_entity");

        SqlOf(ctx, t1.SemiJoin(t2, (a, b) => a.GetInt64("id") == b.GetInt64("id")).Select(a => new { Id = a.GetInt32("id") }))
            .Should().Contain(" left semi join ");
        SqlOf(ctx, t1.AntiJoin(t2, (a, b) => a.GetInt64("id") == b.GetInt64("id")).Select(a => new { Id = a.GetInt32("id") }))
            .Should().Contain(" left anti join ");
        SqlOf(ctx, t1.PasteJoin(t2).Select(p => new { A = p.Item1.GetInt32("id"), B = p.Item2.GetInt64("id") }))
            .Should().Contain(" paste join ");

        var e1 = ctx.From<ISimpleEntity>();
        var e2 = ctx.From<IComplexEntity>();
        SqlOf(ctx, t1.SemiJoin(e1, (a, b) => a.GetInt64("id") == b.Id).Select(a => new { Id = a.GetInt32("id") }))
            .Should().Contain(" left semi join ");
        SqlOf(ctx, t1.AntiJoin(e2, (a, b) => a.GetInt64("id") == b.Id).Select(a => new { Id = a.GetInt32("id") }))
            .Should().Contain(" left anti join ");
        SqlOf(ctx, t1.PasteJoin(e2).Select(p => new { A = p.Item1.GetInt32("id"), B = p.Item2.Id }))
            .Should().Contain(" paste join ");
    }

    [Fact]
    public void SemiJoin_AfterWindow_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => simple.Window("w").SemiJoin(complex, (s, c) => s.Id == c.Id);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Named windows*");
    }

    [Fact]
    public void SemiAntiPaste_OnJoinedLhs_ShouldRenderAtEveryArity()
    {
        using var ctx = ClickHouseTestContext.Create();
        var s = ctx.From<ISimpleEntity>();
        var c = ctx.From<IComplexEntity>();

        var a2 = s.Join(c, (p, x) => p.Id == x.Id);
        var a3 = a2.Join(s, (p, x) => p.Item1.Id == x.Id);
        var a4 = a3.Join(s, (p, x) => p.Item1.Id == x.Id);
        var a5 = a4.Join(s, (p, x) => p.Item1.Id == x.Id);
        var a6 = a5.Join(s, (p, x) => p.Item1.Id == x.Id);
        var a7 = a6.Join(s, (p, x) => p.Item1.Id == x.Id);

        SqlOf(ctx, a2.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");
        SqlOf(ctx, a3.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");
        SqlOf(ctx, a4.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");
        SqlOf(ctx, a5.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");
        SqlOf(ctx, a6.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");
        SqlOf(ctx, a7.SemiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left semi join ");

        SqlOf(ctx, a2.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");
        SqlOf(ctx, a3.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");
        SqlOf(ctx, a4.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");
        SqlOf(ctx, a5.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");
        SqlOf(ctx, a6.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");
        SqlOf(ctx, a7.AntiJoin(s, (p, x) => p.Item1.Id == x.Id).Select(p => new { p.Item1.Id })).Should().Contain(" left anti join ");

        SqlOf(ctx, a2.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
        SqlOf(ctx, a3.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
        SqlOf(ctx, a4.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
        SqlOf(ctx, a5.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
        SqlOf(ctx, a6.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
        SqlOf(ctx, a7.PasteJoin(s).Select(p => new { p.Item1.Id })).Should().Contain(" paste join ");
    }

    [Fact]
    public void Global_ThenJoin_ShouldKeepGlobalOnFirstJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id)
            .Global()
            .Join(simple, (p, s) => p.Item2.Id == s.Id)
            .Select(p => new { p.Item1.Id }));

        sql.Should().Contain(" global left join ");
    }

    [Fact]
    public void ArrayLength_ShouldRenderLength()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => new { N = SqlFunctions.ClickHouse.length(x.Tags) }))
            .Should().Contain("toInt64(length(tags))");
    }

    [Fact]
    public void ArrayHas_ShouldRenderHas()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.ClickHouse.has(x.Tags, "a")).Select(x => new { x.Id }))
            .Should().Contain("has(tags, 'a')");
    }

    [Fact]
    public void ArrayIndexOf_ShouldRenderIndexOf()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => new { P = SqlFunctions.ClickHouse.index_of(x.Nums, 3L) }))
            .Should().Contain("toInt64(indexOf(nums, 3))");
    }

    [Fact]
    public void ArrayHasAnyAndHasAll_ShouldRender()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.ClickHouse.has_any(x.Nums, new long[] { 1, 2 }),
            B = SqlFunctions.ClickHouse.has_all(x.Nums, new long[] { 1, 2 })
        }));

        sql.Should().Contain("hasAny(nums, @p0)");
        sql.Should().Contain("hasAll(nums, @p1)");
    }

    [Fact]
    public void ArrayRelationPredicates_ShouldRender()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.ClickHouse.starts_with(x.Nums, new long[] { 3, 1 }),
            E = SqlFunctions.ClickHouse.ends_with(x.Nums, new long[] { 1, 2 }),
            C = SqlFunctions.ClickHouse.has_substr(x.Nums, new long[] { 1, 2 })
        }));

        sql.Should().Contain("startsWith(nums, @p0)");
        sql.Should().Contain("endsWith(nums, @p1)");
        sql.Should().Contain("hasSubstr(nums, @p2)");
    }

    [Fact]
    public void TupleElementAccess_ShouldRenderTupleElement()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ITupleEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { A = x.Pair.Item1, B = x.Pair.Item2 }));

        sql.Should().Contain("tupleElement(pair, 1)");
        sql.Should().Contain("tupleElement(pair, 2)");
    }

    [Fact]
    public void TupleCreate_ShouldRenderTuple()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => Tuple.Create(x.Id, x.String)))
            .Should().Contain("tuple(id, somestring)");
    }

    [Fact]
    public void TupleCreate_WithCapturedValue_ShouldRefreshParamsOnCachedPlan()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ITupleEntity>();
        var extra = 5;

        QueryCommand<Tuple<int, int>> Build() => e.Select(x => Tuple.Create(x.Id, extra));

        _ = ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);
        var second = (DbPreparedQueryCommand<Tuple<int, int>>)ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);

        Normalize(second.DbCommand.CommandText).Should().Contain("tuple(id, @extra)");
    }

    [Fact]
    public void TupleElementAccess_WithCapturedFilter_ShouldRefreshParamsOnCachedPlan()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ITupleEntity>();
        var min = 0;

        QueryCommand<int> Build() => e.Where(x => x.Id > min).Select(x => x.Pair.Item1);

        _ = ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);
        var second = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);

        Normalize(second.DbCommand.CommandText).Should().Contain("tupleElement(pair, 1)");
    }

    [Fact]
    public void TupleElementAccess_OnCapturedTuple_ShouldNotTranslateToSql()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ITupleEntity>();
        var local = Tuple.Create(1, "a");

        SqlOf(ctx, e.Select(x => new { V = local.Item1 }))
            .Should().NotContain("tupleElement");
    }

    [Fact]
    public void TupleElementAccess_OnNewTuple_ShouldFoldToArgument()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ITupleEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { V = new Tuple<int, int>(x.Id, x.Id).Item1 }));

        sql.Should().Contain("id as").And.NotContain("tupleElement");
    }

    [Fact]
    public void ArrayStringConcat_ShouldRenderArrayStringConcat()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => new { S = SqlFunctions.ClickHouse.array_string_concat(x.Tags, ",") }))
            .Should().Contain("arrayStringConcat(tags, ',')");
    }

    [Fact]
    public void ArrayStringConcat_WithDefaultDelimiter_ShouldOmitArgument()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => new { S = SqlFunctions.ClickHouse.array_string_concat(x.Tags) }))
            .Should().Contain("arrayStringConcat(tags)").And.NotContain("null");
    }

    [Fact]
    public void SplitByChar_ShouldRenderSplitByChar()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { N = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.split_by_char(",", x.String)) }))
            .Should().Contain("toInt64(length(splitByChar(',', somestring)))");
    }

    [Fact]
    public void Split_CharSeparator_ShouldUseSplitByChar()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { N = SqlFunctions.ClickHouse.length(x.String!.Split(',')) }))
            .Should().Contain("toInt64(length(splitByChar(',', somestring)))");
    }

    [Fact]
    public void Split_StringSeparator_ShouldUseSplitByChar()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { N = SqlFunctions.ClickHouse.length(x.String!.Split(",")) }))
            .Should().Contain("toInt64(length(splitByChar(',', somestring)))");
    }

    [Fact]
    public void Split_CharArraySeparator_ShouldUseSplitByChar()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { N = SqlFunctions.ClickHouse.length(x.String!.Split(new[] { '|' })) }))
            .Should().Contain("toInt64(length(splitByChar('|', somestring)))");
    }

    [Fact]
    public void Split_MultiCharSeparator_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { N = SqlFunctions.ClickHouse.length(x.String!.Split("::")) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*one-character separator*");
    }

    [Fact]
    public void Split_RemoveEmptyEntries_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { N = SqlFunctions.ClickHouse.length(x.String!.Split(',', StringSplitOptions.RemoveEmptyEntries)) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*StringSplitOptions*");
    }

    [Fact]
    public void ArraySortReverseDistinct_ShouldRender()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            S = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_sort(x.Nums)),
            R = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_reverse(x.Nums)),
            D = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_distinct(x.Nums))
        }));

        sql.Should().Contain("toInt64(length(arraySort(nums)))");
        sql.Should().Contain("toInt64(length(arrayReverse(nums)))");
        sql.Should().Contain("toInt64(length(arrayDistinct(nums)))");
    }

    [Fact]
    public void ArrayRange_ShouldRenderRange()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.range(5)),
            B = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.range(1, 5)),
            C = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.range(1, 5, 2))
        }));

        sql.Should().Contain("toInt64(length(range(5)))");
        sql.Should().Contain("toInt64(length(range(1, 5)))");
        sql.Should().Contain("toInt64(length(range(1, 5, 2)))");
    }

    [Fact]
    public void ArrayEnumerate_ShouldRenderArrayEnumerate()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => new { N = SqlFunctions.ClickHouse.length(SqlFunctions.ClickHouse.array_enumerate(x.Nums)) }))
            .Should().Contain("toInt64(length(arrayEnumerate(nums)))");
    }

    [Fact]
    public void ArrayCumSum_ShouldRenderArrayCumSum()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => new { S = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_cum_sum(x.Nums), ",") }))
            .Should().Contain("arrayStringConcat(arrayCumSum(nums), ',')");
    }

    [Fact]
    public void ArraySlice_ShouldRenderArraySlice()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_slice(x.Nums, 1), ","),
            B = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_slice(x.Nums, 1, 2), ",")
        }));

        sql.Should().Contain("arrayStringConcat(arraySlice(nums, 1), ',')");
        sql.Should().Contain("arrayStringConcat(arraySlice(nums, 1, 2), ',')");
    }

    [Fact]
    public void ArrayPushBack_ShouldRenderArrayPushBack()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => new { S = SqlFunctions.ClickHouse.array_string_concat(SqlFunctions.ClickHouse.array_push_back(x.Nums, 4L), ",") }))
            .Should().Contain("arrayStringConcat(arrayPushBack(nums, 4), ',')");
    }

    [Fact]
    public void ArrayJoin_ShouldRenderArrayJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.Select(x => new { x.Id, Tag = SqlFunctions.ClickHouse.array_join(x.Tags) }))
            .Should().Contain("arrayJoin(tags)");
    }

    [Fact]
    public void ArrayFunction_WithCapturedArray_ShouldBindSingleParameter()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();
        var wanted = new[] { "a", "b" };

        var command = Prepare(ctx, e.Where(x => SqlFunctions.ClickHouse.has_any(x.Tags, wanted)).Select(x => new { x.Id }));

        Normalize(command.DbCommand.CommandText).Should().Contain("hasAny(tags, @p0)");
        command.DbCommandParams.Cast<DbParameter>().Should().ContainSingle();
    }

    [Fact]
    public void ArrayJoinClause_ShouldRenderArrayJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.ArrayJoin(x => x.Tags).Select(x => new { x.Id }))
            .Should().Contain(" array join tags");
    }

    [Fact]
    public void LeftArrayJoinClause_ShouldRenderLeftArrayJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.LeftArrayJoin(x => x.Tags).Select(x => new { x.Id }))
            .Should().Contain(" left array join tags");
    }

    [Fact]
    public void ArrayJoinClause_WithMultipleExpressions_ShouldRenderCommaSeparated()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        SqlOf(ctx, e.ArrayJoin(x => x.Tags).ArrayJoin(x => x.Nums).Select(x => new { x.Id }))
            .Should().Contain(" array join tags, nums");
    }

    [Fact]
    public void ArrayJoinClause_OnJoinedQuery_ShouldRender()
    {
        using var ctx = ClickHouseTestContext.Create();
        var arrays = ctx.From<IArrayEntity>();
        var complex = ctx.From<IComplexEntity>();

        SqlOf(ctx, arrays.Join(complex, (a, c) => a.Id == c.Id)
            .ArrayJoin(p => p.Item1.Tags)
            .Select(p => new { p.Item1.Id }))
            .Should().Contain("array join t1.tags");
    }

    [Fact]
    public void ArrayJoinClause_MixingKinds_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => e.ArrayJoin(x => x.Tags).LeftArrayJoin(x => x.Nums);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot mix*");
    }

    [Fact]
    public void ArrayJoinClause_WithNonSequence_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => e.ArrayJoin(x => x.Id);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ArrayJoinElement_ShouldRenderElementAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.ArrayJoinElement(x => x.Tags).Select(p => new { p.Item1.Id, Tag = p.Element }));

        sql.Should().Contain("array join tags as __nextorm_aj_element");
        sql.Should().Contain("__nextorm_aj_element as `Tag`");
        sql.Should().Contain("id");
    }

    [Fact]
    public void LeftArrayJoinElement_ShouldRenderLeftElementAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.LeftArrayJoinElement(x => x.Nums).Select(p => new { p.Item1.Id, N = p.Element }));

        sql.Should().Contain("left array join nums as __nextorm_aj_element");
    }

    [Fact]
    public void ArrayJoinElement_WhereOnElement_ShouldReferenceAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.ArrayJoinElement(x => x.Tags).Where(p => p.Element == "a").Select(p => new { p.Item1.Id }));

        sql.Should().Contain("array join tags as __nextorm_aj_element");
        sql.Should().Contain("where __nextorm_aj_element = 'a'");
    }

    [Fact]
    public void ArrayJoinElement_OrderByElement_ShouldReferenceAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.ArrayJoinElement(x => x.Tags).OrderBy(p => (object?)p.Element).Select(p => new { p.Item1.Id }));

        sql.Should().Contain("order by __nextorm_aj_element");
    }

    [Fact]
    public void ArrayJoinElement_AfterCondition_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => e.Where(x => x.Id > 0).ArrayJoinElement(x => x.Tags);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ArrayJoinElement_SecondCall_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var act = () => e.ArrayJoinElement(x => x.Tags).ArrayJoinElement(p => p.Item1.Tags);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DistinctOn_ShouldThrowBecauseClickHouseHasNoDistinctOn()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.DistinctOn(x => x.Id).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*DISTINCT ON*");
    }

    [Fact]
    public void TableSample_ShouldThrowBecauseClickHouseUsesSample()
    {
        using var ctx = ClickHouseTestContext.Create();
        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>(o => o.TableSample(10)).Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*TABLESAMPLE*");
    }

    [Fact]
    public void TextSearch_ShouldThrowBecauseClickHouseHasNoTsvector()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { Q = SqlFunctions.Postgres.to_tsquery("a") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*text-search*");
    }

    [Fact]
    public void ForSystemTime_ShouldThrowBecauseClickHouseHasNoTemporalTables()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.ForSystemTime(TemporalClause.All()).Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR SYSTEM_TIME*");
    }

    [Fact]
    public void XmlMethods_ShouldThrowBecauseOnlySqlServerHasThem()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.xml_query(x.String, "/root") }));

        act.Should().Throw<NotSupportedException>().WithMessage("*XML data-type methods*");
    }

    [Fact]
    public void XmlNodes_ShouldThrowBecauseClickHouseHasNoApply()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<IComplexEntity>()
            .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.String, "/root/item"))
            .Select(p => new { p.Item2.Value }));

        act.Should().Throw<NotSupportedException>().WithMessage("*CrossApply*");
    }

    [Fact]
    public void UnsignedNumericCast_ShouldEmitCastInsteadOfDropping()
    {
        // A user can still cast a UInt64 column to a signed type. The conversion source is a ulong,
        // which TypeFacts must accept or the cast is dropped.
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IUnsignedCastEntity>();

        SqlOf(ctx, e.Select(x => new { V = (long)x.Big })).Should().Contain("cast(Big as Int64)");
    }

    [Fact]
    public void UnsignedNumericProjection_ShouldRenderNativeColumnWithoutCast()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IUnsignedCastEntity>();

        SqlOf(ctx, e.Select(x => new { x.Big })).Should().Be("select Big from unsigned_cast");
    }

    [Fact]
    public void DerivedSourceThenJoin_ShouldRenderDerivedTable()
    {
        using var ctx = ClickHouseTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Where(c => c.Id > 0)
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id, p.Item1.String }));

        sql.Should().Be("select t1.id, t2.id as `SId`, t1.`String` from (select id, somestring as `String` from complex_entity\n where (id > 0)) as `t1` join simple_entity as `t2` on t1.id = cast(t2.id as Int64)");
    }

    [Fact]
    public void DerivedSourceWithJoinThenJoin_ShouldResolveTheOuterAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var derived = ctx.From<ISimpleEntity>()
            .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
            .Select(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Join(ctx.From<IComplexEntity>(), (d, c2) => d.OrderId == c2.Id)
            .Select(p => new { p.Item1.OrderId, p.Item1.CustomerName, Third = p.Item2.Id }));

        sql.Should().Be("select t3.`OrderId`, t3.`CustomerName`, t4.id as `Third` from (select t1.id as `OrderId`, t2.somestring as `CustomerName` from simple_entity as `t1` join complex_entity as `t2` on cast(t1.id as Int64) = t2.id) as `t3` join complex_entity as `t4` on cast(t3.`OrderId` as Int64) = t4.id");
    }

    [Fact]
    public void DerivedSourceWhereThenJoin_ShouldPushTheFilterOntoTheProjection()
    {
        using var ctx = ClickHouseTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Select(c => new { c.Id, c.String });

        var sql = SqlOf(ctx, ctx.From(derived)
            .Where(d => d.Id > 5)
            .Join(ctx.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
            .Select(p => new { p.Item1.Id, SId = p.Item2.Id }));

        sql.Should().Be("select t1.id, t2.id as `SId` from (select id, somestring as `String` from complex_entity) as `t1` join simple_entity as `t2` on t1.id = cast(t2.id as Int64)\n where (t1.id > 5)");
    }

    [Fact]
    public void FromSql_ShouldRenderDerivedTableWithNamedParameters()
    {
        using var ctx = ClickHouseTestContext.Create();
        var min = 1;

        var command = Prepare(ctx, ctx
            .FromSql("select id from complex_entity where id > @min", new { min })
            .Select(t => new { Id = t["id"].AsInt }));

        var sql = Normalize(command.DbCommand.CommandText);
        sql.Should().Contain("from (select id from complex_entity where id > @min) as `t1`");
        sql.Should().Contain("t1.id");
    }

    [Fact]
    public void FromSql_AsJoinedSource_ShouldRenderDerivedTableAndResolveColumns()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx
            .From<ISimpleEntity>()
            .Join(ctx.FromSql("select id from complex_entity"), (s, r) => s.Id == r["id"].AsInt)
            .Select(p => new { p.Item1.Id, R = p.Item2["id"].AsInt }));

        sql.Should().Contain("join (select id from complex_entity) as `t2`");
        sql.Should().Contain("on t1.id = t2.id");
    }

    [Fact]
    public void ColumnByName_ShouldRenderColumnIdentifierAndRenameAlias()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .Select(x => new { Region = SqlFunctions.Column<ulong>(x, "RegionID") }));

        sql.Should().Be("select RegionID as `Region` from simple_entity");
    }

    [Fact]
    public void ColumnByName_OnJoinProjection_ShouldQualifyWithTableAlias()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
            .Where(p => SqlFunctions.Column<string>(p.Item2, "somestring") == "x")
            .Select(p => new { p.Item1.Id, S = SqlFunctions.Column<string>(p.Item2, "somestring") }));

        sql.Should().Contain("where t2.somestring = 'x'");
        sql.Should().Contain("t2.somestring as `S`");
    }

    [Fact]
    public void ColumnByName_QuotedIdentifiers_ShouldBacktick()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .Select(x => new { R = SqlFunctions.Column<ulong>(x, "RegionID") }));

        sql.Should().Be("select `RegionID` as `R` from `simple_entity`");
    }

    [Fact]
    public void ColumnByName_OnNonSource_ShouldThrow()
    {
        using var ctx = ClickHouseTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>()
            .Select(x => new { R = SqlFunctions.Column<int>("not-a-source", "id") }));

        act.Should().Throw<BuildSqlCommandException>();
    }

}

[SqlTable("unsigned_cast")]
public interface IUnsignedCastEntity
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("Big")]
    ulong Big { get; set; }


}

public interface IServerTableRow
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    long Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("name")]
    string? Name { get; set; }
}
