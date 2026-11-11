using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse residual branch coverage (cycle 8, unit D5) executed against a live Testcontainers
/// server. Every fact owns the <c>ch8_*</c> objects created in the constructor and asserts the exact
/// values the server returns.
///
/// <para>
/// Targeted arms in <c>src/nextorm.clickhouse/ClickHouseDialect.cs</c>: the public
/// <c>SAMPLE</c> operation (<see cref="ISqlDialect.SupportsSample"/>, :494) over a class-owned
/// <c>MergeTree</c> with a sampling key, <c>MakeCreateTableAsSelect</c> (:115-120),
/// <c>MakeConcat</c> (:123), <c>MakeColumnReference</c>/<c>MakeParam</c>/<c>MakeCoalesce</c>/
/// <c>MakeBool</c> (:546-555), <c>MakeDateTrunc</c> (:673-685, including the unsupported
/// <c>decade</c>/<c>century</c>/<c>millennium</c> rejection), the full <c>MakeDateAdd</c> field table
/// (:691-713), the <c>DateTimeOffset</c> scaled <c>MakeDateAdd</c> overload (:738-757),
/// <c>MakeDateDiff</c>/<c>MakeDateDiffBig</c> (:759-767), <c>MakeEndOfMonth</c> (:791), <c>MakeDateFromParts</c>
/// (:794-795), <c>MakeStringAgg</c>/<c>MakeFilteredStringAgg</c> (:798-803) and
/// <c>MakeNullableDurationType</c>/<c>MakeDurationType</c> (:822-826).
/// </para>
///
/// <para>
/// Overlap avoided: <c>ClickHouse/ClickHouseDialectBranchCoverage3Tests</c> owns the aggregate/date-part
/// matrix, <c>ClickHouse/ClickHouseIntegrationTests</c> owns the basic date-add/trunc forms and the
/// join-keyword matrix, and <c>ClickHouse/PlanKeyBranchCoverage5Tests</c> owns the plan-key comparison
/// of SAMPLE. This class adds the direct DDL/sampling route, the CTAS head and the residual
/// date/type/string block. Join-keyword and grouping/totals fallbacks were not needed.
/// </para>
/// </summary>
[Collection("ClickHouse")]
public sealed class Cycle8ClickHouseResidualTests : ProviderTestSuite
{
    private const string TableName = "ch8_residual";
    private const string CtasName = "ch8_ctas";

    public Cycle8ClickHouseResidualTests()
    {
        // ClickHouse rejects multi-statement commands, so each DDL/DML statement runs on its own command.
        foreach (var statement in Statements())
            Execute(statement);
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private static IReadOnlyList<string> Statements() =>
    [
        $"drop table if exists {CtasName}",
        $"drop table if exists {TableName}",
        """
        create table ch8_residual
        (
            id Int32,
            sid UInt32,
            grp Int32,
            label String,
            nullable_label Nullable(String),
            created DateTime,
            created64 DateTime64(3),
            dto DateTime64(6, 'UTC')
        )
        engine = MergeTree order by (sid, id) sample by sid
        """,
        """
        insert into ch8_residual (id, sid, grp, label, nullable_label, created, created64, dto) values
            (1, 1, 1, 'alpha', NULL, '2023-01-31 10:30:15', '2023-01-31 10:30:15.250', '2023-01-31 10:30:15.123456'),
            (2, 2, 1, 'beta', 'bee', '2023-02-28 23:59:59', '2023-02-28 23:59:59.750', '2023-02-28 23:59:59.654321'),
            (3, 3, 2, 'gamma', NULL, '2023-03-15 00:00:00', '2023-03-15 00:00:00.001', '2023-03-15 00:00:00.000001'),
            (4, 4, 2, 'delta', 'dee', '2023-12-31 12:00:00', '2023-12-31 12:00:00.500', '2023-12-31 12:00:00.500500')
        """,
    ];

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private string SqlOf<T>(QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)Context.GetPreparedQueryCommand(
                command, createEnumerator: false, storeInCache: false, TestContext.Current.CancellationToken))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    // ---- SAMPLE (:494) -----------------------------------------------------------------------

