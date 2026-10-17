using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tuple/scalar IN-value sweep executed against SQLite: the planner has to walk every clause of the
/// command looking for unkeyed value lists (projection, having, grouping, join condition, ordering,
/// set operations, CTEs and subqueries). Each shape below puts a captured value list in a different
/// clause, which is exactly what the scan paths exist for.
/// </summary>
[Collection("Sqlite")]
public sealed class TupleInValuesSweepTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private static readonly List<(long Id, int? Int)> Pairs =
    [
        (1L, null),
        (2L, 1),
        (3L, 1)
    ];

    private static readonly List<long> Ids = [1L, 2L, 3L];

    [Fact]
    public void TupleIn_Where_ShouldFilter()
    {
        var rows = _sut.ComplexEntity
            .Where(x => Pairs.Contains(new ValueTuple<long, int?>(x.Id, x.Int)))
            .Select(x => x.Id)
            .ToList();

        rows.OrderBy(x => x).Should().Equal(1L, 2L, 3L);

        var mismatch = _sut.ComplexEntity
            .Where(x => Pairs.Contains(new ValueTuple<long, int?>(x.Id, 2)))
            .Select(x => x.Id)
            .ToList();

        mismatch.Should().BeEmpty();
    }

    [Fact]
    public void TupleIn_ProjectionAndOrdering_ShouldTranslate()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => Pairs.Contains(new ValueTuple<long, int?>(x.Id, x.Int)) ? 0 : 1)
            .Select(x => new
            {
                x.Id,
                Hit = Pairs.Contains(new ValueTuple<long, int?>(x.Id, x.Int))
            })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(r => r.Hit);
    }

    [Fact]
    public void ScalarIn_ProjectionAndHaving_ShouldTranslate()
    {
        var projected = _sut.ComplexEntity
            .Select(x => new
            {
                x.Id,
                Hit = Ids.Contains(x.Id)
            })
            .ToList();

        projected.Should().OnlyContain(r => r.Hit);

        var grouped = _sut.ComplexEntity
            .Where(x => x.Int != null)
            .GroupBy(x => x.Int)
            .Having(g => Ids.Contains((long)g.Int!))
            .Select(g => new { g.Int, Count = SqlFunctions.Sql.count() })
            .ToList();

        grouped.Should().HaveCount(1);
    }

    [Fact]
    public void TupleIn_JoinCondition_ShouldTranslate()
    {
        var joined = _sut.ComplexEntity
            .Join(_sut.SimpleEntity, (c, s) => Pairs.Contains(new ValueTuple<long, int?>(c.Id, c.Int)) && c.Id == s.Id)
            .Select(x => new { x.Item1.Id, Sid = x.Item2.Id })
            .ToList();

        joined.Should().HaveCount(3);
    }

    [Fact]
    public void TupleIn_InsideCteAndUnion_ShouldTranslate()
    {
        var ctx = _sut.DataProvider;

        var cte = ctx.From<IComplexEntity>()
            .Where(x => Pairs.Contains(new ValueTuple<long, int?>(x.Id, x.Int)))
            .Select(x => new { x.Id })
            .AsCte("tuple_in_cte");

        var rows = ctx.From(cte).Select(x => x.Id).ToList();
        rows.OrderBy(x => x).Should().Equal(1L, 2L, 3L);

        var union = _sut.SimpleEntity
            .Where(x => Ids.Contains(x.Id))
            .Select(x => x.Id)
            .Union(_sut.SimpleEntity.Where(x => Ids.Contains(x.Id)).Select(x => x.Id));

        _sut.From(union).Count().Should().Be(3);
    }
}
