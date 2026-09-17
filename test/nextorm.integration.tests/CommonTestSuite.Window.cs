using FluentAssertions;
using nextorm.core;

namespace nextorm.integration.tests;

/// <summary>
/// Behavioural coverage for window functions (<c>OVER</c>). The seeded <c>complex_entity</c> has three
/// rows: (1, null), (2, 1) and (3, 1), so partitioning by <c>nullableint</c> yields a singleton
/// partition (id 1) and a two-row partition (ids 2 and 3).
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void RowNumber_ShouldNumberRowsWithinPartition()
    {
        var rows = _sut.ComplexEntity
            .Select(e => new
            {
                e.Id,
                rn = NORM.SQL.row_number().Over(partitionBy: () => e.Int, orderBy: () => e.Id)
            })
            .OrderBy(1, OrderDirection.Asc)
            .ToList();

        var byId = rows.ToDictionary(x => x.Id);

        // id 1 is alone in the null partition; the 1 partition is ordered by id 2, 3.
        byId[1].rn.Should().Be(1);
        byId[2].rn.Should().Be(1);
        byId[3].rn.Should().Be(2);
    }

    [Fact]
    public void Rank_ShouldAssignTheSameRankToTies()
    {
        // false, false, true ordered by the boolean: the two false rows tie at rank 1, so the true row
        // is ranked 3 (rank leaves a gap after a tie).
        var rows = _sut.ComplexEntity
            .Select(e => new
            {
                e.Id,
                rank = NORM.SQL.rank().Over(NORM.SQL.asc(() => e.Boolean))
            })
            .ToList();

        var byId = rows.ToDictionary(x => x.Id);

        byId[2].rank.Should().Be(1);
        byId[3].rank.Should().Be(1);
        byId[1].rank.Should().Be(3);
    }

    [Fact]
    public void SumOverPartition_ShouldSumEachPartitionIndependently()
    {
        var rows = _sut.ComplexEntity
            .Select(e => new
            {
                e.Id,
                total = NORM.SQL.sum_over(e.Id).Over(partitionBy: () => e.Int)
            })
            .ToList();

        var byId = rows.ToDictionary(x => x.Id);

        byId[1].total.Should().Be(1L);
        byId[2].total.Should().Be(5L);
        byId[3].total.Should().Be(5L);
    }

    [Fact]
    public void LagAndLead_ShouldReturnNeighbourValues()
    {
        // The nullableint column is null, 1, 1 so the boundary and the "previous value is null" cases
        // are both covered; ordering is by id.
        var rows = _sut.ComplexEntity
            .Select(e => new
            {
                e.Id,
                prev = NORM.SQL.lag(e.Int, 1).Over(NORM.SQL.asc(() => e.Id)),
                next = NORM.SQL.lead(e.Int, 1).Over(NORM.SQL.asc(() => e.Id))
            })
            .OrderBy(1, OrderDirection.Asc)
            .ToList();

        var byId = rows.ToDictionary(x => x.Id);

        byId[1].prev.Should().BeNull();
        byId[1].next.Should().Be(1);

        byId[2].prev.Should().BeNull();
        byId[2].next.Should().Be(1);

        byId[3].prev.Should().Be(1);
        byId[3].next.Should().BeNull();
    }

    [Fact]
    public void LagWithDefault_ShouldUseTheDefaultAtTheBoundary()
    {
        var rows = _sut.ComplexEntity
            .Select(e => new
            {
                e.Id,
                prev = NORM.SQL.lag(e.Int, 1, 0).Over(NORM.SQL.asc(() => e.Id))
            })
            .OrderBy(1, OrderDirection.Asc)
            .ToList();

        // id 1 has no previous row, so the default (0) is used; id 2's previous value is null, which is
        // returned unchanged (the default only fills a missing row).
        rows[0].prev.Should().Be(0);
        rows[1].prev.Should().BeNull();
        rows[2].prev.Should().Be(1);
    }
}
