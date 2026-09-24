using System.Text;
using FluentAssertions;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MySql.Tests;

/// <summary>
/// Direct assertions on the MySQL dialect hooks that the query-level tests do not exercise.
/// </summary>
public class MySqlDialectTests
{
    private static readonly ISqlDialect Dialect = MySqlDialect.Instance;

    [Fact]
    public void DurationHooks_ShouldUseNativeTime()
    {
        Dialect.SupportsNativeDuration.Should().BeTrue();
        Dialect.MakeDurationType(null).Should().Be("time");
        Dialect.MakeDurationType(DurationUnit.Seconds, 6).Should().Be("time(6)");
        Dialect.MakeNullableDurationType(DurationUnit.Seconds, 6).Should().Be("time(6)");
        Dialect.MakeTypeName(typeof(TimeSpan)).Should().Be("time");
    }

    [Fact]
    public void MakeConcat_ShouldUseConcatFunction()
    {
        Dialect.MakeConcat(["a", "b"]).Should().Be("concat(a, b)");
        Dialect.MakeConcat(["a", "b", "c"]).Should().Be("concat(a, b, c)");
    }

    [Fact]
    public void LockingHooks_ShouldSupportWaitModes()
    {
        Dialect.Lock.Should().NotBeNull();
        Dialect.Lock!.Render(LockMode.Update).Should().Be(" for update");
        Dialect.Lock!.Render(LockMode.Share).Should().Be(" lock in share mode");
        Dialect.Lock!.Render(LockMode.Update, LockWaitMode.NoWait).Should().Be(" for update nowait");
        Dialect.Lock!.Render(LockMode.Update, LockWaitMode.SkipLocked).Should().Be(" for update skip locked");
        Dialect.Lock!.Render(LockMode.Share, LockWaitMode.NoWait).Should().Be(" for share nowait");
        Dialect.Lock!.Render(LockMode.Share, LockWaitMode.SkipLocked).Should().Be(" for share skip locked");
    }

    [Fact]
    public void QuoteIdentifier_ShouldUseBackticks()
    {
        Dialect.QuoteIdentifier("id").Should().Be("`id`");
        Dialect.QuoteIdentifier("a`b").Should().Be("`a``b`");
    }

    [Fact]
    public void TextJsonHooks_ShouldUseJsonExtractFamily()
    {
        Dialect.SupportsTextJson.Should().BeTrue();
        Dialect.MakeTextJsonFunction("json_value", ["j", "'$.id'"]).Should().Be("json_unquote(json_extract(j, '$.id'))");
        Dialect.MakeTextJsonFunction("json_query", ["j", "'$.name'"]).Should().Be("json_extract(j, '$.name')");
        Dialect.MakeTextJsonFunction("json_modify", ["j", "'$.id'", "'1'"]).Should().Be("json_set(j, '$.id', '1')");
        Dialect.MakeIsJson("j", asPredicate: true).Should().Be("(json_valid(j)) = 1");
        Dialect.MakeIsJson("j", asPredicate: false).Should().Be("json_valid(j)");
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
        Dialect.MakeDatePart("year", "dt").Should().Be("extract(year from dt)");
        Dialect.MakeDatePart("doy", "dt").Should().Be("dayofyear(dt)");
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
        Dialect.SupportsQueryHints.Should().BeTrue();
        Dialect.SupportsCube.Should().BeFalse();
        Dialect.SupportsDateArithmetic.Should().BeTrue();
        Dialect.SupportsStringAgg.Should().BeTrue();
        Dialect.SupportsFullText.Should().BeTrue();
        Dialect.SupportsAnyValueAggregate.Should().BeTrue();
        Dialect.MakeAggregate("any_agg").Should().Be("ANY_VALUE");
        Dialect.SupportsPercentileWindow.Should().BeFalse();
    }

    [Fact]
    public void QueryHintHooks_ShouldUseInlineOptimizerHintComment()
    {
        Dialect.SupportsQueryHints.Should().BeTrue();
        Dialect.RenderQueryHints("select 1", ["MAX_EXECUTION_TIME(1000)"], null)
            .Should().Be("select /*+ MAX_EXECUTION_TIME(1000) */ 1");
        Dialect.RenderQueryHints("select 1", ["NO_RANGE_OPTIMIZATION(t idx)"], null)
            .Should().Be("select /*+ NO_RANGE_OPTIMIZATION(t idx) */ 1");
        Dialect.RenderQueryHints("with recent as (select 1) select recent.* from recent", ["MAX_EXECUTION_TIME(1000)"], null)
            .Should().Be("with recent as (select 1) select /*+ MAX_EXECUTION_TIME(1000) */ recent.* from recent");
        Dialect.RenderQueryHints("with selected as (select 1) select selected.* from selected", ["h"], null)
            .Should().Be("with selected as (select 1) select /*+ h */ selected.* from selected");
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

    [Fact]
    public void SessionInfoHooks_ShouldUseMySqlForms()
    {
        Dialect.SessionInfoFunctions.Should().NotBeNull();
        Dialect.SessionInfoFunctions!.Supports("current_schema").Should().BeTrue();
        Dialect.SessionInfoFunctions!.Render("current_user").Should().Be("current_user()");
        Dialect.SessionInfoFunctions!.Render("session_user").Should().Be("session_user()");
        Dialect.SessionInfoFunctions!.Render("current_schema").Should().Be("schema()");
        Dialect.SessionInfoFunctions!.Render("current_database").Should().Be("database()");
        Dialect.SessionInfoFunctions!.Render("version").Should().Be("version()");
    }

    [Fact]
    public void UuidHooks_ShouldBeUnsupported()
    {
        // MySQL has only UUID() (v1); a v4/v7 generator is not available.
        Dialect.UuidGenerators.Should().BeNull();
    }
}
