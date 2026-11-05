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
        var builtMin = E.SelectWhereMinOld(e => e.Score).ToList();
        var exMin = ExtremeRowExtensions.SelectWhereMin(E, e => e.Score).ToList();
        // Global minimum (score 1) is tied between ids 6 and 10; tolerate the tie.
        builtMin.Should().ContainSingle();
        exMin.Should().ContainSingle();
        builtMin[0].Score.Should().Be(1);
        exMin[0].Score.Should().Be(1);
        builtMin[0].Id.Should().BeOneOf(6, 10);
        exMin[0].Id.Should().BeOneOf(6, 10);

        var builtMax = E.SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All).ToList();
        var exMax = ExtremeRowExtensions.SelectWhereMax(E, e => e.Score, ExtremeRowTies.All).ToList();
        exMax.Select(e => e.Id).OrderBy(x => x).Should().Equal(builtMax.Select(e => e.Id).OrderBy(x => x));
    }

    [Fact]
    public void QueryComponentsEx_ClickHouse_GroupedAndNull_MatchBuiltIn()
    {
        var built = E.SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        var ex = ExtremeRowExtensions.SelectWhereMax(E, e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        ex.Select(e => e.Id).OrderBy(x => x).Should().Equal(built.Select(e => e.Id).OrderBy(x => x));
        ex.Should().NotContain(e => e.Category == "d");
    }

    [Fact]
    public void QueryComponentsEx_ClickHouse_Composite_MatchBuiltIn()
    {
        var built = E.SelectWhereMinOld(e => new { e.Category, e.Score }).ToList();
        var ex = ExtremeRowExtensions.SelectWhereMin(E, e => new { e.Category, e.Score }).ToList();
        ex.Select(e => e.Id).OrderBy(x => x).Should().Equal(built.Select(e => e.Id).OrderBy(x => x));
    }

    [Fact]
    public void QueryComponentsEx_ClickHouse_Projection_MatchBuiltIn()
    {
        var built = E.SelectWhereMinOld(e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All).ToList();
        var ex = ExtremeRowExtensions.SelectWhereMin(E, e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All).ToList();
        ex.Select(x => (x.Id, x.Label)).Should().Equal(built.Select(x => (x.Id, x.Label)));
    }
}
