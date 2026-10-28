using FluentAssertions;
using NextORM.Core;
using NextORM.Tests.Shared.ClickHouseRetry;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse date-part/date-arithmetic/session/grouping-modifier surface executed against a live
/// server. The dialects render dedicated <c>toXxx</c>/<c>addXxx</c>/<c>dateDiff</c> forms for these
/// names; the shared suite only exercises the portable subset.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseDateAndSessionExecutionTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static readonly DateTime Value = new(2023, 5, 17, 13, 45, 30);

    [Fact]
    public async Task DateParts_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Year = SqlFunctions.Sql.extract("year", Value),
                Quarter = SqlFunctions.Sql.extract("quarter", Value),
                Month = SqlFunctions.Sql.extract("month", Value),
                Day = SqlFunctions.Sql.extract("day", Value),
                Dow = SqlFunctions.Sql.extract("dow", Value),
                IsoDow = SqlFunctions.Sql.extract("isodow", Value),
                Doy = SqlFunctions.Sql.extract("doy", Value),
                Week = SqlFunctions.Sql.extract("week", Value),
                Hour = SqlFunctions.Sql.extract("hour", Value),
                Minute = SqlFunctions.Sql.extract("minute", Value),
                Second = SqlFunctions.Sql.extract("second", Value),
                Epoch = SqlFunctions.Sql.date_part("epoch", Value)
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseDateAndSessionExecutionTests.DateParts_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.Year.Should().Be(2023);
        r.Quarter.Should().Be(2);
        r.Month.Should().Be(5);
        r.Day.Should().Be(17);
        r.Dow.Should().Be(3);
        r.IsoDow.Should().Be(3);
        r.Doy.Should().Be(137);
        r.Week.Should().Be(20);
        r.Hour.Should().Be(13);
        r.Minute.Should().Be(45);
        r.Second.Should().Be(30);
        r.Epoch.Should().Be(new DateTimeOffset(Value, TimeSpan.Zero).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task DateTrunc_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Second = SqlFunctions.Sql.date_trunc("second", Value),
                Minute = SqlFunctions.Sql.date_trunc("minute", Value),
                Hour = SqlFunctions.Sql.date_trunc("hour", Value),
                Day = SqlFunctions.Sql.date_trunc("day", Value),
                Month = SqlFunctions.Sql.date_trunc("month", Value),
                Quarter = SqlFunctions.Sql.date_trunc("quarter", Value),
                Year = SqlFunctions.Sql.date_trunc("year", Value)
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseDateAndSessionExecutionTests.DateTrunc_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.Second.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30));
        r.Minute.Should().Be(new DateTime(2023, 5, 17, 13, 45, 0));
        r.Hour.Should().Be(new DateTime(2023, 5, 17, 13, 0, 0));
        r.Day.Should().Be(new DateTime(2023, 5, 17));
        r.Month.Should().Be(new DateTime(2023, 5, 1));
        r.Quarter.Should().Be(new DateTime(2023, 4, 1));
        r.Year.Should().Be(new DateTime(2023, 1, 1));
    }

    [Fact]
    public void DateTruncUnsupportedFields_ShouldBeRejected()
    {
        var act = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Sql.date_trunc("century", Value) })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*century*");
    }

    [Fact]
    public async Task DateAdd_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Year = SqlFunctions.Sql.date_add("year", 1, Value),
                Quarter = SqlFunctions.Sql.date_add("quarter", 1, Value),
                Month = SqlFunctions.Sql.date_add("month", 1, Value),
                Week = SqlFunctions.Sql.date_add("week", 1, Value),
                Day = SqlFunctions.Sql.date_add("day", 1, Value),
                Hour = SqlFunctions.Sql.date_add("hour", 1, Value),
                Minute = SqlFunctions.Sql.date_add("minute", 1, Value),
                Second = SqlFunctions.Sql.date_add("second", 1, Value),
                Milliseconds = SqlFunctions.Sql.date_add("milliseconds", 500, Value),
                Microseconds = SqlFunctions.Sql.date_add("microseconds", 500, Value),
                Decade = SqlFunctions.Sql.date_add("decade", 1, Value),
                DecadeAgain = SqlFunctions.Sql.date_add("decade", 2, Value)
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseDateAndSessionExecutionTests.DateAdd_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.Year.Should().Be(Value.AddYears(1));
        r.Quarter.Should().Be(Value.AddMonths(3));
        r.Month.Should().Be(Value.AddMonths(1));
        r.Week.Should().Be(Value.AddDays(7));
        r.Day.Should().Be(Value.AddDays(1));
        r.Hour.Should().Be(Value.AddHours(1));
        r.Minute.Should().Be(Value.AddMinutes(1));
        r.Second.Should().Be(Value.AddSeconds(1));
        r.Milliseconds.Should().Be(Value.AddMilliseconds(500));
        r.Microseconds.Should().Be(Value.AddTicks(5_000));
        r.Decade.Should().Be(Value.AddYears(10));
        r.DecadeAgain.Should().Be(Value.AddYears(20));
    }

    [Fact]
    public void DateAddUnsupportedField_ShouldBeRejected()
    {
        var act = () => _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Sql.date_add("fortnight", 1, Value) })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*fortnight*");
    }

    [Fact]
    public async Task DateDiff_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Days = SqlFunctions.Sql.date_diff_big("day", Value, Value.AddDays(10)),
                Months = SqlFunctions.Sql.date_diff_big("month", Value, Value.AddMonths(3)),
                Hours = SqlFunctions.Sql.date_diff_big("hour", Value, Value.AddHours(5)),
                Minutes = SqlFunctions.Sql.date_diff_big("minute", Value, Value.AddMinutes(5)),
                Seconds = SqlFunctions.Sql.date_diff_big("second", Value, Value.AddSeconds(5)),
                Milliseconds = SqlFunctions.Sql.date_diff_big("milliseconds", Value, Value.AddMilliseconds(500)),
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseDateAndSessionExecutionTests.DateDiff_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.Days.Should().Be(10);
        r.Months.Should().Be(3);
        r.Hours.Should().Be(5);
        r.Minutes.Should().Be(5);
        r.Seconds.Should().Be(5);
        r.Milliseconds.Should().Be(500);
    }

    [Fact]
    public async Task SessionInfoFunctions_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                User = SqlFunctions.Sql.current_user(),
                Database = SqlFunctions.Sql.current_database(),
                Version = SqlFunctions.Sql.version(),
                Connection = SqlFunctions.Sql.connection_id(),
                Host = SqlFunctions.ClickHouse.host_name(),
                Port = SqlFunctions.ClickHouse.tcp_port()
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseDateAndSessionExecutionTests.SessionInfoFunctions_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.User.Should().NotBeNullOrEmpty();
        r.Database.Should().NotBeNullOrEmpty();
        r.Version.Should().StartWith("2");
        r.Connection.Should().BeGreaterThanOrEqualTo(0UL);
        r.Host.Should().NotBeNullOrEmpty();
        r.Port.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UuidGenerators_ShouldExecute()
    {
        var query = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                V4 = SqlFunctions.Sql.gen_random_uuid(),
                V7 = SqlFunctions.Sql.uuidv7()
            });

        var r = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseDateAndSessionExecutionTests.UuidGenerators_ShouldExecute#1",
                ClickHouseReadCategory.PointRead),
            token => query.FirstAsync(token),
            TestContext.Current.CancellationToken);

        r.V4.Should().NotBe(Guid.Empty);
        r.V7.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GroupingModifiers_ShouldExecute()
    {
        _sut.ComplexEntity
            .GroupByRollup(x => x.Int)
            .Select(x => new { x.Int, Count = SqlFunctions.Sql.count() })
            .ToList()
            .Should().HaveCountGreaterThan(2);

        _sut.ComplexEntity
            .GroupByCube(x => x.Int)
            .Select(x => new { x.Int, Count = SqlFunctions.Sql.count() })
            .ToList()
            .Should().HaveCountGreaterThan(2);

        var query = _sut.ComplexEntity
            .GroupByGroupingSets(x => new { x.Int }, [0], [])
            .Select(x => new { x.Int, Count = SqlFunctions.Sql.count() });

        var groupingSets = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseDateAndSessionExecutionTests.GroupingModifiers_ShouldExecute#1",
                ClickHouseReadCategory.MaterialisedListRead),
            token => query.ToListAsync(token),
            TestContext.Current.CancellationToken);

        groupingSets.Should().HaveCountGreaterThan(2);
    }

    [Fact]
    public async Task OffsetOnlyPaging_ShouldExecute()
    {
        var all = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Id).ToList();
        var query = _sut.ComplexEntity.OrderBy(x => x.Id).Offset(1).Select(x => x.Id);

        var shifted = await ClickHouseReadRetry.ExecuteAsync(
            ClickHouseReadOperation.Audited(
                ClickHouseTestIdentity.CurrentTestDisplayName(),
                "ClickHouseDateAndSessionExecutionTests.OffsetOnlyPaging_ShouldExecute#1",
                ClickHouseReadCategory.MaterialisedListRead),
            token => query.ToListAsync(token),
            TestContext.Current.CancellationToken);

        shifted.Should().Equal(all.Skip(1));
    }
}
