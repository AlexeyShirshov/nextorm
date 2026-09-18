using FluentAssertions;
using nextorm.core;

namespace nextorm.integration.tests;

/// <summary>
/// Tests that assert PostgreSQL specific numeric behaviour, backed by a Testcontainers instance
/// unless NEXTORM_POSTGRES_CONNECTION points at an existing server.
/// </summary>
public sealed class PostgresSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void Stdev_ShouldMatchSampleStandardDeviation()
    {
        _sut.SimpleEntity
            .Select(x => NORM.SQL.stdev((double)x.Id))
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
            .Select(x => NORM.SQL.date_add("day", 1, x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
    }

    [Fact]
    public void DateDiff_ShouldCountDayBoundaries()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => NORM.SQL.date_diff("day", x.Datetime, NORM.SQL.date_add("day", 3, x.Datetime)))
            .First();

        r.Should().Be(3);
    }

    [Fact]
    public void EndOfMonth_ShouldReturnLastDay()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => NORM.SQL.end_of_month(x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 31));
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
