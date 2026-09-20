using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests that assert PostgreSQL specific numeric behaviour, backed by a Testcontainers instance
/// unless NEXTORM_POSTGRES_CONNECTION points at an existing server.
/// </summary>
public sealed class PostgresSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void InfoFunctions_ShouldReturnServerValues()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                U = SqlFunctions.Sql.gen_random_uuid(),
                Db = SqlFunctions.Sql.current_database(),
                Ver = SqlFunctions.Sql.version(),
                User = SqlFunctions.Sql.current_user(),
                Session = SqlFunctions.Sql.session_user(),
                Schema = SqlFunctions.Sql.current_schema(),
                T = SqlFunctions.Postgres.pg_typeof(x.Id)
            })
            .First();

        r.U.Should().NotBe(Guid.Empty);
        r.Db.Should().NotBeNullOrEmpty();
        r.Ver.Should().Contain("PostgreSQL");
        r.User.Should().NotBeNullOrEmpty();
        r.Session.Should().NotBeNullOrEmpty();
        r.Schema.Should().NotBeNullOrEmpty();
        r.T.Should().Be("integer");
    }

    [Fact]
    public void Stdev_ShouldMatchSampleStandardDeviation()
    {
        _sut.SimpleEntity
            .Select(x => SqlFunctions.Sql.stdev((double)x.Id))
            .First()
            .Should().BeApproximately(3.0276503540974917, 1e-12);
    }

    [Fact]
    public void OrderByDescending_NullsFirst_ShouldSortData()
    {
        // PostgreSQL treats NULL as the largest value, so DESC puts the NULL group first.
        // The shared suite avoids depending on this; here it is asserted explicitly.
        var r = _sut.ComplexEntity.OrderByDescending(it => it.Int).Select(it => new { it.Id }).ToList();

        r[0].Id.Should().Be(1);
    }

    [Fact]
    public void DateAdd_ShouldShiftDate()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_add("day", 1, x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
    }

    [Fact]
    public void DateDiff_ShouldCountDayBoundaries()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff("day", x.Datetime, SqlFunctions.Sql.date_add("day", 3, x.Datetime)))
            .First();

        r.Should().Be(3);
    }

    [Fact]
    public void EndOfMonth_ShouldReturnLastDay()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.end_of_month(x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 31));
    }

    [Fact]
    public void SetSeed_ShouldReturnNullBecausePostgresReturnsVoid()
    {
        var value = _sut.SimpleEntity
            .Select(x => SqlFunctions.Postgres.setseed(0.5))
            .FirstOrDefault();

        value.Should().BeNull();
    }

    [Fact]
    public async Task ArrayShuffleSample_ShouldExecute()
    {
        var cmd = _sut.SimpleEntity
            .Select(x => x.Id)
            .PrepareFromSql(
                "select id from simple_entity where array_length(array_shuffle(array[1,2,3,4]), 1) = 4 "
                + "and array_length(array_sample(array[1,2,3,4], 2), 1) = 2",
                TestContext.Current.CancellationToken);

        var ids = await _sut.DataProvider.ToListAsync(cmd);

        ids.Should().NotBeEmpty();
    }

    [Fact]
    public void LastIndexOf_ShouldReturnZeroBasedLastPosition()
    {
        // "dadfasd" has its last 'd' at index 6.
        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.LastIndexOf("d"))
            .First()
            .Should().Be(6);
    }

    [Fact]
    public void StringJoinAndSplit_ShouldRoundTrip()
    {
        // "dadfasd" split on 'a' is {"d", "df", "sd"}, joined back with '-' gives "d-df-sd".
        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => string.Join("-", e.String!.Split('a')))
            .First()
            .Should().Be("d-df-sd");
    }
}
