using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;

namespace NextORM.Integration.Tests;

[Collection("ClickHouse")]
public sealed class ClickHouseQueryComponentsExIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private EntityBuilder<ExtremaEntity> E => _sut.DataProvider.From<ExtremaEntity>();

    [Fact]
    public void QueryComponentsEx_ClickHouse_MinOne_MaxAll_MatchBuiltIn()
    {
        var builtMin = E.SelectWhereMin(e => e.Score).ToList();
        var exMin = E.SelectWhereMinEx(e => e.Score).ToList();
        // Global minimum (score 1) is tied between ids 6 and 10; tolerate the tie.
        builtMin.Should().ContainSingle();
        exMin.Should().ContainSingle();
        builtMin[0].Score.Should().Be(1);
        exMin[0].Score.Should().Be(1);
        builtMin[0].Id.Should().BeOneOf(6, 10);
        exMin[0].Id.Should().BeOneOf(6, 10);

        var builtMax = E.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        var exMax = E.SelectWhereMaxEx(e => e.Score, ExtremeRowTies.All).ToList();
        exMax.Select(e => e.Id).OrderBy(x => x).Should().Equal(builtMax.Select(e => e.Id).OrderBy(x => x));
    }

    [Fact]
    public void QueryComponentsEx_ClickHouse_GroupedAndNull_MatchBuiltIn()
    {
        var built = E.SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        var ex = E.SelectWhereMaxEx(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        ex.Select(e => e.Id).OrderBy(x => x).Should().Equal(built.Select(e => e.Id).OrderBy(x => x));
        ex.Should().NotContain(e => e.Category == "d");
    }

    [Fact]
    public void QueryComponentsEx_ClickHouse_Composite_MatchBuiltIn()
    {
        var built = E.SelectWhereMin(e => new { e.Category, e.Score }).ToList();
        var ex = E.SelectWhereMinEx(e => new { e.Category, e.Score }).ToList();
        ex.Select(e => e.Id).OrderBy(x => x).Should().Equal(built.Select(e => e.Id).OrderBy(x => x));
    }

    [Fact]
    public void QueryComponentsEx_ClickHouse_Projection_MatchBuiltIn()
    {
        var built = E.SelectWhereMin(e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All).ToList();
        var ex = E.SelectWhereMinEx(e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All).ToList();
        ex.Select(x => (x.Id, x.Label)).Should().Equal(built.Select(x => (x.Id, x.Label)));
    }
}
