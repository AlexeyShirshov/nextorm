using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

[SqlTable("t217_dto")]
public interface IT217DtoEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("value")]
    DateTimeOffset Value { get; set; }

    [Column("maybe")]
    DateTimeOffset? Maybe { get; set; }
}

/// <summary>
/// T217 (#217) SQL Server SQL-generation coverage for the server-side <see cref="DateTimeOffset"/>
/// translation. SQL Server has a native <c>datetimeoffset</c>: <c>dateadd</c> preserves the operand's
/// offset, and a fractional amount is scaled to the next finer unit so the fraction is not truncated.
/// The client clock is never captured.
/// </summary>
public class T217DateTimeOffsetSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void DtoNow_ShouldUseNativeDatetimeoffset()
    {
        using var ctx = SqlServerTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.Now))
            .Should().Contain("sysdatetimeoffset() as [Now]");
    }

    [Fact]
    public void DtoUtcNow_ShouldBeZeroOffsetDatetimeoffset()
    {
        using var ctx = SqlServerTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.UtcNow))
            .Should().Contain("todatetimeoffset(sysutcdatetime(), '+00:00') as [UtcNow]");
    }

    [Theory]
    [InlineData("Year", "datepart(year, value)")]
    [InlineData("Month", "datepart(month, value)")]
    [InlineData("Day", "datepart(day, value)")]
    [InlineData("Hour", "datepart(hour, value)")]
    [InlineData("Minute", "datepart(minute, value)")]
    [InlineData("Second", "datepart(second, value)")]
    [InlineData("DayOfYear", "datepart(dayofyear, value)")]
    public void DtoPart_ShouldUseDatepart(string member, string expected)
    {
        using var ctx = SqlServerTestContext.Create();
        var sql = member switch
        {
            "Year" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Value.Year })),
            "Month" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Value.Month })),
            "Day" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Value.Day })),
            "Hour" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Value.Hour })),
            "Minute" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Value.Minute })),
            "Second" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Value.Second })),
            _ => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Value.DayOfYear })),
        };

        sql.Should().Contain(expected);
    }

    [Fact]
    public void DtoNullablePart_ShouldPreserveColumn()
    {
        using var ctx = SqlServerTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Maybe!.Value.Year }))
            .Should().Contain("datepart(year, maybe)");
    }

    [Theory]
    [InlineData("AddYears", "dateadd(year, 1, value)")]
    [InlineData("AddMonths", "dateadd(month, 1, value)")]
    [InlineData("AddDays", "dateadd(hour, (1) * 24, value)")]
    [InlineData("AddHours", "dateadd(minute, (1) * 60, value)")]
    [InlineData("AddMinutes", "dateadd(second, (1) * 60, value)")]
    [InlineData("AddSeconds", "dateadd(second, cast(floor(1) as int), dateadd(millisecond, cast((1 - floor(1)) * 1000 as int), value))")]
    [InlineData("AddMilliseconds", "dateadd(millisecond, cast(floor(1) as int), dateadd(microsecond, cast((1 - floor(1)) * 1000 as int), value))")]
    public void DtoAdd_ShouldScaleFractionalUnit(string member, string expected)
    {
        using var ctx = SqlServerTestContext.Create();
        var sql = member switch
        {
            "AddYears" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddYears(1) })),
            "AddMonths" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddMonths(1) })),
            "AddDays" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(1) })),
            "AddHours" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddHours(1) })),
            "AddMinutes" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddMinutes(1) })),
            "AddSeconds" => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddSeconds(1) })),
            _ => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddMilliseconds(1) })),
        };

        sql.Should().Contain(expected);
    }

    [Fact]
    public void DtoAddFractionalDay_ShouldScaleToFinerUnit()
    {
        using var ctx = SqlServerTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(0.5) }))
            .Should().Contain("dateadd(hour, (0.5) * 24, value)");
    }

    [Fact]
    public void DtoAddLargeLiteralAmount_ShouldNotOverflowTheScaledProduct()
    {
        // A large whole amount scaled in place ((2200000) * 1000) overflows dateadd's int amount; the
        // whole part must stay in the requested unit and only the fraction is scaled to the finer unit.
        using var ctx = SqlServerTestContext.Create();
        var seconds = SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddSeconds(2_200_000) }));
        seconds.Should().Contain("dateadd(second, cast(floor(2200000) as int)");
        seconds.Should().NotContain("(2200000) * 1000");

        var millis = SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddMilliseconds(2_200_000) }));
        millis.Should().Contain("dateadd(millisecond, cast(floor(2200000) as int)");
        millis.Should().NotContain("(2200000) * 1000");
    }

    [Fact]
    public void DtoAddFractionalSecond_ShouldKeepTheRemainderInFinerUnit()
    {
        using var ctx = SqlServerTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddSeconds(1.5) }))
            .Should().Contain("dateadd(millisecond, cast((1.5 - floor(1.5)) * 1000 as int)");
    }

    [Fact]
    public void DtoPart_ShouldBeTranslatedInWhereAndOrderBy()
    {
        using var ctx = SqlServerTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Where(x => x.Value.Year == 2023).Select(x => new { x.Id }))
            .Should().Contain("where datepart(year, value) = 2023");
        SqlOf(ctx, ctx.From<IT217DtoEntity>().OrderBy(x => x.Value.Year).Select(x => new { x.Id }))
            .Should().Contain("order by datepart(year, value)");
    }

    [Theory]
    [InlineData("Offset")]
    [InlineData("UtcDateTime")]
    [InlineData("LocalDateTime")]
    [InlineData("Date")]
    [InlineData("Ticks")]
    [InlineData("DayOfWeek")]
    public void UnsupportedMembers_ShouldThrowBeforeSql(string member)
    {
        using var ctx = SqlServerTestContext.Create();
        Func<string> act = member switch
        {
            "Offset" => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Offset })),
            "UtcDateTime" => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.UtcDateTime })),
            "LocalDateTime" => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.LocalDateTime })),
            "Date" => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Date })),
            "Ticks" => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Ticks })),
            _ => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.DayOfWeek })),
        };

        act.Should().Throw<NotSupportedException>().WithMessage($"*DateTimeOffset.{member}*");
    }

    [Theory]
    [InlineData("ToOffset")]
    [InlineData("AddTicks")]
    [InlineData("Add")]
    public void UnsupportedMethods_ShouldThrowBeforeSql(string method)
    {
        using var ctx = SqlServerTestContext.Create();
        var cmd = method switch
        {
            "ToOffset" => ctx.From<IT217DtoEntity>().Select(x => x.Value.ToOffset(TimeSpan.Zero)),
            "AddTicks" => ctx.From<IT217DtoEntity>().Select(x => x.Value.AddTicks(5)),
            _ => ctx.From<IT217DtoEntity>().Select(x => x.Value.Add(TimeSpan.FromDays(1))),
        };

        var act = () => SqlOf(ctx, cmd);

        act.Should().Throw<NotSupportedException>().WithMessage($"*DateTimeOffset.{method}*");
    }

    [Fact]
    public void NestedUnsupportedMember_ShouldThrowBeforeSql()
    {
        using var ctx = SqlServerTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Offset.TotalMinutes }));

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset.Offset*");
    }

    [Fact]
    public void DateTimeNow_ShouldBeUnchanged()
    {
        // R217-05: the new DTO emitter must not replace the existing DateTime SQL.
        using var ctx = SqlServerTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTime.Now)).Should().Contain("getdate() as [Now]");
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTime.UtcNow)).Should().Contain("getutcdate()");
    }

    [Fact]
    public void RepeatedPreparation_ShouldNotChangeSql()
    {
        // C14: the DTO translation must not leak command/cache state between preparations.
        using var ctx = SqlServerTestContext.Create();
        var first = SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(1) }));
        var second = SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(1) }));

        second.Should().Be(first);
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTime.Now)).Should().Contain("getdate()");
    }
}
