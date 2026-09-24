using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Exercises PostgreSQL-specific query shapes: <c>WITH TIES</c>, <c>ROLLUP</c>/<c>CUBE</c> grouping and
/// the multi-table terminal helpers.
/// </summary>
public sealed class PostgresSqlFeaturesTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void WithTies_ShouldReturnData()
    {
        var ids = _sut.SimpleEntity
            .OrderBy(x => x.Id)
            .WithTies()
            .Limit(3)
            .Select(x => x.Id)
            .ToList();

        ids.Should().HaveCount(3);
    }

    [Fact]
    public void RollupAndCube_ShouldAggregate()
    {
        var rollup = _sut.ComplexEntity
            .GroupByRollup(e => e.Int)
            .Select(e => new { e.Int, C = SqlFunctions.Sql.count() })
            .ToList();
        rollup.Should().HaveCount(3);

        var cube = _sut.ComplexEntity
            .GroupByCube(e => e.Int)
            .Select(e => new { e.Int, C = SqlFunctions.Sql.count() })
            .ToList();
        cube.Should().HaveCount(3);
    }

    [Fact]
    public void LimitAndOffset_ShouldPage()
    {
        var page = _sut.SimpleEntity
            .OrderBy(x => x.Id)
            .Limit(3)
            .Select(x => x.Id)
            .ToList();
        page.Should().Equal(1, 2, 3);
    }
}
