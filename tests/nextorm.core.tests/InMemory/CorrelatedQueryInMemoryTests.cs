using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory provider evaluates a correlated subquery once per outer row, with the outer row's
/// values bound to the inner query, matching the SQL providers' per-row semantics.
/// </summary>
public class CorrelatedQueryInMemoryTests
{
    private readonly InMemoryRepository _sut;

    public CorrelatedQueryInMemoryTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }

    [Fact]
    public void CorrelatedExistsInWhere_ShouldFilterPerRow()
    {
        var r = _sut.SimpleEntity
            .Where(it => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)))
            .Select(it => it.Id)
            .ToList();

        r.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void CorrelatedExistsInWhere_ShouldFilterOutNonMatchingRows()
    {
        var r = _sut.SimpleEntity
            .Where(it => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id + 100)))
            .Select(it => it.Id)
            .ToList();

        r.Should().BeEmpty();
    }

    [Fact]
    public void CorrelatedExistsInProjection_ShouldEvaluatePerRow()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, has = SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)) })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.has);
    }

    [Fact]
    public void CorrelatedScalarInProjection_ShouldMatchTheOuterRow()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, sid = _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id).First() })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.sid == row.Id);
    }

    [Fact]
    public void CorrelatedScalarInWhere_ShouldFilterOnTheOuterRow()
    {
        var r = _sut.SimpleEntity
            .Where(it => it.Id == _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id).First())
            .Select(it => new { it.Id })
            .ToList();

        r.Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void CorrelatedScalarInOrderBy_ShouldOrderByTheOuterRow()
    {
        var r = _sut.SimpleEntity
            .OrderBy(it => _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id).First())
            .Select(it => new { it.Id })
            .ToList();

        r.Select(x => x.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void CorrelatedInInProjection_ShouldEvaluatePerRow()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, inc = SqlFunctions.Sql.@in(it.Id, _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id)) })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.inc);
    }

    [Fact]
    public void CorrelatedAggregateInProjection_ShouldAggregatePerRow()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, cnt = _sut.SimpleEntity.Where(s => s.Id == it.Id).Count() })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.cnt == 1);
    }

    [Fact]
    public void CorrelatedScalarFirstOrDefault_ShouldYieldDefaultWhenNoRowMatches()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).FirstOrDefault() })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.sid == 0);
    }

    [Fact]
    public void CorrelatedScalarSingle_ShouldYieldValueWhenExactlyOneRowMatches()
    {
        var r = _sut.SimpleEntity
            .Where(s => s.Id == 1)
            .Select(s => new { s.Id, cid = _sut.SimpleEntity.Where(c => c.Id == s.Id).Select(c => (int?)c.Id).Single() })
            .ToList();

        r.Should().ContainSingle();
        r[0].cid.Should().Be(1);
    }

    [Fact]
    public void CorrelatedScalarSingle_ShouldThrowWhenMultipleRowsMatch()
    {
        var act = () => _sut.SimpleEntity
            .Select(s => new { s.Id, cid = _sut.SimpleEntity.Select(c => (int?)c.Id).Single() })
            .ToList();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NonCorrelatedScalarInProjection_ShouldEvaluateOnce()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, sid = _sut.SimpleEntity.Where(s => s.Id == 1).Select(s => s.Id).First() })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.sid == 1);
    }

    [Fact]
    public void CorrelatedExistsCombinedWithOr_ShouldFilterPerRow()
    {
        var r = _sut.SimpleEntity
            .Where(it => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)) || it.Id == 1)
            .Select(it => it.Id)
            .ToList();

        r.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void CorrelatedExistsCombinedWithAnd_ShouldFilterPerRow()
    {
        var r = _sut.SimpleEntity
            .Where(it => it.Id > 0 && SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)))
            .Select(it => it.Id)
            .ToList();

        r.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void NegatedCorrelatedExists_ShouldFilterPerRow()
    {
        var r = _sut.SimpleEntity
            .Where(it => !SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)))
            .Select(it => it.Id)
            .ToList();

        r.Should().BeEmpty();
    }
}
