using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Window-function sweep against SQLite: the ranking functions that the shared window suite does not
/// call (<c>dense_rank</c>, <c>ntile</c>, <c>desc</c> ordering), the value functions
/// (<c>first_value</c>/<c>last_value</c>/<c>nth_value</c>) and the aggregate windows with explicit
/// ROWS/RANGE frames.
/// </summary>
[Collection("Sqlite")]
public sealed class WindowFunctionSweepTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void RankingFunctions_ShouldExecute()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Dense = SqlFunctions.Sql.dense_rank().Over(SqlFunctions.Sql.asc(() => x.Int)),
                Bucket = SqlFunctions.Sql.ntile(2).Over(SqlFunctions.Sql.asc(() => x.Id)),
                Rank = SqlFunctions.Sql.rank().Over(SqlFunctions.Sql.desc(() => x.Id)),
                Row = SqlFunctions.Sql.row_number().Over(
                    new Expression<Func<object?>>[] { () => x.Int },
                    new[] { SqlFunctions.Sql.desc(() => x.Id) })
            })
            .ToList();

        rows.Select(r => r.Dense).Should().Equal(1, 2, 2);
        rows.Select(r => r.Bucket).Should().Equal(1, 1, 2);
        rows.Select(r => r.Rank).Should().Equal(3, 2, 1);
        rows.Select(r => r.Row).Should().Equal(1, 2, 1);
    }

    [Fact]
    public void ValueFunctionsWithFrame_ShouldExecute()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                First = SqlFunctions.Sql.first_value(x.Int).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.Preceding(1), WindowFrameBound.CurrentRow)),
                Last = SqlFunctions.Sql.last_value(x.Int).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)),
                Second = SqlFunctions.Sql.nth_value(x.Int, 2).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)),
                Lagged = SqlFunctions.Sql.lag(x.Id, 1, 0).Over(SqlFunctions.Sql.asc(() => x.Id)),
                Led = SqlFunctions.Sql.lead(x.Id, 1, 0).Over(SqlFunctions.Sql.asc(() => x.Id))
            })
            .ToList();

        rows.Select(r => r.Last).Should().Equal(null, 1, 1);
        rows.Select(r => r.First).Should().Equal(null, null, 1);
        rows.Select(r => r.Second).Should().Equal(null, 1, 1);
        rows.Select(r => r.Lagged).Should().Equal(0, 1, 2);
        rows.Select(r => r.Led).Should().Equal(2, 3, 0);
    }

    [Fact]
    public void AggregateWindows_ShouldExecute()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Sum = SqlFunctions.Sql.sum_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)),
                Avg = SqlFunctions.Sql.avg_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)),
                Count = SqlFunctions.Sql.count_over().Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Range(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)),
                Min = SqlFunctions.Sql.min_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Groups(1, 1)),
                Max = SqlFunctions.Sql.max_over(x.Id).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Groups(1, 1))
            })
            .ToList();

        rows.Select(r => r.Sum).Should().Equal(1L, 3L, 6L);
        rows.Select(r => r.Avg).Should().HaveCount(3);
        rows.Select(r => r.Count).Should().Equal(1, 2, 3);
        rows.Select(r => r.Min).Should().Equal(1L, 1L, 2L);
        rows.Select(r => r.Max).Should().Equal(2L, 3L, 3L);
    }
}
