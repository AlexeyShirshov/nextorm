using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T241 (issue #241) provider-agnostic materialization regression: an active cancellable token and
/// <see cref="CancellationToken.None"/> must return the same complete, correctly ordered sorting
/// result and the same grouping result. This is the R241-06/R241-15 half of the frozen oracle — the
/// SQL-provider row mapper must keep reading every key column after the cancellation handling change.
/// <para>
/// The fixture is the shared <c>complex_entity</c> seed: rows (1, null), (2, 1), (3, 1). The non-null
/// filter therefore keeps ids 2 and 3, both with <c>nullableint = 1</c>.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public async Task T241_ActiveAndNone_SortingGrouping_KeepQueryResults()
    {
        using var cts = new CancellationTokenSource();

        var noneSorted = await _sut.ComplexEntity
            .Where(e => e.Int != null)
            .OrderBy(e => e.Int)
            .OrderBy(e => e.Id)
            .Select(e => new { e.Id, e.Int })
            .ToListAsync(CancellationToken.None);

        var activeSorted = await _sut.ComplexEntity
            .Where(e => e.Int != null)
            .OrderBy(e => e.Int)
            .OrderBy(e => e.Id)
            .Select(e => new { e.Id, e.Int })
            .ToListAsync(cts.Token);

        noneSorted.Select(x => x.Id).Should().Equal(2L, 3L);
        activeSorted.Should().Equal(noneSorted);

        var noneGrouped = await _sut.ComplexEntity
            .GroupBy(e => new { e.Int })
            .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
            .ToListAsync(CancellationToken.None);

        var activeGrouped = await _sut.ComplexEntity
            .GroupBy(e => new { e.Int })
            .Select(e => new { e.Int, count = SqlFunctions.Sql.count() })
            .ToListAsync(cts.Token);

        noneGrouped.Should().HaveCount(2);
        noneGrouped.Should().Contain(x => x.Int == null && x.count == 1);
        noneGrouped.Should().Contain(x => x.Int == 1 && x.count == 2);
        activeGrouped.Should().BeEquivalentTo(noneGrouped);
    }
}
