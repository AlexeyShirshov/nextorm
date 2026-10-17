using FluentAssertions;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// T217 (#217) real-provider coverage for the server-side <see cref="DateTimeOffset"/> translation.
/// The shared assertions run once per provider subclass over a provider-specific <c>t217_dto</c>
/// fixture seeded with that provider's native column type, so the materialised value can be compared
/// against the provider contract (a server-side instant, never the client clock). The DateTimeOffset
/// aware ClickHouse reader is a provider-local adaptation, so its microsecond-retention case lives in
/// the ClickHouse subclass.
/// </summary>
public abstract class T217DateTimeOffsetTestsBase : ProviderTestSuite
{
    private static readonly object SeedGate = new();
    private static readonly HashSet<string> Seeded = new(StringComparer.Ordinal);

    // A millisecond-precision UTC instant so every provider round-trips the shared value exactly
    // (SQLite keeps milliseconds). ClickHouse additionally keeps microseconds in its nullable column.
    protected static readonly DateTime Instant = new(2023, 5, 17, 10, 0, 0, 123, DateTimeKind.Utc);

    // Whole-unit date-add on SQLite goes through datetime(), which drops the sub-second part, so its
    // value can be off by up to a second on those paths; a fractional unit that folded to a no-op is
    // still caught (a fraction lost is a whole second or more).
    protected virtual TimeSpan AddTolerance => TimeSpan.FromMilliseconds(1);

    // The `second`-unit add keeps its fraction on every provider except SQLite, whose datetime()
    // modifier returns whole seconds; the millisecond add keeps sub-second precision everywhere.
    protected virtual TimeSpan FractionalSecondTolerance => TimeSpan.FromMilliseconds(1);

    protected virtual TimeSpan SubSecondTolerance => TimeSpan.FromMilliseconds(2);

    // PostgreSQL/MySQL/SQLite/ClickHouse render DTO Now as a UTC instant; SQL Server's
    // sysdatetimeoffset() carries the server's local offset (UTC in the test container).
    protected virtual bool LocalNowUsesZeroOffset => true;

    protected EntityBuilder<IT217DtoEntity> Dto => _sut.DataProvider.From<IT217DtoEntity>();

    protected void EnsureFixture()
    {
        lock (SeedGate)
        {
            if (!Seeded.Add(Provider.Name))
                return;

            SeedFixture(_sut.DataProvider);
        }
    }

    /// <summary>Creates the provider-specific <c>t217_dto</c> table and inserts the shared rows.</summary>
    protected abstract void SeedFixture(IDataContext ctx);

    protected static void Exec(IDataContext ctx, string sql)
    {
        using (ctx.ExecuteRaw(sql))
        {
        }
    }

