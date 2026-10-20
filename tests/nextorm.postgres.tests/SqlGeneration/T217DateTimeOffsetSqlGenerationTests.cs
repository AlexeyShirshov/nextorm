using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

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
/// T217 (#217) PostgreSQL SQL-generation coverage for the server-side <see cref="DateTimeOffset"/>
/// translation. PostgreSQL stores a <see cref="DateTimeOffset"/> as <c>timestamptz</c>; date parts and
/// arithmetic normalise to the UTC wall time so the result is independent of the session time zone
/// and DST, and the client clock is never captured.
/// </summary>
public class T217DateTimeOffsetSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void DtoNow_ShouldBeServerNow()
    {
        using var ctx = PostgresTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.Now)).Should().Contain("now() as \"Now\"");
    }

    [Fact]
    public void DtoUtcNow_ShouldBeServerNow()
    {
        using var ctx = PostgresTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.UtcNow)).Should().Contain("now() as \"UtcNow\"");
    }

    [Theory]
    [InlineData("Year", "extract(year from (value at time zone 'UTC'))")]
    [InlineData("Month", "extract(month from (value at time zone 'UTC'))")]
    [InlineData("Day", "extract(day from (value at time zone 'UTC'))")]
    [InlineData("Hour", "extract(hour from (value at time zone 'UTC'))")]
    [InlineData("Minute", "extract(minute from (value at time zone 'UTC'))")]
    [InlineData("Second", "extract(second from (value at time zone 'UTC'))")]
    [InlineData("DayOfYear", "extract(doy from (value at time zone 'UTC'))")]
    public void DtoPart_ShouldNormalizeToUtc(string member, string expected)
    {
        using var ctx = PostgresTestContext.Create();
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
    public void DtoNullablePart_ShouldNormalizeToUtc()
    {
        using var ctx = PostgresTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Maybe!.Value.Year }))
            .Should().Contain("extract(year from (maybe at time zone 'UTC'))");
    }

    [Theory]
    [InlineData("AddYears", "((value at time zone 'UTC') + (1 * interval '1 year')) at time zone 'UTC'")]
    [InlineData("AddMonths", "((value at time zone 'UTC') + (1 * interval '1 month')) at time zone 'UTC'")]
    [InlineData("AddDays", "((value at time zone 'UTC') + (1 * interval '1 day')) at time zone 'UTC'")]
    [InlineData("AddHours", "((value at time zone 'UTC') + (1 * interval '1 hour')) at time zone 'UTC'")]
    [InlineData("AddMinutes", "((value at time zone 'UTC') + (1 * interval '1 minute')) at time zone 'UTC'")]
    [InlineData("AddSeconds", "((value at time zone 'UTC') + (1 * interval '1 second')) at time zone 'UTC'")]
    [InlineData("AddMilliseconds", "((value at time zone 'UTC') + (1 * interval '1 milliseconds')) at time zone 'UTC'")]
    public void DtoAdd_ShouldReturnTimestamptz(string member, string expected)
    {
        using var ctx = PostgresTestContext.Create();
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
    public void DtoAddFractionalDay_ShouldKeepFraction()
    {
        using var ctx = PostgresTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(0.5) }))
            .Should().Contain("((value at time zone 'UTC') + (0.5 * interval '1 day')) at time zone 'UTC'");
    }

    [Fact]
    public void DtoPart_ShouldBeTranslatedInWhereAndOrderBy()
    {
        using var ctx = PostgresTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Where(x => x.Value.Year == 2023).Select(x => new { x.Id }))
            .Should().Contain("where extract(year from (value at time zone 'UTC')) = 2023");
        SqlOf(ctx, ctx.From<IT217DtoEntity>().OrderBy(x => x.Value.Year).Select(x => new { x.Id }))
            .Should().Contain("order by extract(year from (value at time zone 'UTC'))");
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
        using var ctx = PostgresTestContext.Create();
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
        using var ctx = PostgresTestContext.Create();
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
        using var ctx = PostgresTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Offset.TotalMinutes }));

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset.Offset*");
    }

    [Fact]
    public void DateTimeNow_ShouldBeUnchanged()
    {
        // R217-05: the new DTO emitter must not replace the existing DateTime SQL.
        using var ctx = PostgresTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTime.Now)).Should().Contain("now() as \"Now\"");
    }

    [Fact]
    public void RepeatedPreparation_ShouldNotChangeSql()
    {
        // C14: the DTO translation must not leak command/cache state between preparations.
        using var ctx = PostgresTestContext.Create();
        var first = SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(1) }));
        var second = SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(1) }));

        second.Should().Be(first);
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTime.Now)).Should().Contain("now()");
    }
}
