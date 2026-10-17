using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

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
/// T217 (#217) core coverage: the fail-closed guards for unsupported DateTimeOffset members, the
/// capability default/opt-in contract and the SQL shape of the supported DateTimeOffset operations on
/// the dialects referenced by this project. The behavioural/container coverage lives in the
/// provider and integration test projects.
/// </summary>
public class T217DateTimeOffsetTranslationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    // A dialect that does not opt into DateTimeOffset translation (the safe default).
    private sealed class NoDtoDialect : SqlDialectBase
    {
        internal static readonly NoDtoDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class NoDtoContext : DataContext
    {
        public NoDtoContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => NoDtoDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    /// <summary>
    /// The capability is a default interface method (<c>false</c>) so an external <see cref="ISqlDialect"/>
    /// implementation written before the member existed keeps compiling with the safe default.
    /// </summary>
    [Fact]
    public void SupportsDateTimeOffsetTranslation_ShouldBeADefaultInterfaceMethod()
    {
        var getter = typeof(ISqlDialect)
            .GetProperty(nameof(ISqlDialect.SupportsDateTimeOffsetTranslation))!
            .GetGetMethod()!;

        getter.IsAbstract.Should().BeFalse();
        getter.IsVirtual.Should().BeTrue();
        typeof(ISqlDialect).GetProperty(nameof(ISqlDialect.SupportsDateTimeOffsetTranslation))!
            .GetValue(NoDtoDialect.Instance).Should().Be(false);
    }

    [Fact]
    public void BuiltInProviders_ShouldOptIn()
    {
        new NoDtoDialect().SupportsDateTimeOffsetTranslation.Should().BeFalse();
        new PostgresDataContext("Host=localhost", new DataContextBuilder()).Dialect.SupportsDateTimeOffsetTranslation.Should().BeTrue();
        new SqlServerDataContext("Server=localhost", new DataContextBuilder()).Dialect.SupportsDateTimeOffsetTranslation.Should().BeTrue();
        new ClickHouseDataContext("Host=localhost", new DataContextBuilder()).Dialect.SupportsDateTimeOffsetTranslation.Should().BeTrue();
    }

    [Fact]
    public void Now_OnNonSupportingDialect_ShouldThrowBeforeSql()
    {
        using var ctx = new NoDtoContext();
        var cmd = ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.Now);

        var act = () => SqlOf(ctx, cmd);

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset*");
    }

    [Fact]
    public void Parts_OnNonSupportingDialect_ShouldThrowBeforeSql()
    {
        using var ctx = new NoDtoContext();
        var cmd = ctx.From<IT217DtoEntity>().Select(x => new { x.Value.Year });

        var act = () => SqlOf(ctx, cmd);

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset*");
    }

    [Fact]
    public void Add_OnNonSupportingDialect_ShouldThrowBeforeSql()
    {
        using var ctx = new NoDtoContext();
        var cmd = ctx.From<IT217DtoEntity>().Select(x => x.Value.AddDays(1));

        var act = () => SqlOf(ctx, cmd);

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset*");
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
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
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

    [Fact]
    public void NestedUnsupportedMember_ShouldThrowBeforeSql()
    {
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        var cmd = ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Offset.TotalMinutes });

        var act = () => SqlOf(ctx, cmd);

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset.Offset*");
    }

    [Fact]
    public void NestedUnsupportedDateTimeMember_ShouldThrowBeforeSql()
    {
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        var cmd = ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Date.AddDays(1) });

        var act = () => SqlOf(ctx, cmd);

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset.Date*");
    }

    [Theory]
    [InlineData("ToOffset")]
    [InlineData("AddTicks")]
    [InlineData("Add")]
    public void UnsupportedMethods_ShouldThrowBeforeSql(string method)
    {
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        var cmd = method switch
        {
            "ToOffset" => ctx.From<IT217DtoEntity>().Select(x => x.Value.ToOffset(TimeSpan.Zero)),
            "AddTicks" => ctx.From<IT217DtoEntity>().Select(x => x.Value.AddTicks(5)),
            _ => ctx.From<IT217DtoEntity>().Select(x => x.Value.Add(TimeSpan.FromDays(1))),
        };

        var act = () => SqlOf(ctx, cmd);

        act.Should().Throw<NotSupportedException>().WithMessage($"*DateTimeOffset.{method}*");
    }

    [Theory]
    [InlineData("ToUnixTimeSeconds")]
    [InlineData("ToUnixTimeMilliseconds")]
    [InlineData("ToLocalTime")]
    [InlineData("ToUniversalTime")]
    public void UnsupportedZeroArgumentMethods_ShouldThrowBeforeSql(string method)
    {
        // C6: a zero-argument DateTimeOffset method must not bypass the single-argument guard and fall
        // through to a silent client fold (which would drop the call or capture the client clock).
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        Func<string> act = method switch
        {
            "ToUnixTimeSeconds" => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.ToUnixTimeSeconds() })),
            "ToUnixTimeMilliseconds" => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.ToUnixTimeMilliseconds() })),
            "ToLocalTime" => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.ToLocalTime() })),
            _ => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.ToUniversalTime() })),
        };

        act.Should().Throw<NotSupportedException>().WithMessage($"*DateTimeOffset.{method}*");
    }

    [Theory]
    [InlineData("Offset")]
    [InlineData("Date")]
    public void UnsupportedMemberAsClientMethodReceiver_ShouldFailClosed(string member)
    {
        // C7: an unsupported DTO member nested as the receiver of a client-side method call must still
        // fail closed rather than being silently evaluated on the client.
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        Func<string> act = member switch
        {
            "Offset" => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Offset.ToString() })),
            _ => () => SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.Date.ToShortDateString() })),
        };

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UnsupportedMember_ConstantSource_ShouldThrowBeforeFolding()
    {
        // A literal/constant DateTimeOffset receiver must not be constant-folded on the client.
        var value = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        var cmd = ctx.From<IT217DtoEntity>().Select(_ => value.Offset);

        var act = () => SqlOf(ctx, cmd);

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset.Offset*");
    }

    [Fact]
    public void Postgres_DtoNow_ShouldBeServerNow()
    {
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.UtcNow))
            .Should().Contain("now()");
    }

    [Fact]
    public void Postgres_DtoParts_ShouldNormalizeToUtc()
    {
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Where(x => x.Value.Year == 2023).Select(x => new { x.Id }))
            .Should().Contain("extract(year from (value at time zone 'UTC'))");
    }

    [Fact]
    public void Postgres_DtoAdd_ShouldReturnTimestamptz()
    {
        using var ctx = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(1) }))
            .Should().Contain("((value at time zone 'UTC') + (1 * interval '1 day')) at time zone 'UTC'");
    }

    [Fact]
    public void SqlServer_DtoNow_ShouldUseNativeDatetimeoffset()
    {
        using var ctx = new SqlServerDataContext("Server=localhost", new DataContextBuilder());
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.Now))
            .Should().Contain("sysdatetimeoffset()");
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.UtcNow))
            .Should().Contain("todatetimeoffset(sysutcdatetime(), '+00:00')");
    }

    [Fact]
    public void SqlServer_DtoAddFractionalDay_ShouldScaleToFinerUnit()
    {
        using var ctx = new SqlServerDataContext("Server=localhost", new DataContextBuilder());
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { V = x.Value.AddDays(0.5) }))
            .Should().Contain("dateadd(hour, (0.5) * 24, value)");
    }

    [Fact]
    public void ClickHouse_DtoNow_ShouldBeUtcNow64()
    {
        using var ctx = new ClickHouseDataContext("Host=localhost", new DataContextBuilder());
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(_ => DateTimeOffset.Now))
            .Should().Contain("now64(6, 'UTC')");
    }

    [Fact]
    public void ClickHouse_DtoParts_ShouldNormalizeToUtc()
    {
        using var ctx = new ClickHouseDataContext("Host=localhost", new DataContextBuilder());
        SqlOf(ctx, ctx.From<IT217DtoEntity>().Select(x => new { x.Value.Year }))
            .Should().Contain("toInt32(toYear(toTimeZone(value, 'UTC')))");
    }

    [Fact]
    public void DateTimeOperations_ShouldBeUnchanged()
    {
        // R217-05: the existing DateTime SQL is unchanged (old MakeNow is not replaced by the DTO emitter).
        using var pg = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        SqlOf(pg, pg.From<IT217DtoEntity>().Select(_ => DateTime.Now)).Should().Contain("now()");
        using var ss = new SqlServerDataContext("Server=localhost", new DataContextBuilder());
        SqlOf(ss, ss.From<IT217DtoEntity>().Select(_ => DateTime.Now)).Should().Contain("getdate()");
        SqlOf(ss, ss.From<IT217DtoEntity>().Select(_ => DateTime.UtcNow)).Should().Contain("getutcdate()");
    }
}