    private string SqlOf<T>(QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)_sut.DataProvider.GetPreparedQueryCommand(
                cmd, false, false, TestContext.Current.CancellationToken))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void UtcNow_ShouldBeServerInstantWithZeroOffset()
    {
        // C02: the server clock, not the client's, and a canonical offset-zero result.
        EnsureFixture();
        var now = Dto.Where(x => x.Id == 1).Select(_ => DateTimeOffset.UtcNow).Single();

        now.Offset.Should().Be(TimeSpan.Zero);
        now.UtcDateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Now_ShouldBeServerInstant()
    {
        EnsureFixture();
        var now = Dto.Where(x => x.Id == 1).Select(_ => DateTimeOffset.Now).Single();

        now.ToUniversalTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(10));
        if (LocalNowUsesZeroOffset)
            now.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ValueColumn_ShouldMaterializeSeededInstant()
    {
        // C02/C13: the stored DateTimeOffset round-trips through the unchanged scalar mapping.
        EnsureFixture();
        var value = Dto.Where(x => x.Id == 1).Select(x => x.Value).Single();

        value.Offset.Should().Be(TimeSpan.Zero);
        value.UtcDateTime.Should().Be(Instant);
    }

    [Fact]
    public void Parts_ShouldBeExtractedFromTheUtcInstant()
    {
        EnsureFixture();
        var r = Dto.Where(x => x.Id == 1)
            .Select(x => new
            {
                x.Value.Year,
                x.Value.Month,
                x.Value.Day,
                x.Value.Hour,
                x.Value.Minute,
                x.Value.Second,
                x.Value.DayOfYear
            })
            .First();

        r.Year.Should().Be(2023);
        r.Month.Should().Be(5);
        r.Day.Should().Be(17);
        r.Hour.Should().Be(10);
        r.Minute.Should().Be(0);
        r.Second.Should().Be(0);
        r.DayOfYear.Should().Be(137);
    }

    [Fact]
    public void Add_ShouldShiftOnTheServer()
    {
        EnsureFixture();
        var r = Dto.Where(x => x.Id == 1)
            .Select(x => new
            {
                D = x.Value.AddDays(1),
                H = x.Value.AddHours(2),
                Mi = x.Value.AddMinutes(30),
                S = x.Value.AddSeconds(1),
                Ms = x.Value.AddMilliseconds(500)
            })
            .First();

        r.D.UtcDateTime.Should().BeCloseTo(Instant.AddDays(1), AddTolerance);
        r.H.UtcDateTime.Should().BeCloseTo(Instant.AddHours(2), AddTolerance);
        r.Mi.UtcDateTime.Should().BeCloseTo(Instant.AddMinutes(30), AddTolerance);
        r.S.UtcDateTime.Should().BeCloseTo(Instant.AddSeconds(1), AddTolerance);
        r.Ms.UtcDateTime.Should().BeCloseTo(Instant.AddMilliseconds(500), AddTolerance);
        r.D.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AddFractional_ShouldKeepTheSignificantFraction()
    {
        // C05: a fractional interval must not collapse to a no-op, and it must not be truncated to the
        // coarse unit (a 1.5s add that kept only 1s is a 0.5s error). The millisecond add is the
        // provider-independent truncation-negative proof: keeping the whole 1500ms lands at +1.5s,
        // a whole-second truncation would sit at +1s and fail the tight sub-second tolerance.
        EnsureFixture();
        var r = Dto.Where(x => x.Id == 1)
            .Select(x => new
            {
                HalfDay = x.Value.AddDays(0.5),
                FractionalSecond = x.Value.AddSeconds(1.5),
                SubSecond = x.Value.AddMilliseconds(1500)
            })
            .First();

        r.HalfDay.UtcDateTime.Should().BeCloseTo(Instant.AddDays(0.5), FractionalSecondTolerance);
        r.FractionalSecond.UtcDateTime.Should().BeCloseTo(Instant.AddSeconds(1.5), FractionalSecondTolerance);
        r.SubSecond.UtcDateTime.Should().BeCloseTo(Instant.AddMilliseconds(1500), SubSecondTolerance);
        r.SubSecond.UtcDateTime.Should().BeAfter(Instant.AddSeconds(1.4));
    }

    [Fact]
    public void NullableColumn_ShouldMaterializeValueAndSqlNull()
    {
        // C06: a nullable DateTimeOffset column keeps a value and a SQL NULL stays null.
        EnsureFixture();
        var rows = Dto.Where(x => x.Id == 1 || x.Id == 2)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Maybe })
            .ToList();

        rows.Should().HaveCount(2);
        var hasValue = rows.Single(r => r.Id == 1).Maybe;
        hasValue.Should().NotBeNull();
        hasValue!.Value.Offset.Should().Be(TimeSpan.Zero);
        hasValue.Value.UtcDateTime.Should().BeCloseTo(new DateTime(2023, 5, 17, 10, 0, 0, 654, DateTimeKind.Utc), AddTolerance);
        rows.Single(r => r.Id == 2).Maybe.Should().BeNull();
    }

    [Fact]
    public void NullablePartsAndAdd_ShouldExtractFromTheNullableColumn()
    {
        // C06: a part/arithmetic expression built on the nullable column is translated server-side
        // (the column is used directly, not captured as a parameter); the SQL NULL row is covered by
        // NullableColumn_ShouldMaterializeValueAndSqlNull.
        EnsureFixture();
        var r = Dto.Where(x => x.Id == 1)
            .Select(x => new { Year = x.Maybe!.Value.Year, Shifted = x.Maybe!.Value.AddDays(1) })
            .First();

        r.Year.Should().Be(2023);
        r.Shifted.UtcDateTime.Should().BeCloseTo(
            new DateTime(2023, 5, 17, 10, 0, 0, 654, DateTimeKind.Utc).AddDays(1),
            AddTolerance);
    }

    [Fact]
    public void Parts_ShouldBeTranslatedInWhereAndOrderBy()
    {
        // C07: the part is evaluated on the server in both a predicate and a sort key. Scoped to the
        // shared ids so a provider-specific extra fixture row cannot change the expected set.
        EnsureFixture();
        var ids = Dto.Where(x => x.Id <= 3 && x.Value.Year == 2023).OrderBy(x => x.Id).Select(x => x.Id).ToList();
        ids.Should().Equal(1, 3);

        // January (id 2) < May (id 1) < December (id 3).
        var byMonth = Dto.Where(x => x.Id <= 3).OrderBy(x => x.Value.Month).Select(x => x.Id).ToList();
        byMonth.Should().Equal(2, 1, 3);
    }

    [Fact]
    public void UnsupportedMember_ShouldFailClosedBeforeExecution()
    {
        // C08/C09: a member with no server translation throws while building the SQL, never silently
        // evaluating on the client.
        EnsureFixture();
        var act = () => SqlOf(Dto.Select(x => new { Offset = x.Value.Offset }));

        act.Should().Throw<NotSupportedException>().WithMessage("*DateTimeOffset.Offset*");
    }

    [Fact]
    public void ExistingDateTimeMapping_ShouldBeUnchanged()
    {
        // C11/C13: the new DateTimeOffset path does not change the existing DateTime materialization.
        var dt = _sut.ComplexEntity.Where(x => x.Id == 1).Select(x => x.Datetime).First();

        dt.Should().Be(new DateTime(2023, 1, 1, 10, 0, 0));
    }
}

