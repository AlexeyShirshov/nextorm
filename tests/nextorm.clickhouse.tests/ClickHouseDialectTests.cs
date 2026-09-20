using System.Text;
using FluentAssertions;
using NextORM.Core;
using NextORM.ClickHouse;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// Direct assertions on the ClickHouse dialect hooks that the query-level tests do not exercise.
/// </summary>
public class ClickHouseDialectTests
{
    private static readonly ISqlDialect Dialect = ClickHouseDialect.Instance;

    [Fact]
    public void MakeConcat_ShouldUseConcatFunction()
    {
        Dialect.MakeConcat(["a", "b"]).Should().Be("concat(a, b)");
        Dialect.MakeConcat(["a", "b", "c"]).Should().Be("concat(a, b, c)");
    }

    [Fact]
    public void IdentifierAndScalarFunctionHooks_ShouldUseClickHouseForms()
    {
        Dialect.Escape("t1").Should().Be("`t1`");
        Dialect.MakeColumnReference("t1").Should().Be("`t1`");
        Dialect.MakeParam("p0").Should().Be("@p0");
        Dialect.MakeCoalesce("a", "b").Should().Be("coalesce(a, b)");
        Dialect.MakeBool(true).Should().Be("true");
        Dialect.MakeBool(false).Should().Be("false");
        Dialect.MakeStringLength("x").Should().Be("lengthUTF8(x)");
        Dialect.MakeTrim("x", StringTrimKind.Both).Should().Be("trimBoth(x)");
        Dialect.MakeTrim("x", StringTrimKind.Start).Should().Be("trimLeft(x)");
        Dialect.MakeTrim("x", StringTrimKind.End).Should().Be("trimRight(x)");
        Dialect.MakeNow(true).Should().Be("now('UTC')");
        Dialect.MakeNow(false).Should().Be("now()");
    }

    [Fact]
    public void ArrayCapabilities_ShouldBeEnabled()
    {
        Dialect.SupportsArrayFunctions.Should().BeTrue();
        Dialect.SupportsArrayJoin.Should().BeTrue();
    }

    [Fact]
    public void JoinStrictness_ShouldRenderClickHouseModifiers()
    {
        Dialect.SupportsJoinStrictness.Should().BeTrue();
        Dialect.SupportsGlobalJoin.Should().BeTrue();

        Dialect.MakeJoinKeyword(JoinType.Inner, JoinStrictness.Default, false).Should().Be(" join ");
        Dialect.MakeJoinKeyword(JoinType.Left, JoinStrictness.Default, false).Should().Be(" left join ");
        Dialect.MakeJoinKeyword(JoinType.Right, JoinStrictness.Default, false).Should().Be(" right join ");
        Dialect.MakeJoinKeyword(JoinType.Full, JoinStrictness.Default, false).Should().Be(" full join ");
        Dialect.MakeJoinKeyword(JoinType.Cross, JoinStrictness.Default, false).Should().Be(" cross join ");
        Dialect.MakeJoinKeyword(JoinType.Left, JoinStrictness.Any, false).Should().Be(" left any join ");
        Dialect.MakeJoinKeyword(JoinType.Right, JoinStrictness.All, false).Should().Be(" right all join ");
        Dialect.MakeJoinKeyword(JoinType.Inner, JoinStrictness.Any, false).Should().Be(" any join ");
        Dialect.MakeJoinKeyword(JoinType.Inner, JoinStrictness.All, false).Should().Be(" all join ");
        Dialect.MakeJoinKeyword(JoinType.Inner, JoinStrictness.Asof, false).Should().Be(" asof join ");
        Dialect.MakeJoinKeyword(JoinType.Left, JoinStrictness.Asof, false).Should().Be(" left asof join ");
        Dialect.MakeJoinKeyword(JoinType.Inner, JoinStrictness.Default, true).Should().Be(" global join ");
        Dialect.MakeJoinKeyword(JoinType.Left, JoinStrictness.Any, true).Should().Be(" global left any join ");
    }

