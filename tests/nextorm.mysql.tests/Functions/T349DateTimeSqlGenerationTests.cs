using FluentAssertions;
using NextORM.Core;
using NextORM.MySql;

namespace NextORM.MySql.Tests;

/// <summary>
/// SQL generation for the T349 MySQL 8.4 date/time catalogue (no database): every name renders its
/// native spelling, SQL NULL arguments stay NULL, the <c>get_format</c> grammar constants are validated
/// before any SQL is emitted, and the whole catalogue is reported supported by the MySQL capability
/// while MariaDB keeps refusing it.
/// </summary>
public class T349DateTimeSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void DateTimeFunctions_ShouldRenderNatively()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.adddate(x.Datetime, 1)))
            .Should().Contain("adddate(dt, 1)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.subdate(x.Datetime, 1)))
            .Should().Contain("subdate(dt, 1)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.addtime(x.Datetime, SqlFunctions.Parameter<TimeSpan>(0))))
            .Should().Contain("addtime(dt, @norm_p0)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.addtime(
                SqlFunctions.Parameter<TimeSpan>(0), SqlFunctions.Parameter<TimeSpan>(1))))
            .Should().Contain("addtime(@norm_p0, @norm_p1)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.subtime(x.Datetime, SqlFunctions.Parameter<TimeSpan>(0))))
            .Should().Contain("subtime(dt, @norm_p0)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.subtime(
                SqlFunctions.Parameter<TimeSpan>(0), SqlFunctions.Parameter<TimeSpan>(1))))
            .Should().Contain("subtime(@norm_p0, @norm_p1)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.convert_tz(x.Datetime, "UTC", "Europe/Berlin")))
            .Should().Contain("convert_tz(dt, _utf8mb4 X'555443', _utf8mb4 X'4575726f70652f4265726c696e')");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.dayname(x.Datetime)))
            .Should().Contain("dayname(dt)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.from_days(730669L)))
            .Should().Contain("from_days(730669)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.get_format(MySqlFormatKind.Date, "EUR")))
            .Should().Contain("get_format(DATE, 'EUR')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.get_format(MySqlFormatKind.DateTime, "JIS")))
            .Should().Contain("get_format(DATETIME, 'JIS')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.makedate(2020, 42)))
            .Should().Contain("makedate(2020, 42)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.maketime(12, 34, 56.789m)))
            .Should().Contain("maketime(12, 34, 56.789)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.microsecond(x.Datetime)))
            .Should().Contain("microsecond(dt)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.microsecond(SqlFunctions.Parameter<TimeSpan>(0))))
            .Should().Contain("microsecond(@norm_p0)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.monthname(x.Datetime)))
            .Should().Contain("monthname(dt)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.period_add(202001L, 2L)))
            .Should().Contain("period_add(202001, 2)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.period_diff(202001L, 201901L)))
            .Should().Contain("period_diff(202001, 201901)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.sec_to_time(45296.789m)))
            .Should().Contain("sec_to_time(45296.789)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.time(x.Datetime)))
            .Should().Contain("time(dt)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.time(SqlFunctions.Parameter<TimeSpan>(0))))
            .Should().Contain("time(@norm_p0)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.time_format(
                SqlFunctions.Parameter<TimeSpan>(0), "%H:%i")))
            .Should().Contain("time_format(@norm_p0, _utf8mb4 X'25483a2569')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.time_to_sec(SqlFunctions.Parameter<TimeSpan>(0))))
            .Should().Contain("time_to_sec(@norm_p0)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.timediff(x.Datetime, SqlFunctions.Parameter<DateTime>(0))))
            .Should().Contain("timediff(dt, @norm_p0)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.timediff(
                SqlFunctions.Parameter<TimeSpan>(0), SqlFunctions.Parameter<TimeSpan>(1))))
            .Should().Contain("timediff(@norm_p0, @norm_p1)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.timestamp(x.Datetime)))
            .Should().Contain("timestamp(dt)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.timestamp(x.Datetime, SqlFunctions.Parameter<TimeSpan>(0))))
            .Should().Contain("timestamp(dt, @norm_p0)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.to_days(x.Datetime)))
            .Should().Contain("to_days(dt)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.to_seconds(x.Datetime)))
            .Should().Contain("to_seconds(dt)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.yearweek(x.Datetime)))
            .Should().Contain("yearweek(dt)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.yearweek(x.Datetime, 1)))
            .Should().Contain("yearweek(dt, 1)");
    }

    [Fact]
    public void DateTimeFunctions_WithNullArguments_ShouldRenderSqlNull()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.adddate((DateTime?)null, 1)))
            .Should().Contain("adddate(null, 1)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.subdate((DateTime?)null, 1)))
            .Should().Contain("subdate(null, 1)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.addtime((DateTime?)null, (TimeSpan?)null)))
            .Should().Contain("addtime(null, null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.subtime((TimeSpan?)null, (TimeSpan?)null)))
            .Should().Contain("subtime(null, null)");
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.convert_tz(x.Datetime, null, null)))
            .Should().Contain("convert_tz(dt, null, null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.dayname((DateTime?)null)))
            .Should().Contain("dayname(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.from_days(null)))
            .Should().Contain("from_days(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.makedate(null, 42)))
            .Should().Contain("makedate(null, 42)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.maketime(12, null, 56.789m)))
            .Should().Contain("maketime(12, null, 56.789)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.microsecond((TimeSpan?)null)))
            .Should().Contain("microsecond(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.monthname((DateTime?)null)))
            .Should().Contain("monthname(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.period_add(null, 2L)))
            .Should().Contain("period_add(null, 2)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.period_diff(202001L, null)))
            .Should().Contain("period_diff(202001, null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.sec_to_time(null)))
            .Should().Contain("sec_to_time(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.time((TimeSpan?)null)))
            .Should().Contain("time(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.time_format(null, "%H:%i")))
            .Should().Contain("time_format(null, _utf8mb4 X'25483a2569')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.time_to_sec(null)))
            .Should().Contain("time_to_sec(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.timediff((DateTime?)null, (DateTime?)null)))
            .Should().Contain("timediff(null, null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.timestamp((DateTime?)null)))
            .Should().Contain("timestamp(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_days(null)))
            .Should().Contain("to_days(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_seconds(null)))
            .Should().Contain("to_seconds(null)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.yearweek((DateTime?)null, 1)))
            .Should().Contain("yearweek(null, 1)");
    }

    [Fact]
    public void GetFormat_InvalidArguments_ShouldThrowBeforeSql()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        const string message =
            "MySQL GET_FORMAT() requires a compile-time IANA format kind and format literal.";

        // An enum value outside the declared families.
        var invalidKind = () => SqlOf(ctx, e.Select(_ =>
            SqlFunctions.MySql.get_format((MySqlFormatKind)99, "EUR")));
        invalidKind.Should().Throw<NotSupportedException>().WithMessage(message);

        // A platform format the vendor does not document.
        var invalidFormat = () => SqlOf(ctx, e.Select(_ =>
            SqlFunctions.MySql.get_format(MySqlFormatKind.Date, "XYZ")));
        invalidFormat.Should().Throw<NotSupportedException>().WithMessage(message);

        // A captured (not compile-time) kind.
        var kind = MySqlFormatKind.Date;
        var nonConstantKind = () => SqlOf(ctx, e.Select(_ =>
            SqlFunctions.MySql.get_format(kind, "EUR")));
        nonConstantKind.Should().Throw<NotSupportedException>().WithMessage(message);

        // A column-valued format, and a missing literal.
        var nonConstantFormat = () => SqlOf(ctx, e.Select(x =>
            SqlFunctions.MySql.get_format(MySqlFormatKind.Date, x.String)));
        nonConstantFormat.Should().Throw<NotSupportedException>().WithMessage(message);

        var nullFormat = () => SqlOf(ctx, e.Select(_ =>
            SqlFunctions.MySql.get_format(MySqlFormatKind.Date, null)));
        nullFormat.Should().Throw<NotSupportedException>().WithMessage(message);
    }

    [Fact]
    public void T349DateTimeFunctions_ShouldBeReportedSupportedOnMySqlAndMariaDb()
    {
        var capability = MySqlDialect.Instance.MySqlFunctions!;
        string[] names =
        [
            "adddate", "addtime", "convert_tz", "dayname", "from_days", "get_format",
            "makedate", "maketime", "microsecond", "monthname", "period_add", "period_diff",
            "sec_to_time", "subdate", "subtime", "time", "time_format", "time_to_sec",
            "timediff", "timestamp", "to_days", "to_seconds", "yearweek"
        ];

        names.Should().HaveCount(23);
        foreach (var name in names)
            capability.Supports(name).Should().BeTrue($"'{name}' is a MySQL 8.4 gate key");

        // #284 (T350 S1: MariaDB 11.4.13 accepted every one of these names) resolves the T349 (b2)
        // temporary exclusion, so MariaDB now claims the same date/time catalogue as MySQL.
        var mariadb = NextORM.MariaDb.MariaDbDialect.Instance.MySqlFunctions!;

        foreach (var name in names)
            mariadb.Supports(name).Should().BeTrue($"#{284} delivered '{name}' on MariaDB 11.4");
    }
}
