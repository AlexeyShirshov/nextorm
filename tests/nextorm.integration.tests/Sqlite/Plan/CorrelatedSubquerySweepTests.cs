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
        rows.Select(r => r.LongCount).Should().Equal(1L, 2L, 3L);
        rows[2].LongCount.Should().Be(3L);
        rows[2].LongCount.Should().BeGreaterThan(0);
        rows[2].Sum.Should().Be(6);
        rows[2].Min.Should().Be(1);
        rows[2].Max.Should().Be(3);
        rows[2].Any.Should().BeTrue();
        rows[2].AnyPred.Should().BeTrue();
        rows[2].All.Should().BeTrue();
        rows[1].First.Should().Be(2);
    }

    /// <summary>
    /// #279: a correlated <c>LongCount()</c> whose inner predicate matches no rows returns <c>0L</c> per
    /// outer row (the aggregate's identity), never an entity value or a skipped row.
    /// </summary>
    [Fact]
    public void CorrelatedLongCount_ShouldReturnZeroWhenNoRowsMatch()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                LongCount = _sut.SimpleEntity.Where(s => s.Id > x.Id + 100).LongCount()
            })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.LongCount).Should().Equal(0L, 0L, 0L);
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

    [Fact]
    public void CorrelatedVarianceTerminals_ShouldExecute()
    {
        var rows = _sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Stdev = _sut.SimpleEntity.Where(s => s.Id <= x.Id + 1).Stdev(s => (double)s.Id),
                Stdevp = _sut.SimpleEntity.Where(s => s.Id <= x.Id + 1).Stdevp(s => (double)s.Id),
                Var = _sut.SimpleEntity.Where(s => s.Id <= x.Id + 1).Var(s => (double)s.Id),
                Varp = _sut.SimpleEntity.Where(s => s.Id <= x.Id + 1).Varp(s => (double)s.Id)
            })
            .ToList();

        rows.Should().HaveCount(3);

        // {1, 2}: sample variance 0.5, population variance 0.25.
        rows[0].Var.Should().BeApproximately(0.5, 1e-9);
        rows[0].Stdev.Should().BeApproximately(Math.Sqrt(0.5), 1e-9);
        rows[0].Varp.Should().BeApproximately(0.25, 1e-9);

        // {1, 2, 3}: sample variance 1, population variance 2/3.
        rows[1].Var.Should().BeApproximately(1.0, 1e-9);
        rows[1].Stdev.Should().BeApproximately(1.0, 1e-9);
        rows[1].Varp.Should().BeApproximately(2.0 / 3.0, 1e-9);
        rows[1].Stdevp.Should().BeApproximately(Math.Sqrt(2.0 / 3.0), 1e-9);

        // {1, 2, 3, 4}: sample variance 5/3, population variance 1.25.
        rows[2].Var.Should().BeApproximately(5.0 / 3.0, 1e-9);
        rows[2].Varp.Should().BeApproximately(1.25, 1e-9);
    }
}
