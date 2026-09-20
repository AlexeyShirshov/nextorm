using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Direct assertions on the SQLite dialect hooks. The date-part cases and the capability flags are
/// not exercised by the existing query-level tests.
/// </summary>
public class SqliteDialectTests
{
    private static readonly ISqlDialect Dialect = SqliteDialect.Instance;

    [Theory]
    [InlineData("year", "cast(strftime('%Y', dt) as integer)")]
    [InlineData("month", "cast(strftime('%m', dt) as integer)")]
    [InlineData("day", "cast(strftime('%d', dt) as integer)")]
    [InlineData("hour", "cast(strftime('%H', dt) as integer)")]
    [InlineData("minute", "cast(strftime('%M', dt) as integer)")]
    [InlineData("second", "cast(strftime('%S', dt) as integer)")]
    [InlineData("doy", "cast(strftime('%j', dt) as integer)")]
    [InlineData("quarter", "cast((cast(strftime('%m', dt) as integer) + 2) / 3 as integer)")]
    [InlineData("week", "cast((cast(strftime('%j', date(dt, '-3 days', 'weekday 4')) as integer) + 6) / 7 as integer)")]
    [InlineData("dow", "cast(strftime('%w', dt) as integer)")]
    [InlineData("isodow", "((cast(strftime('%w', dt) as integer) + 6) % 7 + 1)")]
    [InlineData("epoch", "((julianday(dt) - 2440587.5) * 86400.0)")]
    public void MakeDatePart_ShouldUseStrftimeForm(string part, string expected)
    {
        Dialect.MakeDatePart(part, "dt").Should().Be(expected);
    }

    [Fact]
    public void SupportsDatePart_ShouldCoverTheExtendedParts()
    {
        Dialect.SupportsDatePart("year").Should().BeTrue();
        Dialect.SupportsDatePart("quarter").Should().BeTrue();
        Dialect.SupportsDatePart("week").Should().BeTrue();
        Dialect.SupportsDatePart("dow").Should().BeTrue();
        Dialect.SupportsDatePart("isodow").Should().BeTrue();
        Dialect.SupportsDatePart("epoch").Should().BeTrue();
        Dialect.SupportsDatePart("nonsense").Should().BeFalse();
    }

    [Fact]
    public void ScalarFunctionHooks_ShouldUseSqliteForms()
    {
        Dialect.MakeStringLength("x").Should().Be("length(x)");
        Dialect.MakeMathFunction("log", ["x"]).Should().Be("ln(x)");
        Dialect.MakeMathFunction("round", ["x", "2"]).Should().Be("round(x, 2)");
        Dialect.MakeFunction("fn", "app").Should().Be("app.fn");
        Dialect.MakeFunction("fn", null).Should().Be("fn");
        Dialect.MakeNow(true).Should().Be("datetime('now')");
        Dialect.MakeNow(false).Should().Be("datetime('now')");
        Dialect.ConcatStringOperator.Should().Be("||");
    }

    [Fact]
    public void CteHooks_ShouldUseRecursiveKeywordAndNoMaxRecursion()
    {
        Dialect.MakeWith(true).Should().Be("with recursive ");
        Dialect.MakeWith(false).Should().Be("with ");
        Dialect.MakeMaxRecursion(50).Should().BeNull();
    }

    [Fact]
    public void GreatestLeast_ShouldUseScalarMaxMin()
    {
        Dialect.SupportsGreatestLeast.Should().BeTrue();
        Dialect.MakeGreatest(["a", "b"]).Should().Be("max(a, b)");
        Dialect.MakeLeast(["a", "b"]).Should().Be("min(a, b)");
        Dialect.MakeGreatest(["a"]).Should().Be("(a)");
        Dialect.MakeLeast(["a"]).Should().Be("(a)");
    }

    [Fact]
    public void CapabilityFlags_ShouldMatchSqlite()
    {
        Dialect.RequireSubqueryAlias.Should().BeFalse();
        Dialect.SupportsRightFullJoin.Should().BeTrue();
        Dialect.SupportsIntersectExceptAll.Should().BeFalse();
        Dialect.SupportsApply.Should().BeFalse();
        Dialect.SupportsQueryHints.Should().BeFalse();
        Dialect.SupportsDateArithmetic.Should().BeTrue();
        Dialect.SupportsStringAgg.Should().BeTrue();
        Dialect.SupportsAnyValueAggregate.Should().BeFalse();
        Dialect.SupportsPercentileWindow.Should().BeFalse();
    }

    [Fact]
    public void DateHooks_ShouldUseSqliteModifiers()
    {
        Dialect.MakeDateAdd("day", "n", "x").Should().Be("datetime(x, (n) || ' days')");
        Dialect.MakeDateAdd("milliseconds", "n", "x")
            .Should().Be("strftime('%Y-%m-%d %H:%M:%f', x, ((n) / 1000.0) || ' seconds')");
        Dialect.MakeDateAdd("quarter", "n", "x")
            .Should().Be("datetime(x, ((n) * 3) || ' months')");
        Dialect.MakeDateDiff("month", "a", "b")
            .Should().Be("((cast(strftime('%Y', b) as integer) * 12 + cast(strftime('%m', b) as integer)) - (cast(strftime('%Y', a) as integer) * 12 + cast(strftime('%m', a) as integer)))");
        Dialect.MakeEndOfMonth("x").Should().Be("date(x, 'start of month', '+1 month', '-1 day')");
        Dialect.MakeDateFromParts("y", "m", "d").Should().Be("date(printf('%04d-%02d-%02d', y, m, d))");
        Dialect.MakeStringAgg("x", "','").Should().Be("group_concat(x, ',')");
    }

    [Fact]
    public void BuiltInTableFunctions_ShouldBeGated()
    {
        Dialect.SupportsTableFunction("generate_series").Should().BeFalse();
        Dialect.SupportsTableFunction("string_split").Should().BeFalse();
    }

    [Fact]
    public void SessionInfoHooks_ShouldExposeOnlyTheVersion()
    {
        Dialect.SupportsSessionInfoFunctions.Should().BeTrue();
        Dialect.SupportsSessionInfoFunction("version").Should().BeTrue();
        Dialect.SupportsSessionInfoFunction("current_user").Should().BeFalse();
        Dialect.SupportsSessionInfoFunction("current_database").Should().BeFalse();
        Dialect.MakeSessionInfoFunction("version").Should().Be("sqlite_version()");
    }

    [Fact]
    public void UuidHooks_ShouldBeUnsupported()
    {
        Dialect.SupportsUuidGenerators.Should().BeFalse();
        Dialect.SupportsUuidGenerator("gen_random_uuid").Should().BeFalse();
    }
}
