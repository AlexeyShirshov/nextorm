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
    }

    [Fact]
    public void DateTimePart_ShouldUseToDayOfYear()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { DOY = x.Datetime!.Value.DayOfYear }))
            .Should().Contain("toDayOfYear(dt)");
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

        SqlOf(ctx, e.Select(x => SqlFunctions.Sql.string_agg(x.String, ",")))
            .Should().Contain("arrayStringConcat(groupArray(somestring), ',')");
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
            F = SqlFunctions.ClickHouse.count_if(() => x.Id > 0L)
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
    public void IfAggregates_ShouldUseIfCombinators()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            C = SqlFunctions.ClickHouse.count_if(() => x.Id > 0L),
            S = SqlFunctions.ClickHouse.sum_if(x.Id, () => x.Id > 0L),
            A = SqlFunctions.ClickHouse.avg_if(x.Id, () => x.Id > 0L),
            Mi = SqlFunctions.ClickHouse.min_if(x.Id, () => x.Id > 0L),
            Ma = SqlFunctions.ClickHouse.max_if(x.Id, () => x.Id > 0L)
        }));

        sql.Should().Contain("toInt32(countIf((id > 0)))");
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

        SqlOf(ctx, ctx.From<IComplexEntity>().Sample(0.1, 0.5).Select(x => new { x.Id }))
            .Should().Contain("from complex_entity sample 0.1 offset 0.5");
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
        var e = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, e.TableSample(10).Select(x => x.Id));

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
}
