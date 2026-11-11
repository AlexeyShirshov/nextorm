using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T256 (#256) ClickHouse <c>date_add</c> rendering: the year family (<c>year</c>/<c>decade</c>/<c>century</c>)
/// promotes its operand to <c>DateTime64(6)</c>, because <c>addYears</c> on a 4-byte <c>DateTime</c> wraps
/// past 2106 while <c>DateTime64</c> reaches 2299. Every other part keeps its previous rendering, and the
/// <c>millennium</c>/unknown parts stay rejected before any SQL is emitted.
/// </summary>
public class T256YearFamilySqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void YearFamily_PromotesOperandToMicroseconds()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("year", 1, x.Datetime) }))
            .Should().Contain("addYears(toDateTime64(dt, 6), 1)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("decade", 2, x.Datetime) }))
            .Should().Contain("addYears(toDateTime64(dt, 6), (2) * 10)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("century", 3, x.Datetime) }))
            .Should().Contain("addYears(toDateTime64(dt, 6), (3) * 100)");
    }

    [Fact]
    public void OtherUnits_KeepExistingRendering()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("quarter", 1, x.Datetime) }))
            .Should().Contain("addQuarters(dt, 1)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("month", 1, x.Datetime) }))
            .Should().Contain("addMonths(dt, 1)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("week", 1, x.Datetime) }))
            .Should().Contain("addWeeks(dt, 1)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("day", 1, x.Datetime) }))
            .Should().Contain("addDays(dt, 1)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("hour", 1, x.Datetime) }))
            .Should().Contain("addHours(toDateTime(dt), 1)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("minute", 1, x.Datetime) }))
            .Should().Contain("addMinutes(toDateTime(dt), 1)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("second", 1, x.Datetime) }))
            .Should().Contain("addSeconds(toDateTime(dt), 1)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("milliseconds", 5, x.Datetime) }))
            .Should().Contain("addMilliseconds(toDateTime64(dt, 3), 5)");

        SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("microseconds", 5, x.Datetime) }))
            .Should().Contain("addMicroseconds(toDateTime64(dt, 6), 5)");
    }

    /// <summary>
    /// <c>millennium</c> is rejected by the core capability guard before any SQL is generated, for a
    /// positive, zero and negative amount alike; the dialect's own range-specific throw stays as the
    /// defensive backstop for direct <c>MakeDateAdd</c> callers.
    /// </summary>
    [Fact]
    public void Millennium_IsUnsupportedAndThrows()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Action positive = () => SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("millennium", 1, x.Datetime) }));
        Action zero = () => SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("millennium", 0, x.Datetime) }));
        Action negative = () => SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("millennium", -1, x.Datetime) }));

        positive.Should().Throw<NotSupportedException>().WithMessage("*'millennium' is not a supported date_add field*");
        zero.Should().Throw<NotSupportedException>().WithMessage("*'millennium' is not a supported date_add field*");
        negative.Should().Throw<NotSupportedException>().WithMessage("*'millennium' is not a supported date_add field*");

        Action direct = () => ClickHouseDialect.Instance.MakeDateAdd("millennium", "1", "dt");

        direct.Should().Throw<NotSupportedException>().WithMessage("*does not support the 'millennium' field*1900-2299*");
    }

    [Fact]
    public void UnknownUnit_RemainsRejected()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        Action act = () => SqlOf(ctx, e.Select(x => new { D = SqlFunctions.Sql.date_add("fortnight", 1, x.Datetime) }));

        act.Should().Throw<NotSupportedException>().WithMessage("*'fortnight' is not a supported date_add field*");
    }
}
