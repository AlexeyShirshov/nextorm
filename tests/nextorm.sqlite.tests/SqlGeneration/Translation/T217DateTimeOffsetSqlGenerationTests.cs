using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

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
/// T217 (#217) SQLite SQL-generation coverage for the server-side <see cref="DateTimeOffset"/>
/// translation. SQLite stores a <see cref="DateTimeOffset"/> as UTC text with an explicit offset;
/// the value is never captured from the client clock, unsupported members fail closed and the
/// existing <see cref="DateTime"/> SQL is unchanged.
/// </summary>
public class T217DateTimeOffsetSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void DtoNow_ShouldBeUtcStrftime()
    {
        using var ctx = SqliteTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.Now))
            .Should().Contain("strftime('%Y-%m-%d %H:%M:%f','now') || '+00:00' as 'Now'");
    }

    [Fact]
    public void DtoUtcNow_ShouldBeUtcStrftime()
    {
        using var ctx = SqliteTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.UtcNow))
            .Should().Contain("strftime('%Y-%m-%d %H:%M:%f','now') || '+00:00' as 'UtcNow'");
    }

    [Theory]
    [InlineData("Year", "cast(strftime('%Y', value) as integer)")]
    [InlineData("Month", "cast(strftime('%m', value) as integer)")]
    [InlineData("Day", "cast(strftime('%d', value) as integer)")]
    [InlineData("Hour", "cast(strftime('%H', value) as integer)")]
    [InlineData("Minute", "cast(strftime('%M', value) as integer)")]
    [InlineData("Second", "cast(strftime('%S', value) as integer)")]
    [InlineData("DayOfYear", "cast(strftime('%j', value) as integer)")]
    public void DtoPart_ShouldUseStrftime(string member, string expected)
    {
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Maybe!.Value.Year }))
            .Should().Contain("cast(strftime('%Y', maybe) as integer)");
    }

    [Theory]
    [InlineData("AddYears", "datetime(value, (1) || ' years')")]
    [InlineData("AddMonths", "datetime(value, (1) || ' months')")]
    [InlineData("AddDays", "datetime(value, (1) || ' days')")]
    [InlineData("AddHours", "datetime(value, (1) || ' hours')")]
    [InlineData("AddMinutes", "datetime(value, (1) || ' minutes')")]
    [InlineData("AddSeconds", "datetime(value, (1) || ' seconds')")]
    [InlineData("AddMilliseconds", "strftime('%Y-%m-%d %H:%M:%f', value, ((1) / 1000.0) || ' seconds')")]
    public void DtoAdd_ShouldUseDateModifier(string member, string expected)
    {
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(0.5) }))
            .Should().Contain("datetime(value, (0.5) || ' days')");
    }

    [Fact]
    public void DtoPart_ShouldBeTranslatedInWhereAndOrderBy()
    {
        using var ctx = SqliteTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Where(x => x.Value.Year == 2023).Select(x => new { x.Id }))
            .Should().Contain("where cast(strftime('%Y', value) as integer) = 2023");
        SqlOf(ctx, ctx.From<IT217DtoEntity>().OrderBy(x => x.Value.Year).Select(x => new { x.Id }))
            .Should().Contain("order by cast(strftime('%Y', value) as integer)");
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();
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
        using var ctx = SqliteTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Offset.TotalMinutes }));

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset.Offset*");
    }

    [Fact]
    public void DateTimeNow_ShouldBeUnchanged()
    {
        // R217-05: the new DTO emitter must not replace the existing DateTime SQL.
        using var ctx = SqliteTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTime.Now)).Should().Contain("datetime('now') as 'Now'");
    }

    [Fact]
    public void RepeatedPreparation_ShouldNotChangeSql()
    {
        // C14: the DTO translation must not leak command/cache state between preparations.
        using var ctx = SqliteTestContext.Create();
        var first = SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(1) }));
        var second = SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(1) }));

        second.Should().Be(first);
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTime.Now)).Should().Contain("datetime('now')");
    }
}