/// <summary>SQLite stores a DateTimeOffset as UTC text with an explicit offset.</summary>
public sealed class SqliteT217DateTimeOffsetTests : T217DateTimeOffsetTestsBase
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    protected override TimeSpan AddTolerance => TimeSpan.FromSeconds(1);

    // SQLite renders a `second`-unit add through datetime(), which returns whole seconds; the
    // millisecond add uses strftime('%f') and does keep its fraction.
    protected override TimeSpan FractionalSecondTolerance => TimeSpan.FromSeconds(1);

    protected override void SeedFixture(IDataContext ctx)
    {
        Exec(ctx, "drop table if exists t217_dto");
        Exec(ctx, "create table t217_dto (id integer primary key, value text not null, maybe text)");
        Exec(
            ctx,
            "insert into t217_dto (id, value, maybe) values "
            + "(1, '2023-05-17 10:00:00.123+00:00', '2023-05-17 10:00:00.654+00:00'), "
            + "(2, '2024-01-01 00:00:00.001+00:00', null), "
            + "(3, '2023-12-31 23:59:59.500+00:00', '2024-01-01 00:00:00.000+00:00')");
    }
}

/// <summary>PostgreSQL stores a DateTimeOffset as <c>timestamptz</c> and canonicalises to UTC.</summary>
public sealed class PostgresT217DateTimeOffsetTests : T217DateTimeOffsetTestsBase
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    protected override void SeedFixture(IDataContext ctx)
    {
        Exec(ctx, "drop table if exists t217_dto");
        Exec(ctx, "create table t217_dto (id integer primary key, value timestamptz not null, maybe timestamptz)");
        Exec(
            ctx,
            "insert into t217_dto (id, value, maybe) values "
            + "(1, timestamptz '2023-05-17 10:00:00.123+00', timestamptz '2023-05-17 10:00:00.654+00'), "
            + "(2, timestamptz '2024-01-01 00:00:00.001+00', null), "
            + "(3, timestamptz '2023-12-31 23:59:59.500+00', timestamptz '2024-01-01 00:00:00.000+00'), "
            + "(5, timestamptz '2023-01-15 10:00:00.123+00', null)");
    }

    [Fact]
    public void SessionTimeZone_ShouldNotChangeTheUtcParts()
    {
        // C12: 2023-05-17 is EDT (-04) and 2023-01-15 is EST (-05); the UTC-normalised parts must read
        // 10:00 in both, so the session time zone and DST do not leak into the result.
        EnsureFixture();
        // A dedicated context so the session time zone does not leak into the shared fixture checks;
        // its connection is disposed with the context, not eagerly.
        using var ctx = new PostgresDataContext(PostgresContainer.ConnectionString, new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        var conn = ((DataContext)ctx).GetConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "set time zone 'America/New_York'";
            cmd.ExecuteNonQuery();
        }

        var summer = ctx.From<IT217DtoEntity>().Where(x => x.Id == 1).Select(x => new { x.Value.Hour }).First();
        var winter = ctx.From<IT217DtoEntity>().Where(x => x.Id == 5).Select(x => new { x.Value.Hour }).First();

        summer.Hour.Should().Be(10);
        winter.Hour.Should().Be(10);
    }
}

