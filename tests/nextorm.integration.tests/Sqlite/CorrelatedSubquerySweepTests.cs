using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Correlated subquery sweep executed against SQLite: every terminal that can appear inside a
/// correlated projection (counts, aggregates, existence, first/last), the correlated predicate
/// forms in <c>Where</c>, and correlation inside ordering, grouping and having. The shared suite
/// pins the main shapes; this file walks the terminal matrix.
/// </summary>
[Collection("Sqlite")]
public sealed class CorrelatedSubquerySweepTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void CorrelatedTerminals_ShouldExecute()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Count = _sut.SimpleEntity.Where(s => s.Id <= x.Id).Count(),
                LongCount = _sut.SimpleEntity.Where(s => s.Id <= x.Id).LongCount(),
                Sum = _sut.SimpleEntity.Where(s => s.Id <= x.Id).Sum(s => s.Id),
                Min = _sut.SimpleEntity.Where(s => s.Id <= x.Id).Min(s => s.Id),
                Max = _sut.SimpleEntity.Where(s => s.Id <= x.Id).Max(s => s.Id),
                Avg = _sut.SimpleEntity.Where(s => s.Id <= x.Id).Avg(s => s.Id),
                Any = _sut.SimpleEntity.Where(s => s.Id == x.Id).Any(),
                AnyPred = SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == x.Id)),
                All = !SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id <= x.Id && s.Id <= 0)),
                First = _sut.SimpleEntity.Where(s => s.Id == x.Id).Select(s => s.Id).First(),
                FirstOrDefault = _sut.SimpleEntity.Where(s => s.Id == x.Id).Select(s => s.Id).FirstOrDefault()
            })
            .ToList();

        rows.Should().HaveCount(3);
        rows[0].Count.Should().Be(1);
        rows[2].Count.Should().Be(3);
        rows[2].LongCount.Should().BeGreaterThan(0);
        rows[2].Sum.Should().Be(6);
        rows[2].Min.Should().Be(1);
        rows[2].Max.Should().Be(3);
        rows[2].Any.Should().BeTrue();
        rows[2].AnyPred.Should().BeTrue();
        rows[2].All.Should().BeTrue();
        rows[1].First.Should().Be(2);
    }

    [Fact]
    public void CorrelatedPredicates_ShouldExecute()
    {
        var exists = _sut.ComplexEntity
            .Where(x => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == x.Id)))
            .Select(x => x.Id)
            .ToList();
        exists.Should().Equal(1L, 2L, 3L);

        var any = _sut.ComplexEntity
            .Where(x => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == x.Id && s.Id > 1)))
            .Select(x => x.Id)
            .ToList();
        any.Should().Equal(2L, 3L);

        var all = _sut.ComplexEntity
            .Where(x => !SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id <= x.Id && s.Id <= 0)))
            .Select(x => x.Id)
            .ToList();
        all.Should().Equal(1L, 2L, 3L);

        var nested = _sut.ComplexEntity
            .Where(x => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s =>
                s.Id == x.Id &&
                SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(t => t.Id == s.Id)))))
            .Select(x => x.Id)
            .ToList();
        nested.Should().Equal(1L, 2L, 3L);
    }

    [Fact]
    public void CorrelatedOrderingAndGrouping_ShouldExecute()
    {
        var ordered = _sut.ComplexEntity
            .OrderByDescending(x => _sut.SimpleEntity.Where(s => s.Id <= x.Id).Count())
            .Select(x => x.Id)
            .ToList();
        ordered.Should().Equal(3L, 2L, 1L);

        var grouped = _sut.ComplexEntity
            .Where(x => x.Int != null)
            .GroupBy(x => x.Int)
            .Having(g => SqlFunctions.Sql.count() >= 1)
            .Select(g => new { g.Int, Count = SqlFunctions.Sql.count() })
            .ToList();
        grouped.Should().HaveCount(1);
    }
}
