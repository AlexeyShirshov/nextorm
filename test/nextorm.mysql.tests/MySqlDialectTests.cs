using System.Text;
using FluentAssertions;
using nextorm.core;
using nextorm.mysql;

namespace nextorm.mysql.tests;

/// <summary>
/// Direct assertions on the MySQL dialect hooks that the query-level tests do not exercise.
/// </summary>
public class MySqlDialectTests
{
    private static readonly ISqlDialect Dialect = MySqlDialect.Instance;

    [Fact]
    public void MakeConcat_ShouldUseConcatFunction()
    {
        Dialect.MakeConcat(["a", "b"]).Should().Be("concat(a, b)");
        Dialect.MakeConcat(["a", "b", "c"]).Should().Be("concat(a, b, c)");
    }

    [Fact]
    public void IdentifierAndScalarFunctionHooks_ShouldUseMySqlForms()
    {
        Dialect.Escape("t1").Should().Be("`t1`");
        Dialect.MakeColumnReference("t1").Should().Be("`t1`");
        Dialect.MakeParam("p0").Should().Be("@p0");
        Dialect.MakeCoalesce("a", "b").Should().Be("coalesce(a, b)");
        Dialect.MakeStringLength("x").Should().Be("char_length(x)");
        Dialect.MakeNow(true).Should().Be("utc_timestamp()");
        Dialect.MakeNow(false).Should().Be("now()");
    }

    [Theory]
    [InlineData("stdev", "stddev_samp")]
    [InlineData("stdevp", "stddev_pop")]
    [InlineData("var", "var_samp")]
    [InlineData("varp", "var_pop")]
    [InlineData("sum", "sum")]
    public void MakeAggregate_ShouldMapProviderNames(string name, string expected)
    {
        Dialect.MakeAggregate(name).Should().Be(expected);
    }

    [Fact]
    public void Paging_WithLimitAndOffset_ShouldEmitLimitBeforeOffset()
    {
        var sb = new StringBuilder();

        Dialect.MakePage(new Paging { Limit = 5, Offset = 10 }, sb);

        sb.ToString().Should().Be("limit 5 offset 10");
    }

    [Fact]
    public void Paging_WithOffsetOnly_ShouldUseMaximumLimit()
    {
        var sb = new StringBuilder();

        Dialect.MakePage(new Paging { Offset = 10 }, sb);

        sb.ToString().Should().Be("limit 18446744073709551615 offset 10");
    }

    [Fact]
    public void GroupingModifierHooks_ShouldUseWithRollup()
    {
        Dialect.SupportsRollup.Should().BeTrue();
        Dialect.SupportsCube.Should().BeFalse();
        Dialect.MakeGrouping("a, b", GroupingType.Rollup).Should().Be("a, b with rollup");
        Dialect.MakeGrouping("a, b", GroupingType.None).Should().Be("a, b");
    }

    [Fact]
    public void CapabilityFlags_ShouldMatchMySql()
    {
        Dialect.RequireSubqueryAlias.Should().BeTrue();
        Dialect.SupportsRightFullJoin.Should().BeTrue();
        Dialect.SupportsFullJoin.Should().BeFalse();
        Dialect.SupportsIntersectExceptAll.Should().BeFalse();
        Dialect.SupportsApply.Should().BeTrue();
        Dialect.SupportsQueryHints.Should().BeFalse();
        Dialect.SupportsCube.Should().BeFalse();
        Dialect.SupportsDateArithmetic.Should().BeTrue();
        Dialect.SupportsStringAgg.Should().BeTrue();
        Dialect.SupportsFullText.Should().BeTrue();
    }

    [Fact]
    public void DateAndFullTextHooks_ShouldUseMySqlForms()
    {
        Dialect.MakeDateAdd("day", "n", "x").Should().Be("date_add(x, interval n day)");
        Dialect.MakeDateAdd("milliseconds", "n", "x").Should().Be("date_add(x, interval (n) * 1000 microsecond)");
        Dialect.MakeDateDiff("milliseconds", "a", "b")
            .Should().Be("cast((timestampdiff(microsecond, a, b) / 1000) as signed)");
        Dialect.MakeEndOfMonth("x").Should().Be("last_day(x)");
        Dialect.MakeDateFromParts("y", "m", "d")
            .Should().Be("str_to_date(concat_ws('-', y, m, d), '%Y-%m-%d')");
        Dialect.MakeStringAgg("x", "','").Should().Be("group_concat(x separator ',')");
        Dialect.MakeFullText("contains", "c", "@p")
            .Should().Be("(match(c) against(@p in boolean mode) > 0)");
        Dialect.MakeFullText("freetext", "c", "@p").Should().Be("(match(c) against(@p) > 0)");
    }

    [Fact]
    public void MakeTypeName_ShouldUseMySqlCastTargets()
    {
        Dialect.MakeTypeName(typeof(byte)).Should().Be("unsigned");
        Dialect.MakeTypeName(typeof(short)).Should().Be("signed");
        Dialect.MakeTypeName(typeof(int)).Should().Be("signed");
        Dialect.MakeTypeName(typeof(long)).Should().Be("signed");
        Dialect.MakeTypeName(typeof(double)).Should().Be("double");
        Dialect.MakeTypeName(typeof(decimal)).Should().Be("decimal");
        Dialect.MakeTypeName(typeof(DateTime)).Should().Be("datetime");
    }

    [Fact]
    public void MakeLikeEscape_ShouldDoubleTheBackslash()
    {
        Dialect.MakeLikeEscape("\\").Should().Be(" escape '\\\\'");
        Dialect.MakeLikeEscape("!").Should().Be(" escape '!'");
    }

    [Fact]
    public void MakeOnesComplement_ShouldKeepTheResultSigned()
    {
        Dialect.MakeOnesComplement("x").Should().Be("(-(x) - 1)");
    }
}