/// <summary>MySQL stores a DateTimeOffset as a <c>datetime(6)</c> wall time without the offset.</summary>
public sealed class MySqlT217DateTimeOffsetTests : T217DateTimeOffsetTestsBase
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    protected override void SeedFixture(IDataContext ctx)
    {
        Exec(ctx, "drop table if exists t217_dto");
        Exec(ctx, "create table t217_dto (id int primary key, value datetime(6) not null, maybe datetime(6))");
        Exec(
            ctx,
            "insert into t217_dto (id, value, maybe) values "
            + "(1, '2023-05-17 10:00:00.123000', '2023-05-17 10:00:00.654000'), "
            + "(2, '2024-01-01 00:00:00.001000', null), "
            + "(3, '2023-12-31 23:59:59.500000', '2024-01-01 00:00:00.000000')");
    }
}

/// <summary>SQL Server has a native <c>datetimeoffset</c> that preserves the stored offset.</summary>
public sealed class SqlServerT217DateTimeOffsetTests : T217DateTimeOffsetTestsBase
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    protected override bool LocalNowUsesZeroOffset => false;

    protected override void SeedFixture(IDataContext ctx)
    {
        Exec(ctx, "drop table if exists t217_dto");
        Exec(ctx, "create table t217_dto (id int primary key, value datetimeoffset(7) not null, maybe datetimeoffset(7))");
        Exec(
            ctx,
            "insert into t217_dto (id, value, maybe) values "
            + "(1, '2023-05-17 10:00:00.1230000 +00:00', '2023-05-17 10:00:00.6540000 +00:00'), "
            + "(2, '2024-01-01 00:00:00.0010000 +00:00', null), "
            + "(3, '2023-12-31 23:59:59.5000000 +00:00', '2024-01-01 00:00:00.0000000 +00:00'), "
            + "(4, '2023-01-01 10:00:00.5000000 +02:00', null)");
    }

    [Fact]
    public void NonZeroOffset_ShouldBePreservedAndPartsReadByOffset()
    {
        // C12: the native datetimeoffset keeps a +02:00 offset; datepart/dateadd run on the stored
        // offset, so the wall time is 10:00 and AddHours keeps the offset.
        EnsureFixture();
        var r = Dto.Where(x => x.Id == 4)
            .Select(x => new { x.Value, Hour = x.Value.Hour, Shifted = x.Value.AddHours(1) })
            .First();

        r.Value.Offset.Should().Be(TimeSpan.FromHours(2));
        r.Value.UtcDateTime.Should().Be(new DateTime(2023, 1, 1, 8, 0, 0, 500, DateTimeKind.Utc));
        r.Hour.Should().Be(10);
        r.Shifted.Offset.Should().Be(TimeSpan.FromHours(2));
        r.Shifted.UtcDateTime.Should().Be(new DateTime(2023, 1, 1, 9, 0, 0, 500, DateTimeKind.Utc));
    }
}

/// <summary>
/// ClickHouse stores a DateTimeOffset as a UTC-normalised <c>DateTime64</c>; the provider-local reader
/// adaptation materialises it as an offset-zero DateTimeOffset without losing the microseconds.
/// </summary>
public sealed class ClickHouseT217DateTimeOffsetTests : T217DateTimeOffsetTestsBase
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    protected override void SeedFixture(IDataContext ctx)
    {
        Exec(ctx, "drop table if exists t217_dto");
        Exec(ctx, "create table t217_dto (id Int32, value DateTime64(6, 'UTC'), maybe Nullable(DateTime64(6, 'UTC'))) engine = Memory");
        Exec(
            ctx,
            "insert into t217_dto (id, value, maybe) values "
            + "(1, '2023-05-17 10:00:00.123000', '2023-05-17 10:00:00.654321'), "
            + "(2, '2024-01-01 00:00:00.001000', NULL), "
            + "(3, '2023-12-31 23:59:59.500000', '2024-01-01 00:00:00.000000')");
    }

    [Fact]
    public void DtoColumn_ShouldMaterializeOffsetZeroWithMicrosecondsRetained()
    {
        // C15: the driver surfaces DateTime64 as System.DateTime, so the DTO projection must dispatch
        // to the ClickHouse reader adaptation and keep the stored microseconds.
        EnsureFixture();
        var r = Dto.Where(x => x.Id == 1).Select(x => new { x.Value, x.Maybe }).First();

        r.Value.Offset.Should().Be(TimeSpan.Zero);
        r.Value.UtcDateTime.Should().Be(Instant);
        r.Maybe.Should().NotBeNull();
        r.Maybe!.Value.Offset.Should().Be(TimeSpan.Zero);
        r.Maybe.Value.Microsecond.Should().Be(321);
        r.Maybe.Value.UtcDateTime.Should().Be(new DateTime(2023, 5, 17, 10, 0, 0, 654, DateTimeKind.Utc).AddTicks(3210));
    }
}
