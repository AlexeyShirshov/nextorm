using FluentAssertions;
using NextORM.Core;
using NextORM.QueryComponents;

namespace NextORM.Integration.Tests;

[Collection("ClickHouse")]
public sealed class ClickHouseQueryComponentsIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private EntityBuilder<ExtremaEntity> E => _sut.DataProvider.From<ExtremaEntity>();

    [Fact]
    public void QueryComponents_ClickHouse_MinOne_MaxAll_MatchBuiltIn()
    {
        var builtMin = E.SelectWhereMin(e => e.Score).ToList();
        var extMin = E.ExternalSelectWhereMin(e => e.Score).ToList();
        extMin.Select(e => e.Score).Should().Equal(builtMin.Select(e => e.Score));

        var builtMax = E.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        var extMax = E.ExternalSelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        extMax.Select(e => e.Id).OrderBy(x => x).Should().Equal(builtMax.Select(e => e.Id).OrderBy(x => x));
    }

    [Fact]
    public void QueryComponents_ClickHouse_GroupedAndNull_MatchBuiltIn()
    {
        var built = E.SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        var ext = E.ExternalSelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        ext.Select(e => e.Id).OrderBy(x => x).Should().Equal(built.Select(e => e.Id).OrderBy(x => x));
        ext.Should().NotContain(e => e.Category == "d");
    }

    [Fact]
    public void QueryComponents_ClickHouse_Projection_MatchBuiltIn()
    {
        var built = E.SelectWhereMin(e => e.Score, e => new { e.Id, e.Label }).ToList();
        var ext = E.ExternalSelectWhereMin(e => e.Score, e => new { e.Id, e.Label }).ToList();
        ext.Select(x => (x.Id, x.Label)).Should().Equal(built.Select(x => (x.Id, x.Label)));
    }

    [Fact]
    public void QueryComponents_ClickHouse_WarmCache_Rebinds()
    {
        int threshold = 3;
        var cmd = (QueryCommand<ExtremaEntity>)E
            .Where(e => e.Score > threshold)
            .ExternalSelectWhereMin(e => e.Score);
        var first = _sut.DataProvider.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        _sut.DataProvider.ToList(first).Should().ContainSingle(e => e.Score == 4);
        threshold = 8;
        var second = _sut.DataProvider.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        _sut.DataProvider.ToList(second).Should().ContainSingle(e => e.Score == 9);
    }
}