    [Theory]
    [InlineData("stdev", "stddevSamp")]
    [InlineData("stdevp", "stddevPop")]
    [InlineData("var", "varSamp")]
    [InlineData("varp", "varPop")]
    [InlineData("covar_pop", "covarPop")]
    [InlineData("covar_samp", "covarSamp")]
    [InlineData("bit_and", "groupBitAnd")]
    [InlineData("bit_or", "groupBitOr")]
    [InlineData("bit_xor", "groupBitXor")]
    [InlineData("arg_min", "argMin")]
    [InlineData("arg_max", "argMax")]
    [InlineData("uniq", "uniq")]
    [InlineData("uniq_exact", "uniqExact")]
    [InlineData("uniq_combined", "uniqCombined")]
    [InlineData("uniq_hll12", "uniqHLL12")]
    [InlineData("quantile_exact", "quantileExact")]
    [InlineData("quantile_timing", "quantileTiming")]
    [InlineData("any_agg", "any")]
    [InlineData("any_last", "anyLast")]
    [InlineData("count_if", "countIf")]
    [InlineData("sum_if", "sumIf")]
    [InlineData("sum", "sum")]
    public void MakeAggregate_ShouldMapProviderNames(string name, string expected)
    {
        Dialect.MakeAggregate(name).Should().Be(expected);
    }

    [Fact]
    public void MakeUniqAggregate_ShouldCastToInt64()
    {
        Dialect.MakeUniqAggregate("uniq_exact", "x").Should().Be("toInt64(uniqExact(x))");
    }

    [Fact]
    public void WrapCount_ShouldCastToClrInteger()
    {
        Dialect.WrapsCountResult.Should().BeTrue();
        Dialect.WrapCount("count(*)", big: false).Should().Be("toInt32(count(*))");
        Dialect.WrapCount("count(*)", big: true).Should().Be("toInt64(count(*))");
        Dialect.WrapCount("countIf(x)", big: false).Should().Be("toInt32(countIf(x))");
    }

    [Fact]
    public void MakeJsonExtract_ShouldMapNamesAndCastLength()
    {
        Dialect.MakeJsonExtract("json_extract_string", ["json", "'s'"]).Should().Be("JSONExtractString(json, 's')");
        Dialect.MakeJsonExtract("json_has", ["json", "'s'"]).Should().Be("JSONHas(json, 's')");
        Dialect.MakeJsonExtract("json_length", ["json", "'a'"]).Should().Be("toInt64(JSONLength(json, 'a'))");
    }

    [Fact]
    public void MakeJsonExtract_ShouldMapJsonPathNames()
    {
        Dialect.SupportsJsonExtract.Should().BeTrue();
        Dialect.MakeJsonExtract("json_value", ["json", "'$.a'"]).Should().Be("JSON_VALUE(json, '$.a')");
        Dialect.MakeJsonExtract("json_query", ["json", "'$.a'"]).Should().Be("JSON_QUERY(json, '$.a')");
        Dialect.MakeJsonExtract("json_exists", ["json", "'$.a'"]).Should().Be("JSON_EXISTS(json, '$.a')");
    }

    [Fact]
    public void MakeGroupByTotals_ShouldAppendTotals()
    {
        Dialect.MakeGroupByTotals("nullableint").Should().Be("nullableint with totals");
        Dialect.SupportsGroupByWithTotals.Should().BeTrue();
    }

    [Fact]
    public void SupportsTableFunction_ShouldMatchClickHouse()
    {
        Dialect.SupportsTableFunction("numbers").Should().BeTrue();
        Dialect.SupportsTableFunction("numbers_mt").Should().BeTrue();
        Dialect.SupportsTableFunction("zeros").Should().BeTrue();
        Dialect.SupportsTableFunction("zeros_mt").Should().BeTrue();
        Dialect.SupportsTableFunction("generate_series").Should().BeFalse();
    }

    [Fact]
    public void WrapTableFunction_ShouldCastNumbersButNotZeros()
    {
        Dialect.WrapTableFunction("numbers", "numbers(3)")
            .Should().Be("(select toInt64(number) as number from numbers(3))");
        Dialect.WrapTableFunction("numbers_mt", "numbers_mt(3)")
            .Should().Be("(select toInt64(number) as number from numbers_mt(3))");
        Dialect.WrapTableFunction("zeros", "zeros(3)").Should().Be("zeros(3)");
        Dialect.WrapTableFunction("zeros_mt", "zeros_mt(3)").Should().Be("zeros_mt(3)");
    }

    [Fact]
    public void MakeQueryModifiers_ShouldRender()
    {
        Dialect.SupportsFinal.Should().BeTrue();
        Dialect.SupportsSample.Should().BeTrue();
        Dialect.SupportsPreWhere.Should().BeTrue();
        Dialect.SupportsSettings.Should().BeTrue();
        Dialect.MakeFinal().Should().Be(" final");
        Dialect.MakeSample(0.1, 0).Should().Be(" sample 0.1");
        Dialect.MakeSample(0.1, 0.5).Should().Be(" sample 0.1 offset 0.5");
        Dialect.MakeSettings([new KeyValuePair<string, string>("max_threads", "2")]).Should().Be(" settings max_threads = 2");
    }

