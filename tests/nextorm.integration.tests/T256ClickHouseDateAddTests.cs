using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// T256 (#256) ClickHouse <c>date_add</c> year family (<c>year</c>/<c>decade</c>/<c>century</c>) executed
/// against a live 25.8 server. The dialect promotes the operand to <c>DateTime64(6)</c> because
/// <c>addYears</c> on a 4-byte <c>DateTime</c> wraps past the 2106 limit (<c>2023 + 100y</c> came back as
/// <c>1987-04-10 07:17:14</c>), while <c>DateTime64</c> reaches 2299. The class owns its
/// <c>t256_date_add</c> fixture so every operand kind of the plan's variant matrix is a real column, and
/// <c>millennium</c> must be refused during translation — before any command reaches the server.
/// </summary>
[Collection("ClickHouse")]
public sealed class T256ClickHouseDateAddTests : ProviderTestSuite
{
    private const string TableName = "t256_date_add";

    /// <summary>Whole-second parameter operand: the driver binds a CLR <c>DateTime</c> as 4-byte seconds.</summary>
    private static readonly DateTime Value = new(2023, 5, 17, 13, 45, 30);

    private static readonly DateTime Row1Us = new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(1_234_560);
    private static readonly DateTime Row1Ms = new DateTime(2023, 5, 17, 13, 45, 30, 123);

    private static readonly DateTime Boundary = new(2050, 1, 1);