    /// <summary>
    /// A ratio of <c>1.0</c> over the sampling key reads every seeded row and the rendered statement
    /// carries the <c>sample</c> modifier; the dialect flag is the capability gate the public route
    /// consults.
    /// </summary>
    [Fact]
    public void Sample_FullRatio_ShouldReadEveryRowAndRenderTheModifier()
    {
        var dialect = ((DataContext)Context).Dialect;
        dialect.SupportsSample.Should().BeTrue("ClickHouse implements the SAMPLE table modifier");

        var command = Context.From<Ch8Row>(o => o.Sample(1.0))
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        SqlOf(command).Should().ContainEquivalentOf("sample");

        var ids = command.ToList();
        ids.Should().Equal(new[] { 1, 2, 3, 4 }, "a ratio of 1.0 keeps every seeded row");
    }

    /// <summary>
    /// A sub-unit ratio (with and without an offset) can only return rows of the seeded table, never a
    /// probabilistic count. ClickHouse rejects an offset together with a full sample, so the ratio stays
    /// below 1 here.
    /// </summary>
    [Fact]
    public void Sample_FractionalRatioAndOffset_ShouldReturnOnlySeededRows()
    {
        var seeded = new[] { 1, 2, 3, 4 };

        var half = Context.From<Ch8Row>(o => o.Sample(0.5))
            .Select(x => x.Id)
            .ToList();
        half.Should().BeSubsetOf(seeded);

        var withOffset = Context.From<Ch8Row>(o => o.Sample(0.5, 0.1))
            .Select(x => x.Id)
            .ToList();
        withOffset.Should().BeSubsetOf(seeded);
    }

    // ---- CREATE TABLE AS SELECT (:115-120) ---------------------------------------------------

