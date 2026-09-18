using FluentAssertions;
using nextorm.core;

namespace nextorm.integration.tests;

/// <summary>
/// End-to-end tests for the ClickHouse specific behaviours, backed by a Testcontainers instance
/// unless <c>NEXTORM_CLICKHOUSE_CONNECTION</c> points at an existing server. Unlike the shared
/// suite these run only against ClickHouse, because the functions under test either have no
/// portable equivalent (the <c>-If</c> combinators, <c>argMin</c>/<c>argMax</c>) or render
/// differently per provider.
/// </summary>
public sealed class ClickHouseIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void Count_ShouldReturn10()
    {
        _sut.SimpleEntity.Select(e => NORM.SQL.count()).First().Should().Be(10);
    }

    [Fact]
    public void DateTrunc_ShouldTruncateToMonth()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => NORM.SQL.date_trunc("month", x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 1));
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
    public void DateTimeAddMonths_ShouldShiftDate()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => x.Datetime!.Value.AddMonths(2))
            .First();

        r.Should().Be(new DateTime(2023, 3, 1, 10, 0, 0));
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
    public void StringAgg_ShouldConcatenateGroupValues()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.String != null)
            .Select(x => NORM.SQL.string_agg(x.String, ","))
            .First();

        r.Should().NotBeNull();
        r.Should().Contain("dadfasd").And.Contain("xxx");
    }

    [Fact]
    public void BitAggregates_ShouldMatchBitwiseOperations()
    {
        // simple_entity ids are 1..10: AND is 0, OR is 15 and XOR is 11.
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                And = NORM.PG_SQL.bit_and(x.Id),
                Or = NORM.PG_SQL.bit_or(x.Id),
                Xor = NORM.PG_SQL.bit_xor(x.Id)
            })
            .First();

        r.And.Should().Be(0);
        r.Or.Should().Be(15);
        r.Xor.Should().Be(11);
    }

    [Fact]
    public void Corr_ShouldReturnOneForIdenticalSeries()
    {
        _sut.ComplexEntity
            .Select(x => NORM.SQL.corr(x.Id, x.Id))
            .First()
            .Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void CovarPop_ShouldMatchPopulationVariance()
    {
        // ids 1..3: population variance is 2 / 3.
        _sut.ComplexEntity
            .Select(x => NORM.SQL.covar_pop(x.Id, x.Id))
            .First()
            .Should().BeApproximately(2.0 / 3.0, 1e-12);
    }

    [Fact]
    public void CovarSamp_ShouldMatchSampleVariance()
    {
        // ids 1..3: sample variance is 2 / 2 = 1.
        _sut.ComplexEntity
            .Select(x => NORM.SQL.covar_samp(x.Id, x.Id))
            .First()
            .Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void ArgMinMax_ShouldReturnValueAtExtremeKey()
    {
        var r = _sut.ComplexEntity
            .Select(x => new
            {
                Min = NORM.CLK_SQL.arg_min(x.RequiredString, x.Id),
                Max = NORM.CLK_SQL.arg_max(x.RequiredString, x.Id)
            })
            .First();

        r.Min.Should().Be("sdf");
        r.Max.Should().Be("34mfs");
    }

    [Fact]
    public void CountIf_ShouldReturnCount()
    {
        _sut.SimpleEntity
            .Select(x => NORM.CLK_SQL.count_if(() => x.Id <= 2))
            .First()
            .Should().Be(2);
    }

    [Fact]
    public void IfAggregates_ShouldFilterBeforeAggregating()
    {
        // The arguments are cast to the ClickHouse result types (Int64 for the integer sum/min/max,
        // Float64 for the average) so the driver reads them back without a lossy conversion.
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                Sum = NORM.CLK_SQL.sum_if((long)x.Id, () => x.Id <= 2),
                Avg = NORM.CLK_SQL.avg_if((double)x.Id, () => x.Id <= 2),
                Min = NORM.CLK_SQL.min_if((long)x.Id, () => x.Id <= 2),
                Max = NORM.CLK_SQL.max_if((long)x.Id, () => x.Id <= 2)
            })
            .First();

        r.Sum.Should().Be(3);
        r.Avg.Should().BeApproximately(1.5, 1e-12);
        r.Min.Should().Be(1);
        r.Max.Should().Be(2);
    }
}