    [Fact]
    public void MakeLimitBy_ShouldRenderClause()
    {
        Dialect.SupportsLimitBy.Should().BeTrue();

        var sb = new StringBuilder();
        Dialect.MakeLimitBy(5, 0, ["a", "b"], sb);
        sb.ToString().Should().Be("limit 5 by a, b");

        sb.Clear();
        Dialect.MakeLimitBy(5, 2, ["a"], sb);
        sb.ToString().Should().Be("limit 2, 5 by a");
    }

    [Fact]
    public void MakeDictionaryFunction_ShouldMapNames()
    {
        Dialect.MakeDictionaryFunction("dict_get", ["'d'", "'a'", "id"]).Should().Be("dictGet('d', 'a', id)");
        Dialect.MakeDictionaryFunction("dict_get_or_default", ["'d'", "'a'", "id", "0"]).Should().Be("dictGetOrDefault('d', 'a', id, 0)");
        Dialect.MakeDictionaryFunction("dict_has", ["'d'", "id"]).Should().Be("dictHas('d', id)");
    }

    [Fact]
    public void MakeQuantile_ShouldUseDoubleParenthesesAndCastToFloat64()
    {
        Dialect.MakeQuantile("quantile", "0.5", "x").Should().Be("toFloat64(quantile(0.5)(x))");
        Dialect.MakeQuantile("quantile_exact", "0.9", "x").Should().Be("toFloat64(quantileExact(0.9)(x))");
        Dialect.MakeMedian("x").Should().Be("toFloat64(median(x))");
    }

    [Fact]
    public void DateAndStringHooks_ShouldUseClickHouseForms()
    {
        Dialect.MakeDateTrunc("month", "dt").Should().Be("dateTrunc('month', dt)");
        Dialect.MakeDateTrunc("milliseconds", "dt").Should().Be("dateTrunc('millisecond', dt)");
        Dialect.MakeDateAdd("day", "2", "dt").Should().Be("addDays(dt, 2)");
        Dialect.MakeDateAdd("decade", "2", "dt").Should().Be("addYears(dt, (2) * 10)");
        Dialect.MakeEndOfMonth("dt").Should().Be("toLastDayOfMonth(dt)");
        Dialect.MakeDatePart("year", "dt").Should().Be("toInt32(toYear(dt))");
        Dialect.MakeDatePart("month", "dt").Should().Be("toInt32(toMonth(dt))");
        Dialect.MakeDatePart("dow", "dt").Should().Be("toInt32(toDayOfWeek(dt))");
        Dialect.MakeDatePart("doy", "dt").Should().Be("toInt32(toDayOfYear(dt))");
        Dialect.MakeStringAgg("somestring", "','").Should().Be("arrayStringConcat(groupArray(somestring), ',')");
    }

    [Fact]
    public void MakeDateConversion_ShouldMapToClickHouseNames()
    {
        Dialect.SupportsDateConversionFunctions.Should().BeTrue();

        Dialect.MakeDateConversion("to_date", ["s"]).Should().Be("toDate(s)");
        Dialect.MakeDateConversion("to_date_time", ["s"]).Should().Be("toDateTime(s)");
        Dialect.MakeDateConversion("to_date32", ["s"]).Should().Be("toDate32(s)");
        Dialect.MakeDateConversion("to_start_of_year", ["dt"]).Should().Be("toStartOfYear(dt)");
        Dialect.MakeDateConversion("to_start_of_month", ["dt"]).Should().Be("toStartOfMonth(dt)");
        Dialect.MakeDateConversion("to_start_of_week", ["dt"]).Should().Be("toStartOfWeek(dt)");
        Dialect.MakeDateConversion("to_monday", ["dt"]).Should().Be("toMonday(dt)");
        Dialect.MakeDateConversion("to_yyyymm", ["dt"]).Should().Be("toInt32(toYYYYMM(dt))");
        Dialect.MakeDateConversion("to_yyyymmdd", ["dt"]).Should().Be("toInt32(toYYYYMMDD(dt))");
        Dialect.MakeDateConversion("to_unix_timestamp", ["dt"]).Should().Be("toInt64(toUnixTimestamp(dt))");
    }