    /// <summary>
    /// The persistent <c>ToTable</c> form renders the ClickHouse head
    /// <c>engine = MergeTree order by tuple() as</c>, materialises the projection server-side and the
    /// created table is readable through the raw-table source.
    /// </summary>
    [Fact]
    public void CreateTableAsSelect_ShouldMaterializeAndReadBack()
    {
        var sql = Context.From<Ch8Row>()
            .Where(x => x.Id <= 2)
            .Select(x => new { x.Id, x.Label })
            .ToTableSql(CtasName);

        sql.Should().Contain("engine = MergeTree order by tuple() as",
            "the ClickHouse dialect requires an explicit engine on CREATE TABLE AS SELECT");

        try
        {
            Context.From<Ch8Row>()
                .Where(x => x.Id <= 2)
                .Select(x => new { x.Id, x.Label })
                .ToTable(CtasName);

            var rows = Context.From(CtasName)
                .Select(t => new { Id = t.GetInt32("id"), Label = t.GetString("label") })
                .ToList();

            rows.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1, 2 });
            rows.Select(r => r.Label).Should().BeEquivalentTo(new[] { "alpha", "beta" });
        }
        finally
        {
            Execute($"drop table if exists {CtasName}");
        }
    }

    // ---- concat (:123) and column/param/coalesce/bool (:546-555) ------------------------------

    /// <summary>
    /// Both the C# <c>+</c> operator and <c>string.Concat</c> lower to <c>concat(...)</c>; the server
    /// returns the concatenated values.
    /// </summary>
    [Fact]
    public void Concat_ShouldUseTheConcatFunction()
    {
        var rows = Context.From<Ch8Row>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Plus = x.Label + "!",
                Concat = string.Concat(x.Label, "?"),
            })
            .ToList();

        rows.Select(r => r.Plus).Should().Equal("alpha!", "beta!", "gamma!", "delta!");
        rows.Select(r => r.Concat).Should().Equal("alpha?", "beta?", "gamma?", "delta?");
    }

    /// <summary>
    /// A column reference, a captured parameter, <c>??</c> (<c>MakeCoalesce</c>) and a comparison
    /// (<c>MakeBool</c>) are all read back from the server; the parameter narrows the row set, so a
    /// broken bind cannot silently return the whole table.
    /// </summary>
    [Fact]
    public void ColumnReferenceParamCoalesceAndBool_ShouldMaterialize()
    {
        const int minId = 3;

        var rows = Context.From<Ch8Row>()
            .Where(x => x.Id >= minId)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Ref = x.Label,
                P = x.NullableLabel ?? "z",
                B = x.Id > 3,
            })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Ref).Should().Equal("gamma", "delta");
        rows.Select(r => r.P).Should().Equal("z", "dee");
        rows.Select(r => r.B).Should().Equal(false, true);
    }

    // ---- date_trunc (:673-685) ---------------------------------------------------------------

    /// <summary>
    /// <c>day</c> truncates to midnight; the plural sub-second names fold onto the singular ClickHouse
    /// units and keep the stored fraction. <c>decade</c>/<c>century</c>/<c>millennium</c> are rejected
    /// while rendering, before any SQL reaches the server.
    /// </summary>
    [Fact]
    public void DateTrunc_ShouldTruncateAndRejectUnsupportedFields()
    {
        var row = Context.From<Ch8Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Day = SqlFunctions.Sql.date_trunc("day", x.Created),
                Milli = SqlFunctions.Sql.date_trunc("milliseconds", x.Created64),
                Micro = SqlFunctions.Sql.date_trunc("microseconds", x.Created64),
            })
            .Single();

        row.Day.Should().Be(new DateTime(2023, 1, 31));
        row.Milli.Should().Be(new DateTime(2023, 1, 31, 10, 30, 15, 250));
        row.Micro.Should().Be(new DateTime(2023, 1, 31, 10, 30, 15, 250));

        var decade = () => Context.From<Ch8Row>()
            .Select(x => SqlFunctions.Sql.date_trunc("decade", x.Created))
            .ToList();
        decade.Should().Throw<NotSupportedException>().WithMessage("*decade*");

        var century = () => Context.From<Ch8Row>()
            .Select(x => SqlFunctions.Sql.date_trunc("century", x.Created))
            .ToList();
        century.Should().Throw<NotSupportedException>().WithMessage("*century*");

        var millennium = () => Context.From<Ch8Row>()
            .Select(x => SqlFunctions.Sql.date_trunc("millennium", x.Created))
            .ToList();
        millennium.Should().Throw<NotSupportedException>().WithMessage("*millennium*");
    }

    // ---- date_add (:691-713) -----------------------------------------------------------------

    /// <summary>
    /// The dedicated <c>addYears</c>/<c>addQuarters</c>/.../<c>addSeconds</c> family for every accepted
    /// field, including the <c>decade</c>/<c>century</c>/<c>millennium</c> year scaling and the
    /// <c>milliseconds</c>/<c>microseconds</c> promotion to <c>DateTime64</c>. An unknown field is
    /// rejected before execution.
    /// </summary>
    [Fact]
    public void DateAdd_ShouldShiftEverySupportedField()
    {
        var row = Context.From<Ch8Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Year = SqlFunctions.Sql.date_add("year", 1, x.Created),
                Quarter = SqlFunctions.Sql.date_add("quarter", 1, x.Created),
                Month = SqlFunctions.Sql.date_add("month", 1, x.Created),
                Week = SqlFunctions.Sql.date_add("week", 1, x.Created),
                Day = SqlFunctions.Sql.date_add("day", 1, x.Created),
                Hour = SqlFunctions.Sql.date_add("hour", 1, x.Created),
                Minute = SqlFunctions.Sql.date_add("minute", 1, x.Created),
                Second = SqlFunctions.Sql.date_add("second", 1, x.Created),
                Milli = SqlFunctions.Sql.date_add("milliseconds", 500, x.Created),
                Micro = SqlFunctions.Sql.date_add("microseconds", 500_000, x.Created),
                Decade = SqlFunctions.Sql.date_add("decade", 1, x.Created),
                Century = SqlFunctions.Sql.date_add("century", 1, new DateTime(2000, 1, 1)),
            })
            .Single();

        row.Year.Should().Be(new DateTime(2024, 1, 31, 10, 30, 15));
        row.Quarter.Should().Be(new DateTime(2023, 4, 30, 10, 30, 15));
        row.Month.Should().Be(new DateTime(2023, 2, 28, 10, 30, 15));
        row.Week.Should().Be(new DateTime(2023, 2, 7, 10, 30, 15));
        row.Day.Should().Be(new DateTime(2023, 2, 1, 10, 30, 15));
        row.Hour.Should().Be(new DateTime(2023, 1, 31, 11, 30, 15));
        row.Minute.Should().Be(new DateTime(2023, 1, 31, 10, 31, 15));
        row.Second.Should().Be(new DateTime(2023, 1, 31, 10, 30, 16));
        row.Milli.Should().Be(new DateTime(2023, 1, 31, 10, 30, 15, 500));
        row.Micro.Should().Be(new DateTime(2023, 1, 31, 10, 30, 15, 500));
        row.Decade.Should().Be(new DateTime(2033, 1, 31, 10, 30, 15));
        row.Century.Should().Be(new DateTime(2100, 1, 1));

        var act = () => Context.From<Ch8Row>()
            .Select(x => SqlFunctions.Sql.date_add("fortnight", 1, x.Created))
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*fortnight*");
    }

    /// <summary>
    /// A <c>DateTimeOffset</c> operand takes the scaled overload: day/hour/minute/second/millisecond
    /// amounts become <c>addMicroseconds</c> (so a fraction is not truncated), while a coarse unit with
    /// no factor falls back to the normalized <c>add*</c> form. The server returns offset-zero instants.
    /// </summary>
    [Fact]
    public void DateAddDateTimeOffset_ShouldScaleUnitsAndExecute()
    {
        var row = Context.From<Ch8Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Days = x.Dto.AddDays(2),
                Hours = x.Dto.AddHours(3),
                Minutes = x.Dto.AddMinutes(30),
                Seconds = x.Dto.AddSeconds(1),
                Millis = x.Dto.AddMilliseconds(500),
                Months = x.Dto.AddMonths(1),
            })
            .Single();

        row.Days.UtcDateTime.Should().BeCloseTo(new DateTime(2023, 2, 2, 10, 30, 15, 123, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));
        row.Hours.UtcDateTime.Should().BeCloseTo(new DateTime(2023, 1, 31, 13, 30, 15, 123, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));
        row.Minutes.UtcDateTime.Should().BeCloseTo(new DateTime(2023, 1, 31, 11, 0, 15, 123, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));
        row.Seconds.UtcDateTime.Should().BeCloseTo(new DateTime(2023, 1, 31, 10, 30, 16, 123, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));
        row.Millis.UtcDateTime.Should().BeCloseTo(new DateTime(2023, 1, 31, 10, 30, 15, 623, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));
        row.Months.UtcDateTime.Should().BeCloseTo(new DateTime(2023, 2, 28, 10, 30, 15, 123, DateTimeKind.Utc), TimeSpan.FromMilliseconds(1));

        row.Days.Offset.Should().Be(TimeSpan.Zero);
        row.Hours.Offset.Should().Be(TimeSpan.Zero);
    }

    // ---- date_diff (:760-770) ----------------------------------------------------------------

    /// <summary>
    /// Whole-day/year differences and the plural millisecond/microsecond folds all run as
    /// <c>dateDiff(unit, start, end)</c> and return the expected 64-bit numbers through
    /// <c>date_diff_big</c>. The 32-bit <c>date_diff</c> form is covered by
    /// <see cref="DateDiff_ShouldMaterializeNarrowResult"/> (D5-CH-DATE-DIFF-INT32).
    /// </summary>
    [Fact]
    public void DateDiff_ShouldCountUnits()
    {
        var row = Context.From<Ch8Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Days = SqlFunctions.Sql.date_diff_big("day", x.Created, SqlFunctions.Sql.date_add("day", 3, x.Created)),
                Years = SqlFunctions.Sql.date_diff_big("year", x.Created, SqlFunctions.Sql.date_add("year", 2, x.Created)),
                Millis = SqlFunctions.Sql.date_diff_big("milliseconds", x.Created, SqlFunctions.Sql.date_add("milliseconds", 500, x.Created)),
                Micros = SqlFunctions.Sql.date_diff_big("microseconds", x.Created64, SqlFunctions.Sql.date_add("microseconds", 500_000, x.Created64)),
            })
            .Single();

        row.Days.Should().Be(3);
        row.Years.Should().Be(2);
        row.Millis.Should().Be(500);
        row.Micros.Should().Be(500_000);
    }

    /// <summary>
    /// The narrow <c>date_diff</c> projects <c>int?</c>, but ClickHouse answers <c>dateDiff</c> with
    /// <c>Int64</c>, so before #255 the projection failed to materialize (D5-CH-DATE-DIFF-INT32). The
    /// renderer now wraps the narrow form in <c>toInt32</c>: zero, positive and negative in-range spans
    /// come back as non-null 32-bit numbers, null operands stay null, and the 64-bit
    /// <c>date_diff_big</c> still returns a span beyond <see cref="int"/> unchanged.
    /// </summary>
    [Fact]
    public void DateDiff_ShouldMaterializeNarrowResult()
    {
        var row = Context.From<Ch8Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Zero = SqlFunctions.Sql.date_diff("day", x.Created, x.Created),
                Days = SqlFunctions.Sql.date_diff("day", x.Created, SqlFunctions.Sql.date_add("day", 3, x.Created)),
                Backwards = SqlFunctions.Sql.date_diff("day", x.Created, SqlFunctions.Sql.date_add("day", -3, x.Created)),
                Hours = SqlFunctions.Sql.date_diff("hour", x.Created, SqlFunctions.Sql.date_add("hour", 5, x.Created)),
                NullStart = SqlFunctions.Sql.date_diff("day", null, x.Created),
                WideMicros = SqlFunctions.Sql.date_diff_big(
                    "microseconds",
                    x.Created,
                    SqlFunctions.Sql.date_add("second", 3600, x.Created)),
            })
            .Single();

        row.Zero.Should().NotBeNull().And.Be(0);
        row.Days.Should().NotBeNull().And.Be(3);
        row.Backwards.Should().NotBeNull().And.Be(-3);
        row.Hours.Should().NotBeNull().And.Be(5);
        row.NullStart.Should().BeNull("a null operand keeps the existing null semantics");
        // 3600 s in microseconds exceeds Int32: only the native 64-bit form may return it.
        row.WideMicros.Should().Be(3_600_000_000L);
    }

    // ---- end_of_month (:785) and date_from_parts (:788-789) ---------------------------------

    /// <summary><c>toLastDayOfMonth</c> keeps the day of the stored month and <c>makeDate</c> builds a date from parts.</summary>
    [Fact]
    public void EndOfMonthAndDateFromParts_ShouldReturnDates()
    {
        var row = Context.From<Ch8Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Eom = SqlFunctions.Sql.end_of_month(x.Created),
                FromParts = SqlFunctions.Sql.date_from_parts(2024, 2, 29),
            })
            .Single();

        row.Eom.Should().Be(new DateTime(2023, 1, 31));
        row.FromParts.Should().Be(new DateTime(2024, 2, 29));
    }

    // ---- string_agg (:792-797) ---------------------------------------------------------------

    /// <summary>
    /// <c>arrayStringConcat(groupArray(...))</c> concatenates the whole group and
    /// <c>groupArrayIf</c> applies the filtered predicate; the groupArray order is unspecified, so the
    /// split values are compared as a set.
    /// </summary>
    [Fact]
    public void StringAggAndFilteredStringAgg_ShouldAggregate()
    {
        var row = Context.From<Ch8Row>()
            .Where(x => x.Grp == 1)
            .Select(x => new
            {
                All = SqlFunctions.Sql.string_agg(x.Label, ","),
                Filtered = SqlFunctions.Sql.string_agg(x.Label, "|", () => x.Id > 1),
            })
            .Single();

        row.All.Should().NotBeNull();
        row.All!.Split(',').Should().BeEquivalentTo(new[] { "alpha", "beta" });

        row.Filtered.Should().NotBeNull();
        row.Filtered!.Split('|').Should().BeEquivalentTo(new[] { "beta" });
    }

    // ---- duration type (:816-820) ------------------------------------------------------------

    /// <summary>
    /// ClickHouse has no native duration type: the non-nullable form is <c>Int64</c> and the nullable
    /// form wraps it in <c>Nullable(...)</c>.
    /// </summary>
    [Fact]
    public void DurationType_ShouldRenderNullableInt64()
    {
        var dialect = ((DataContext)Context).Dialect;

        dialect.MakeDurationType(DurationUnit.Milliseconds).Should().Be("Int64");
        dialect.MakeNullableDurationType(DurationUnit.Milliseconds).Should().Be("Nullable(Int64)");
    }

    [SqlTable("ch8_residual")]
    public sealed class Ch8Row
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("sid")]
        public uint Sid { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;

        [Column("nullable_label")]
        public string? NullableLabel { get; set; }

        [Column("created")]
        public DateTime Created { get; set; }

        [Column("created64")]
        public DateTime Created64 { get; set; }

        [Column("dto")]
        public DateTimeOffset Dto { get; set; }
    }
}
