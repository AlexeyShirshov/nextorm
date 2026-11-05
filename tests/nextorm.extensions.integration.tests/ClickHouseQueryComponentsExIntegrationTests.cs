using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Live behaviour of <c>SelectWhereMin</c>/<c>SelectWhereMax</c> on ClickHouse, asserted against the pinned
/// <c>extrema_entity</c> fixture (see <see cref="ExtensionsQueryComponentsLiveTests"/>) rather than against
/// another implementation.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseQueryComponentsExIntegrationTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private EntityBuilder<ExtremaEntity> E => _sut.DataProvider.From<ExtremaEntity>();

    private static int[] Ids(IEnumerable<ExtremaEntity> rows) => rows.Select(e => e.Id).OrderBy(id => id).ToArray();

    [Fact]
    public void MinOne_And_MaxAll_Scalar_Global()
    {
        var min = E.SelectWhereMin(e => e.Score).ToList();
        min.Should().ContainSingle();
        min[0].Score.Should().Be(1);
        min[0].Id.Should().BeOneOf(6, 10);

        var max = E.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        Ids(max).Should().Equal(3, 4);
    }

    [Fact]
    public void GroupedAll_WithNullGroupKey()
    {
        // a → 9 tied (3,4); b → 3 (5); c → 4 (9); NULL → 7 (8); d holds only a NULL score.
        var rows = E.SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();

        Ids(rows).Should().Equal(3, 4, 5, 8, 9);
        rows.Should().NotContain(e => e.Category == "d");
    }

    [Fact]
    public void CompositeKey_Global_ComparesLexicographically()
    {
        // Both key components are value keys, so a NULL in either excludes the row: (a,5) is the minimum
        // and (c,4) the maximum of the remaining rows.
        var min = E.SelectWhereMin(e => new { e.Category, e.Score }).ToList();
        min.Should().ContainSingle().Which.Id.Should().Be(2);

        var max = E.SelectWhereMax(e => new { e.Category, e.Score }).ToList();
        max.Should().ContainSingle().Which.Id.Should().Be(9);
    }

    [Fact]
    public void Projection_All_KeepsTheTiedWinners()
    {
        var rows = E.SelectWhereMin(e => e.Score, e => new { e.Id, e.Label }, ExtremeRowTies.All).ToList();

        rows.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id).Should().Equal((6, "six"), (10, "ten"));
    }
}
