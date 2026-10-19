using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQL Server residual branch coverage (cycle 8, cluster D4) executed against a live Testcontainers
/// server. Every fact owns the <c>c8_ss_marker</c> table (created and seeded in the constructor) and
/// asserts the values the server returns.
///
/// <para>
/// Targeted arms in <c>src/nextorm.sqlserver/SqlServerDialect.cs</c>: <c>IndexHints</c> (the
/// index-hint renderer surface, with and without a merged table hint) and its index-ignore rejection
/// (<c>SqlServerIndexHintRenderer.RenderIndexHint</c>), the numeric <c>MakeTypeName</c> conversion
/// arms, <c>MakeDateTrunc</c>'s sub-second folds, <c>MakeDateAdd</c>'s
/// decade/century/millennium year scaling and the <c>DateTimeOffset</c> default field arm,
/// <c>TSqlDatePart</c>'s millisecond mapping, <c>MakeNow</c>'s local clock, the one-argument
/// <c>round</c> fold, <c>MakeCase</c>'s boolean-CASE materialisation (value and predicate contexts).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>Coverage6/SqlServerRenderingBranchCoverage6Tests</c>
/// owns the table-hint/index-hint merge on a joined source (<c>TableHintsWithIndex</c> both loops),
/// <c>SqlServer/SqlServerFunctionExecutionTests</c> owns the scalar/date/math/format library and the
/// limit-1 <c>WITH TIES</c> page, and <c>SqlServerPreparationDifferentialBranchCoverage4Tests</c>
/// owns its prepared-plan twin. This class adds the index-only hint route, the index-ignore
/// rejection and the typed/date residuals.
/// </para>
///
/// <para>
/// A live probe of the offset-bearing <c>WITH TIES</c> page (<c>Page(1, 1).WithTies()</c>) reproduced
/// tracked defect #298: SQL Server rejects the emitted <c>fetch next n rows with ties</c> with
/// "Incorrect syntax near the keyword 'with'", so that arm is recorded as an observation against the
/// existing issue instead of being asserted as a passing fact.
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class Cycle8SqlServerResidualTests : ProviderTestSuite
{
    private static readonly DateTime Ts = new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(1_234_567);
    private static readonly DateTime TsEnd = Ts.AddMilliseconds(500);
    private static readonly DateTimeOffset Dto =
        new DateTimeOffset(2023, 5, 17, 13, 45, 30, TimeSpan.FromHours(2)).AddTicks(1_234_567);

    private const string Ddl = """
        drop table if exists c8_ss_marker;
        create table c8_ss_marker
        (
            id int not null primary key,
            name nvarchar(50) not null,
            grp int not null,
            score int not null,
            flag bit not null,
            alt bit not null,
            ts datetime2 not null,
            ts_end datetime2 not null,
            dto datetimeoffset not null,
            amount decimal(10, 2) not null
        );
        insert into c8_ss_marker (id, name, grp, score, flag, alt, ts, ts_end, dto, amount) values
            (1, N'alpha', 1, 10, 1, 0, '2023-05-17T13:45:30.1234567', '2023-05-17T13:45:30.6234567', '2023-05-17T13:45:30.1234567+02:00', 12.34),
            (2, N'beta', 1, 20, 0, 1, '2023-05-17T13:45:30.1234567', '2023-05-17T13:45:30.6234567', '2023-05-17T13:45:30.1234567+02:00', 12.34),
            (3, N'gamma', 2, 20, 1, 1, '2023-05-17T13:45:30.1234567', '2023-05-17T13:45:30.6234567', '2023-05-17T13:45:30.1234567+02:00', 12.34),
            (4, N'delta', 2, 20, 0, 0, '2023-05-17T13:45:30.1234567', '2023-05-17T13:45:30.6234567', '2023-05-17T13:45:30.1234567+02:00', 12.34),
            (5, N'epsilon', 3, 30, 1, 0, '2023-05-17T13:45:30.1234567', '2023-05-17T13:45:30.6234567', '2023-05-17T13:45:30.1234567+02:00', 12.34);
        create index ix_c8_ss_grp on c8_ss_marker (grp);
        """;

    public Cycle8SqlServerResidualTests() => Execute(Ddl);

    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Index-hint arm: an index-only hint reaches <c>SqlServerIndexHintRenderer</c> without a table
    /// hint, and the folded <c>WITH (nolock, index(...))</c> form keeps the exact rows.
    /// </summary>
    [Fact]
    public void IndexHint_AloneAndMerged_ShouldKeepTheRows()
    {
        var indexOnly = Context.From<C8SsMarker>(o => o.WithIndex("ix_c8_ss_grp"))
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        indexOnly.Should().Equal(new[] { 1, 2 }, "the index hint does not change the matching rows");

        var merged = Context.From<C8SsMarker>(o =>
            {
                o.WithTableHint("nolock");
                o.WithIndex("ix_c8_ss_grp");
            })
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        merged.Should().Equal(new[] { 1, 2 }, "the merged table/index hint clause keeps the same rows");
    }

    /// <summary>
    /// Index-ignore rejection arm: the public <see cref="IndexHintKind.Ignore"/> form is rejected by
    /// the SQL Server renderer before any SQL reaches the server.
    /// </summary>
    [Fact]
    public void IndexIgnore_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => Context.From<C8SsMarker>(o => o.WithIndex(IndexHintKind.Ignore, "ix_c8_ss_grp"))
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*index-ignore*");
    }

    /// <summary>
    /// Numeric conversion arms of <c>MakeTypeName</c>: every cast round-trips the exact integer value.
    /// </summary>
    [Fact]
    public void NumericConversions_ShouldReturnTheConvertedValues()
    {
        var row = Context.From<C8SsMarker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Byte = (byte)x.Score,
                Short = (short)x.Score,
                Long = (long)x.Score,
                Float = (float)x.Score,
                Double = (double)x.Score,
                Decimal = (decimal)x.Score
            })
            .First();

        row.Byte.Should().Be((byte)10);
        row.Short.Should().Be((short)10);
        row.Long.Should().Be(10L);
        row.Float.Should().Be(10f);
        row.Double.Should().Be(10d);
        row.Decimal.Should().Be(10m);
    }

    /// <summary>
    /// <c>MakeDateTrunc</c> sub-second folds: the plural ANSI parts map onto the singular T-SQL parts
    /// and truncate exactly; the decade/century/millennium parts are rejected before rendering.
    /// </summary>
    [Fact]
    public void DateTrunc_SubSecondFields_ShouldTruncateExactly()
    {
        var row = Context.From<C8SsMarker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Micro = SqlFunctions.Sql.date_trunc("microseconds", x.Ts),
                Milli = SqlFunctions.Sql.date_trunc("milliseconds", x.Ts)
            })
            .First();

        row.Micro.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(1_234_560),
            "datetrunc(microsecond) drops the 100 ns digit");
        row.Milli.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(1_230_000),
            "datetrunc(millisecond) keeps three fractional digits");

        FluentActions
            .Invoking(() => Context.From<C8SsMarker>()
                .Select(x => SqlFunctions.Sql.date_trunc("millennium", x.Ts))
                .ToList())
            .Should().Throw<NotSupportedException>("SQL Server has no datetrunc millennium part");
    }

    /// <summary>
    /// <c>MakeDateAdd</c> year folds: decade/century/millennium become a scaled <c>dateadd(year, ...)</c>
    /// and the returned instants match the CLR arithmetic.
    /// </summary>
    [Fact]
    public void DateAdd_DecadeCenturyMillennium_ShouldScaleTheYears()
    {
        var row = Context.From<C8SsMarker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Decade = SqlFunctions.Sql.date_add("decade", 1, x.Ts),
                Century = SqlFunctions.Sql.date_add("century", 1, x.Ts),
                Millennium = SqlFunctions.Sql.date_add("millennium", 1, x.Ts)
            })
            .First();

        row.Decade.Should().Be(Ts.AddYears(10));
        row.Century.Should().Be(Ts.AddYears(100));
        row.Millennium.Should().Be(Ts.AddYears(1000));
    }

    /// <summary>
    /// <c>TSqlDatePart</c> millisecond fold: <c>date_diff</c> and <c>date_diff_big</c> count the
    /// boundaries between two instants and keep the argument order.
    /// </summary>
    [Fact]
    public void DateDiff_MillisecondAndMicrosecond_ShouldCountBoundaries()
    {
        var row = Context.From<C8SsMarker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Millis = SqlFunctions.Sql.date_diff("milliseconds", x.Ts, x.TsEnd),
                MillisReversed = SqlFunctions.Sql.date_diff("milliseconds", x.TsEnd, x.Ts),
                Micros = SqlFunctions.Sql.date_diff_big("microseconds", x.Ts, x.TsEnd)
            })
            .First();

        row.Millis.Should().Be(500);
        row.MillisReversed.Should().Be(-500);
        row.Micros.Should().Be(500_000L);
    }

    /// <summary>
    /// <c>DateTimeOffset</c> add arm: year/month amounts keep the source offset through the
    /// offset-aware <c>MakeDateAdd</c> default field branch.
    /// </summary>
    [Fact]
    public void DateTimeOffsetAdd_YearAndMonth_ShouldPreserveTheOffset()
    {
        var row = Context.From<C8SsMarker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Years = x.Dto.AddYears(1),
                Months = x.Dto.AddMonths(2)
            })
            .First();

        row.Years.Should().Be(Dto.AddYears(1));
        row.Years.Offset.Should().Be(TimeSpan.FromHours(2));
        row.Months.Should().Be(Dto.AddMonths(2));
        row.Months.Offset.Should().Be(TimeSpan.FromHours(2));
    }

    /// <summary>
    /// <c>MakeNow</c> local arm: <c>DateTime.Now</c> renders the server's local clock and
    /// <c>DateTime.UtcNow</c> the UTC clock, each matching its server-native twin.
    /// </summary>
    [Fact]
    public void NowAndUtcNow_ShouldReadTheServerClocks()
    {
        var row = Context.From<C8SsMarker>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Local = DateTime.Now,
                Utc = DateTime.UtcNow,
                ServerLocal = SqlFunctions.SqlServer.sysdatetime(),
                ServerUtc = SqlFunctions.SqlServer.sysutcdatetime()
            })
            .First();

        row.ServerLocal.Should().NotBeNull();
        row.ServerUtc.Should().NotBeNull();

        row.Local.Should().BeCloseTo(row.ServerLocal!.Value, TimeSpan.FromSeconds(5),
            "getdate() is the server's local clock");
        row.Utc.Should().BeCloseTo(row.ServerUtc!.Value, TimeSpan.FromSeconds(5),
            "getutcdate() is the server's UTC clock");
    }

    /// <summary>
    /// One-argument <c>round</c> arm: <c>Math.Round(value)</c> renders <c>round(value, 0)</c> and
    /// returns the rounded value.
    /// </summary>
    [Fact]
    public void RoundOneArgument_ShouldRoundToAnInteger()
    {
        var rounded = Context.From<C8SsMarker>()
            .Where(x => x.Id == 1)
            .Select(x => Math.Round(x.Amount))
            .First();

        rounded.Should().Be(12m);
    }

    /// <summary>
    /// Boolean-CASE arm: a boolean-valued conditional is materialised as a <c>bit</c> scalar in a
    /// projection and compared with <c>1</c> in a predicate context.
    /// </summary>
    [Fact]
    public void BooleanCase_ProjectionAndPredicate_ShouldMaterialiseABit()
    {
        var projected = Context.From<C8SsMarker>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Picked = x.Score > 20 ? x.Flag : x.Alt })
            .ToList();

        projected.Should().HaveCount(5);
        projected.Select(r => r.Picked).Should().Equal(new[] { false, true, true, false, true },
            "scores of 20 or less take the else branch (alt), the 30 row takes the flag");

        var filtered = Context.From<C8SsMarker>()
            .Where(x => x.Score > 20 ? x.Flag : x.Alt)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        filtered.Should().Equal(new[] { 2, 3, 5 }, "the predicate compares the materialised bit with 1");
    }

    [SqlTable("c8_ss_marker")]
    public sealed class C8SsMarker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }

        [Column("score")]
        public int Score { get; set; }

        [Column("flag")]
        public bool Flag { get; set; }

        [Column("alt")]
        public bool Alt { get; set; }

        [Column("ts")]
        public DateTime Ts { get; set; }

        [Column("ts_end")]
        public DateTime TsEnd { get; set; }

        [Column("dto")]
        public DateTimeOffset Dto { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }
    }
}
