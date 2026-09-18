using System.Text;
using FluentAssertions;
using nextorm.core;
using nextorm.clickhouse;

namespace nextorm.clickhouse.tests;

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
    [InlineData("count_if", "countIf")]
    [InlineData("sum_if", "sumIf")]
    [InlineData("sum", "sum")]
    public void MakeAggregate_ShouldMapProviderNames(string name, string expected)
    {
        Dialect.MakeAggregate(name).Should().Be(expected);
    }

    [Fact]
    public void DateAndStringHooks_ShouldUseClickHouseForms()
    {
        Dialect.MakeDateTrunc("month", "dt").Should().Be("dateTrunc('month', dt)");
        Dialect.MakeDateTrunc("milliseconds", "dt").Should().Be("dateTrunc('millisecond', dt)");
        Dialect.MakeDateAdd("day", "2", "dt").Should().Be("addDays(dt, 2)");
        Dialect.MakeDateAdd("decade", "2", "dt").Should().Be("addYears(dt, (2) * 10)");
        Dialect.MakeEndOfMonth("dt").Should().Be("toLastDayOfMonth(dt)");
        Dialect.MakeStringAgg("somestring", "','").Should().Be("arrayStringConcat(groupArray(somestring), ',')");
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
        Dialect.SupportsStringAgg.Should().BeTrue();
        Dialect.SupportsArrayAgg.Should().BeFalse();
        Dialect.SupportsBitAggregates.Should().BeTrue();
        Dialect.SupportsStatisticalAggregates.Should().BeTrue();
        Dialect.SupportsRegressionAggregates.Should().BeFalse();
        Dialect.SupportsArgMinMax.Should().BeTrue();
        Dialect.SupportsIfAggregates.Should().BeTrue();
    }
}
