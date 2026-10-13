using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

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
/// T217 (#217) ClickHouse SQL-generation coverage for the server-side <see cref="DateTimeOffset"/>
/// translation, plus the provider-local DTO reader adaptation. ClickHouse stores a
/// <see cref="DateTimeOffset"/> as a UTC-normalized <c>DateTime64</c> (no preserved offset), so the
/// emitters normalise to UTC explicitly and a materialized value canonicalises to +00:00.
/// </summary>
public class T217DateTimeOffsetSqlGenerationTests
{
    private static readonly ParameterExpression Record = Expression.Parameter(typeof(IDataRecord), "record");

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void DtoNow_ShouldBeUtcNow64()
    {
        using var ctx = ClickHouseTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.Now)).Should().Contain("now64(6, 'UTC') as `Now`");
    }

    [Fact]
    public void DtoUtcNow_ShouldBeUtcNow64()
    {
        using var ctx = ClickHouseTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.UtcNow)).Should().Contain("now64(6, 'UTC') as `UtcNow`");
    }

    [Theory]
    [InlineData("Year", "toInt32(toYear(toTimeZone(value, 'UTC')))")]
    [InlineData("Month", "toInt32(toMonth(toTimeZone(value, 'UTC')))")]
    [InlineData("Day", "toInt32(toDayOfMonth(toTimeZone(value, 'UTC')))")]
    [InlineData("Hour", "toInt32(toHour(toTimeZone(value, 'UTC')))")]
    [InlineData("Minute", "toInt32(toMinute(toTimeZone(value, 'UTC')))")]
    [InlineData("Second", "toInt32(toSecond(toTimeZone(value, 'UTC')))")]
    [InlineData("DayOfYear", "toInt32(toDayOfYear(toTimeZone(value, 'UTC')))")]
    public void DtoPart_ShouldNormalizeToUtc(string member, string expected)
    {
        using var ctx = ClickHouseTestContext.Create();
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
        using var ctx = ClickHouseTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Maybe!.Value.Year }))
            .Should().Contain("toInt32(toYear(toTimeZone(maybe, 'UTC')))");
    }

    [Theory]
    [InlineData("AddYears", "addYears(toTimeZone(value, 'UTC'), 1)")]
    [InlineData("AddMonths", "addMonths(toTimeZone(value, 'UTC'), 1)")]
    [InlineData("AddDays", "addMicroseconds(toTimeZone(value, 'UTC'), (1) * 86400000000)")]
    [InlineData("AddHours", "addMicroseconds(toTimeZone(value, 'UTC'), (1) * 3600000000)")]
    [InlineData("AddMinutes", "addMicroseconds(toTimeZone(value, 'UTC'), (1) * 60000000)")]
    [InlineData("AddSeconds", "addMicroseconds(toTimeZone(value, 'UTC'), (1) * 1000000)")]
    [InlineData("AddMilliseconds", "addMicroseconds(toTimeZone(value, 'UTC'), (1) * 1000)")]
    public void DtoAdd_ShouldScaleToMicroseconds(string member, string expected)
    {
        using var ctx = ClickHouseTestContext.Create();
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
        // R217-03: ClickHouse addDays truncates a fractional day to a no-op, so the DTO emitter scales
        // it to addMicroseconds; 0.5 day must stay 12h (not vanish), to DateTime64(6) precision.
        using var ctx = ClickHouseTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(0.5) }))
            .Should().Contain("addMicroseconds(toTimeZone(value, 'UTC'), (0.5) * 86400000000)");

        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddHours(0.5) }))
            .Should().Contain("addMicroseconds(toTimeZone(value, 'UTC'), (0.5) * 3600000000)");
    }

    [Fact]
    public void DtoPart_ShouldBeTranslatedInWhereAndOrderBy()
    {
        using var ctx = ClickHouseTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Where(x => x.Value.Year == 2023).Select(x => new { x.Id }))
            .Should().Contain("where toInt32(toYear(toTimeZone(value, 'UTC'))) = 2023");
        SqlOf(ctx, ctx.From<IT217DtoEntity>().OrderBy(x => x.Value.Year).Select(x => new { x.Id }))
            .Should().Contain("order by toInt32(toYear(toTimeZone(value, 'UTC')))");
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
        using var ctx = ClickHouseTestContext.Create();
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
        using var ctx = ClickHouseTestContext.Create();
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
        using var ctx = ClickHouseTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Offset.TotalMinutes }));

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset.Offset*");
    }

    [Fact]
    public void DateTimeNow_ShouldBeUnchanged()
    {
        // R217-05: the new DTO emitter must not replace the existing DateTime SQL.
        using var ctx = ClickHouseTestContext.Create();
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTime.Now)).Should().Contain("now() as `Now`");
    }

    [Fact]
    public void RepeatedPreparation_ShouldNotChangeSql()
    {
        // C14: the DTO translation must not leak command/cache state between preparations.
        using var ctx = ClickHouseTestContext.Create();
        var first = SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(1) }));
        var second = SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(1) }));

        second.Should().Be(first);
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTime.Now)).Should().Contain("now()");
    }

    [Fact]
    public void DtoScalarProjection_ShouldDispatchToDtoReader_AndMaterializeOffsetZeroWithMicros()
    {
        // C15/C16: the ClickHouse DTO reader adaptation must fire for a DateTimeOffset projection. The
        // driver surfaces DateTime64 as System.DateTime, so the accessor must read a DateTime and
        // reinterpret it as an offset-zero DateTimeOffset without losing the stored microseconds.
        var micros = new DateTime(2023, 5, 17, 10, 0, 0, DateTimeKind.Utc).AddTicks(4560);
        var column = new SelectExpression(typeof(DateTimeOffset)) { Index = 0, PropertyName = "Value" };

        var body = ClickHouseTestContext.CreateClickHouse().MapColumnExpression(column, Record);

        body.Type.Should().Be(typeof(DateTimeOffset));
        body.Should().BeAssignableTo<MethodCallExpression>().Subject.Method.Name.Should().Be("ToDateTimeOffset");

        var result = (DateTimeOffset)Invoke(body, micros)!;
        result.Offset.Should().Be(TimeSpan.Zero);
        result.Microsecond.Should().Be(456);
        result.UtcDateTime.Should().Be(micros);
    }

    [Fact]
    public void DtoScalarProjection_ValueKindUnspecified_ShouldCanonicaliseToUtc()
    {
        var column = new SelectExpression(typeof(DateTimeOffset)) { Index = 0, PropertyName = "Value" };
        var body = ClickHouseTestContext.CreateClickHouse().MapColumnExpression(column, Record);
        var unspecified = new DateTime(2023, 5, 17, 10, 0, 0, DateTimeKind.Unspecified);

        var result = (DateTimeOffset)Invoke(body, unspecified)!;

        result.Offset.Should().Be(TimeSpan.Zero);
        result.UtcDateTime.Should().Be(unspecified);
    }

    [Fact]
    public void NullableDtoScalarProjection_SqlNull_ShouldMaterializeNull()
    {
        var column = new SelectExpression(typeof(DateTimeOffset?)) { Index = 0, PropertyName = "Value" };
        var body = ClickHouseTestContext.CreateClickHouse().MapColumnExpression(column, Record);

        body.Type.Should().Be(typeof(DateTimeOffset?));
        Invoke(body, DBNull.Value).Should().BeNull();
    }

    private static object? Invoke(Expression body, object? value)
    {
        var table = new DataTable();
        table.Columns.Add("Value", typeof(DateTime));
        table.Rows.Add(value ?? DBNull.Value);
        using var reader = table.CreateDataReader();
        reader.Read();

        var lambda = Expression.Lambda(body, Record).Compile();
        return lambda.DynamicInvoke(reader);
    }
}