    [Theory]
    [InlineData("decade")]
    [InlineData("century")]
    [InlineData("millennium")]
    public void DateTrunc_WithUnsupportedField_ShouldThrow(string field)
    {
        Action act = () => Dialect.MakeDateTrunc(field, "dt");

        act.Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData(typeof(int), "Int32")]
    [InlineData(typeof(long), "Int64")]
    [InlineData(typeof(double), "Float64")]
    [InlineData(typeof(decimal), "Decimal(38, 10)")]
    public void MakeTypeName_ShouldUseClickHouseNames(Type type, string expected)
    {
        Dialect.MakeTypeName(type).Should().Be(expected);
    }

    [Fact]
    public void CteHooks_ShouldOmitRecursiveKeyword()
    {
        Dialect.MakeWith(true).Should().Be("with ");
        Dialect.MakeWith(false).Should().Be("with ");
        Dialect.MakeMaxRecursion(50).Should().BeNull();
    }

    [Fact]
    public void Paging_WithOffsetOnly_ShouldUseMaximumLimit()
    {
        var sb = new StringBuilder();

        Dialect.MakePage(new Paging { Offset = 10 }, sb);

        sb.ToString().Should().Be("limit 18446744073709551615 offset 10");
    }

    [Fact]
    public void CapabilityFlags_ShouldMatchClickHouse()
    {
        Dialect.RequireSubqueryAlias.Should().BeTrue();
        Dialect.SupportsRightFullJoin.Should().BeTrue();
        Dialect.SupportsIntersectExceptAll.Should().BeTrue();
        Dialect.SupportsApply.Should().BeFalse();
        Dialect.SupportsQueryHints.Should().BeFalse();
        Dialect.SupportsDateTrunc.Should().BeTrue();
        Dialect.SupportsDateArithmetic.Should().BeTrue();
        Dialect.SupportsDateConversionFunctions.Should().BeTrue();
        Dialect.SupportsStringAgg.Should().BeTrue();
        Dialect.SupportsArrayAgg.Should().BeFalse();
        Dialect.SupportsBitAggregates.Should().BeTrue();
        Dialect.SupportsStatisticalAggregates.Should().BeTrue();
        Dialect.SupportsRegressionAggregates.Should().BeFalse();
        Dialect.SupportsArgMinMax.Should().BeTrue();
        Dialect.SupportsIfAggregates.Should().BeTrue();
        Dialect.SupportsUniqAggregates.Should().BeTrue();
        Dialect.SupportsQuantileAggregates.Should().BeTrue();
        Dialect.SupportsAnyAggregates.Should().BeTrue();
        Dialect.SupportsAnyValueAggregate.Should().BeTrue();
        Dialect.SupportsPercentileWindow.Should().BeFalse();
        Dialect.SupportsPercentRankCumeDist.Should().BeTrue();
        Dialect.SupportsJsonExtract.Should().BeTrue();
        Dialect.SupportsDictionaries.Should().BeTrue();
        Dialect.SupportsGroupByWithTotals.Should().BeTrue();
        Dialect.SupportsGlobalPredicates.Should().BeTrue();
        Dialect.SupportsLimitBy.Should().BeTrue();
        Dialect.SupportsFinal.Should().BeTrue();
        Dialect.SupportsSample.Should().BeTrue();
        Dialect.SupportsPreWhere.Should().BeTrue();
        Dialect.SupportsSettings.Should().BeTrue();
    }

    [Fact]
    public void SessionInfoHooks_ShouldUseClickHouseForms()
    {
        Dialect.SupportsSessionInfoFunctions.Should().BeTrue();
        Dialect.SupportsSessionInfoFunction("current_user").Should().BeTrue();
        Dialect.SupportsSessionInfoFunction("current_database").Should().BeTrue();
        Dialect.SupportsSessionInfoFunction("version").Should().BeTrue();
        Dialect.SupportsSessionInfoFunction("session_user").Should().BeFalse();
        Dialect.SupportsSessionInfoFunction("current_schema").Should().BeFalse();
        Dialect.MakeSessionInfoFunction("current_user").Should().Be("currentUser()");
        Dialect.MakeSessionInfoFunction("current_database").Should().Be("currentDatabase()");
        Dialect.MakeSessionInfoFunction("version").Should().Be("version()");
    }

    [Fact]
    public void UuidHooks_ShouldUseClickHouseForms()
    {
        Dialect.SupportsUuidGenerators.Should().BeTrue();
        Dialect.SupportsUuidGenerator("gen_random_uuid").Should().BeTrue();
        Dialect.SupportsUuidGenerator("uuidv7").Should().BeTrue();
        Dialect.MakeUuidGenerator("gen_random_uuid").Should().Be("generateUUIDv4()");
        Dialect.MakeUuidGenerator("uuidv7").Should().Be("generateUUIDv7()");
    }
}