    public T256ClickHouseDateAddTests()
    {
        foreach (var statement in FixtureStatements())
            Execute(statement);
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private DataContext Context => (DataContext)_sut.DataProvider;

    private static IReadOnlyList<string> FixtureStatements() =>
    [
        $"drop table if exists {TableName}",
        // `ns` only carries a row-1 value: DateTime64(9) is backed by a 64-bit count of nanoseconds,
        // which limits that precision to 1677-2262, so the 2100/2289 rows keep a valid filler there and
        // the scale-9 truncation case is asserted on row 1 only.
        $"""
        create table {TableName}
        (
            id Int32,
            ms DateTime64(3),
            us DateTime64(6),
            ns DateTime64(9),
            nms Nullable(DateTime64(3)),
            nus Nullable(DateTime64(6)),
            dto DateTime64(6, 'UTC')
        )
        engine = Memory
        """,
        $"""
        insert into {TableName} (id, ms, us, ns, nms, nus, dto) values
            (1, toDateTime64('2023-05-17 13:45:30.123', 3), toDateTime64('2023-05-17 13:45:30.123456', 6), toDateTime64('2023-05-17 13:45:30', 9) + INTERVAL 123456789 NANOSECOND, NULL, NULL, toDateTime64('2023-05-17 13:45:30.123456', 6, 'UTC')),
            (2, toDateTime64('2023-05-17 13:45:30.500', 3), toDateTime64('2023-05-17 13:45:30.654321', 6), toDateTime64('2023-05-17 13:45:30', 9) + INTERVAL 654321987 NANOSECOND, toDateTime64('2023-05-17 13:45:30.750', 3), toDateTime64('2023-05-17 13:45:30.987654', 6), toDateTime64('2023-05-17 13:45:30.654321', 6, 'UTC')),
            (3, toDateTime64('2100-06-15 12:00:00', 3), toDateTime64('2100-06-15 12:00:00', 6), toDateTime64('2023-05-17 13:45:30', 9), NULL, NULL, toDateTime64('2100-06-15 12:00:00', 6, 'UTC')),
            (4, toDateTime64('2289-02-03 04:05:06.007', 3), toDateTime64('2289-02-03 04:05:06.000007', 6), toDateTime64('2023-05-17 13:45:30', 9), NULL, NULL, toDateTime64('2289-02-03 04:05:06.000007', 6, 'UTC'))
        """
    ];

    private void Execute(string sql)
    {
        Context.EnsureConnectionOpen();
        using var command = Context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private DbPreparedQueryCommand<T> Prepare<T>(QueryCommand<T> command)
        => (DbPreparedQueryCommand<T>)Context.GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: false, TestContext.Current.CancellationToken);

    /// <summary>
    /// The issue's regression case: <c>2023-05-17 13:45:30 + 1 century = 2123-05-17 13:45:30</c>. On the
    /// unfixed 4-byte <c>DateTime</c> rendering this came back as <c>1987-04-10 07:17:14</c> (a 2^32-second
    /// wrap); the promoted <c>DateTime64(6)</c> operand returns the exact instant.
    /// </summary>
    [Fact]
    public void Century_2023_Adds100Years()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Sql.date_add("century", 1, Value) })
            .First();

        r.V.Should().Be(new DateTime(2123, 5, 17, 13, 45, 30));
    }

    /// <summary>
    /// The whole year-family matrix against a live server: <c>year</c>/<c>decade</c>/<c>century</c> ×
    /// (a <c>DateTime</c> parameter, a <c>DateTime64(3)</c> column, a <c>DateTime64(6)</c> column and a
    /// <c>DateTimeOffset</c>) × positive/negative/zero amounts. Every input and result stays inside the
    /// pinned 25.8 <c>DateTime64</c> range (1900–2299), so the expected instants are exact and a
    /// 4-byte wrap (a 2^32-second shift) cannot be mistaken for rounding.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(0)]
    public void YearFamily_AmountsAndOperandKinds(int amount)
    {
        var row = Context.From<T256Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                YearParameter = SqlFunctions.Sql.date_add("year", amount, Value),
                YearMs = SqlFunctions.Sql.date_add("year", amount, x.Ms),
                YearUs = SqlFunctions.Sql.date_add("year", amount, x.Us),
                YearDto = x.Dto.AddYears(amount),
                DecadeParameter = SqlFunctions.Sql.date_add("decade", amount, Value),
                DecadeMs = SqlFunctions.Sql.date_add("decade", amount, x.Ms),
                DecadeUs = SqlFunctions.Sql.date_add("decade", amount, x.Us),
                // A DateTimeOffset has no decade/century unit in the public surface; the scaled AddYears
                // form is the reachable equivalent and runs through the same year-family promotion.
                DecadeDto = x.Dto.AddYears(amount * 10),
                CenturyParameter = SqlFunctions.Sql.date_add("century", amount, Value),
                CenturyMs = SqlFunctions.Sql.date_add("century", amount, x.Ms),
                CenturyUs = SqlFunctions.Sql.date_add("century", amount, x.Us),
                CenturyDto = x.Dto.AddYears(amount * 100)
            })
            .Single();

        row.YearParameter.Should().Be(Value.AddYears(amount));
        row.YearMs.Should().Be(Row1Ms.AddYears(amount));
        row.YearUs.Should().Be(Row1Us.AddYears(amount));
        row.YearDto.UtcDateTime.Should().Be(Row1Us.AddYears(amount));
        row.YearDto.Offset.Should().Be(TimeSpan.Zero);

        row.DecadeParameter.Should().Be(Value.AddYears(amount * 10));
        row.DecadeMs.Should().Be(Row1Ms.AddYears(amount * 10));
        row.DecadeUs.Should().Be(Row1Us.AddYears(amount * 10));
        row.DecadeDto.UtcDateTime.Should().Be(Row1Us.AddYears(amount * 10));

        row.CenturyParameter.Should().Be(Value.AddYears(amount * 100));
        row.CenturyMs.Should().Be(Row1Ms.AddYears(amount * 100));
        row.CenturyUs.Should().Be(Row1Us.AddYears(amount * 100));
        row.CenturyDto.UtcDateTime.Should().Be(Row1Us.AddYears(amount * 100));
    }

    /// <summary>
    /// A typed-null operand propagates <c>NULL</c> through the promoted <c>addYears</c> for every year-family
    /// unit and for positive, negative and zero amounts: the nullable <c>DateTime64(3)</c>/<c>DateTime64(6)</c>
    /// columns return <c>null</c>, and the seeded non-null row keeps its exact shifted instant.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(0)]
    public void YearFamily_NullPropagates(int amount)
    {
        var rows = Context.From<T256Row>()
            .Where(x => x.Id == 1 || x.Id == 2)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                YearMs = SqlFunctions.Sql.date_add("year", amount, x.Nms),
                YearUs = SqlFunctions.Sql.date_add("year", amount, x.Nus),
                DecadeMs = SqlFunctions.Sql.date_add("decade", amount, x.Nms),
                DecadeUs = SqlFunctions.Sql.date_add("decade", amount, x.Nus),
                CenturyMs = SqlFunctions.Sql.date_add("century", amount, x.Nms),
                CenturyUs = SqlFunctions.Sql.date_add("century", amount, x.Nus)
            })
            .ToList();

        rows.Should().HaveCount(2);
        var nullRow = rows[0];
        nullRow.Id.Should().Be(1);
        nullRow.YearMs.Should().BeNull("a NULL DateTime64(3) operand stays NULL through the promotion");
        nullRow.YearUs.Should().BeNull("a NULL DateTime64(6) operand stays NULL through the promotion");
        nullRow.DecadeMs.Should().BeNull();
        nullRow.DecadeUs.Should().BeNull();
        nullRow.CenturyMs.Should().BeNull();
        nullRow.CenturyUs.Should().BeNull();

        var valueRow = rows[1];
        valueRow.Id.Should().Be(2);
        valueRow.YearMs.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30, 750).AddYears(amount));
        valueRow.YearUs.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(9_876_540).AddYears(amount));
        valueRow.DecadeMs.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30, 750).AddYears(amount * 10));
        valueRow.CenturyUs.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(9_876_540).AddYears(amount * 100));
    }

    /// <summary>
    /// Microsecond precision survives the promotion for the whole year family: a <c>DateTime64(6)</c>
    /// column keeps <c>.123456</c> and a <c>DateTime64(3)</c> column keeps <c>.123</c> across a century.
    /// A <c>DateTime64(9)</c> operand is normalized to scale 6, so its sub-microsecond tail
    /// (<c>.123456789</c> → <c>.123456</c>) is discarded by the documented scale-6 normalization.
    /// </summary>
    [Fact]
    public void YearFamily_PreservesMicroseconds()
    {
        var row = Context.From<T256Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                YearUs = SqlFunctions.Sql.date_add("year", 1, x.Us),
                CenturyUs = SqlFunctions.Sql.date_add("century", 1, x.Us),
                YearMs = SqlFunctions.Sql.date_add("year", 1, x.Ms),
                CenturyMs = SqlFunctions.Sql.date_add("century", 1, x.Ms),
                // DateTime64(9) input → toDateTime64(<v>, 6): the last three fractional digits are dropped.
                YearNsNormalizedToScale6 = SqlFunctions.Sql.date_add("year", 1, x.Ns)
            })
            .Single();

        row.YearUs.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(1_234_560).AddYears(1));
        row.CenturyUs.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(1_234_560).AddYears(100));
        row.YearMs.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30, 123).AddYears(1));
        row.CenturyMs.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30, 123).AddYears(100));

        // .123456789 truncated to scale 6 = .123456 — the .000000789 tail is intentionally lost.
        var truncated = new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(1_234_560).AddYears(1);
        row.YearNsNormalizedToScale6.Should().Be(truncated);
    }

    /// <summary>
    /// A <c>DateTimeOffset</c> operand keeps the same instant: the provider normalizes it to
    /// <c>toTimeZone(…, 'UTC')</c>, promotes to <c>DateTime64(6)</c> and materializes the result back as a
    /// zero-offset <c>DateTimeOffset</c> with the microseconds intact.
    /// </summary>
    [Fact]
    public void YearFamily_DateTimeOffsetPreservesInstant()
    {
        var row = Context.From<T256Row>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                PlusYear = x.Dto.AddYears(1),
                MinusYear = x.Dto.AddYears(-1),
                ZeroYears = x.Dto.AddYears(0),
                PlusCentury = x.Dto.AddYears(100)
            })
            .Single();

        var expected = new DateTimeOffset(new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(1_234_560), TimeSpan.Zero);

        row.PlusYear.UtcDateTime.Should().Be(expected.UtcDateTime.AddYears(1));
        row.PlusYear.Offset.Should().Be(TimeSpan.Zero);
        row.MinusYear.UtcDateTime.Should().Be(expected.UtcDateTime.AddYears(-1));
        row.ZeroYears.Should().Be(expected, "a zero year-family step must return the same instant");
        row.PlusCentury.UtcDateTime.Should().Be(expected.UtcDateTime.AddYears(100));
        row.PlusCentury.Offset.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// The promoted <c>DateTime64(6)</c> result is comparable server-side: the same year-family rendering
    /// used in a filter selects exactly the rows whose shifted instant passes the boundary, and a projected
    /// comparison materializes the matching booleans for every row (reader path included).
    /// </summary>
    [Fact]
    public void YearFamily_WorksInComparisons()
    {
        // Rows 1–2 shift to 2024 (below the 2050 boundary), rows 3–4 far beyond it.
        var filtered = Context.From<T256Row>()
            .Where(x => x.Us.AddYears(1) > Boundary)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        filtered.Should().Equal(3, 4);

        // A negative year step keeps rows 1–2 below 2023-06-01 and pushes 3–4 far past it.
        var shiftedBack = Context.From<T256Row>()
            .Where(x => x.Us.AddYears(-1) < new DateTime(2023, 6, 1))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        shiftedBack.Should().Equal(1, 2);

        var compared = Context.From<T256Row>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Shifted = SqlFunctions.Sql.date_add("year", 1, x.Us),
                LateEnough = SqlFunctions.Sql.date_add("year", 1, x.Us) > Boundary
            })
            .ToList();

        compared.Select(x => x.Id).Should().Equal(1, 2, 3, 4);
        compared[0].LateEnough.Should().BeFalse();
        compared[0].Shifted.Should().Be(Row1Us.AddYears(1));
        compared[1].LateEnough.Should().BeFalse();
        compared[1].Shifted.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30).AddTicks(6_543_210).AddYears(1));
        compared[2].LateEnough.Should().BeTrue();
        compared[2].Shifted.Should().Be(new DateTime(2100, 6, 15, 12, 0, 0).AddYears(1));
        compared[3].LateEnough.Should().BeTrue();
        compared[3].Shifted.Should().Be(new DateTime(2289, 2, 3, 4, 5, 6).AddTicks(70).AddYears(1));
    }

    /// <summary>
    /// A decade step crosses the 2106 <c>DateTime</c> limit and still reaches the end of the pinned 25.8
    /// range: <c>2100 + 1 decade = 2110</c> (beyond 2106, where a 4-byte <c>DateTime</c> wraps) and
    /// <c>2289-02-03 04:05:06 + 1 decade = 2299-02-03 04:05:06</c>, both exact and microsecond-preserving.
    /// </summary>
    [Fact]
    public void Decade_Crosses2106AndReaches2299()
    {
        var rows = Context.From<T256Row>()
            .Where(x => x.Id == 3 || x.Id == 4)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                PlusDecade = SqlFunctions.Sql.date_add("decade", 1, x.Us),
                PlusThreeDecades = SqlFunctions.Sql.date_add("decade", 3, x.Us),
                MinusDecade = SqlFunctions.Sql.date_add("decade", -1, x.Us)
            })
            .ToList();

        rows.Should().HaveCount(2);

        rows[0].Id.Should().Be(3);
        rows[0].PlusDecade.Should().Be(new DateTime(2110, 6, 15, 12, 0, 0), "a decade step past the 2106 DateTime limit must not wrap");
        rows[0].PlusDecade!.Value.Year.Should().BeGreaterThan(2106);
        rows[0].PlusThreeDecades.Should().Be(new DateTime(2130, 6, 15, 12, 0, 0));
        rows[0].MinusDecade.Should().Be(new DateTime(2090, 6, 15, 12, 0, 0));

        rows[1].Id.Should().Be(4);
        rows[1].PlusDecade.Should().Be(new DateTime(2299, 2, 3, 4, 5, 6).AddTicks(70), "2289 + 1 decade reaches the 2299 edge of the pinned range");
        rows[1].MinusDecade.Should().Be(new DateTime(2279, 2, 3, 4, 5, 6).AddTicks(70));
        // row 4 + 3 decades = 2319 is outside the pinned 1900–2299 range: that is the documented
        // limitation (deferred), so its clamping behaviour is deliberately not asserted here.
    }

    /// <summary>
    /// <c>millennium</c> is refused for a positive, zero and negative amount on the public
    /// <c>date_add</c> path. The failure is raised while the query is translated — the assertion below
    /// calls <see cref="Prepare{T}"/> only (no enumerator is created, so no reader/command is executed) —
    /// and the same translation succeeds for a supported unit, proving the throw comes from the field
    /// guard rather than from a failed connection.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Millennium_RejectsBeforeExecution(int amount)
    {
        var unsupported = Context.From<T256Row>()
            .Select(x => new { V = SqlFunctions.Sql.date_add("millennium", amount, x.Us) });

        FluentActions.Invoking(() => Prepare(unsupported))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*'millennium' is not a supported date_add field*");

        var supported = Context.From<T256Row>()
            .Select(x => new { V = SqlFunctions.Sql.date_add("year", amount, x.Us) });

        Prepare(supported).DbCommand.CommandText.Should().Contain("addYears(toDateTime64(");
    }
}

/// <summary>
/// T256 live fixture: one column per operand kind of the year-family variant matrix
/// (<c>DateTime64(3)</c>, <c>DateTime64(6)</c>, <c>DateTime64(9)</c>, their nullable forms and a
/// UTC <c>DateTime64(6)</c> read as <see cref="DateTimeOffset"/>).
/// </summary>
[SqlTable("t256_date_add")]
public sealed class T256Row
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("ms")]
    public DateTime Ms { get; set; }

    [Column("us")]
    public DateTime Us { get; set; }

    [Column("ns")]
    public DateTime Ns { get; set; }

    [Column("nms")]
    public DateTime? Nms { get; set; }

    [Column("nus")]
    public DateTime? Nus { get; set; }

    [Column("dto")]
    public DateTimeOffset Dto { get; set; }
}
