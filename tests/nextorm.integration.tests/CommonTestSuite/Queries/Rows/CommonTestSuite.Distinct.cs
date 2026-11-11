using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void Distinct_ShouldRemoveDuplicateProjections()
    {
        // complex_entity stores nullableint values null, 1, 1 so DISTINCT must collapse the two 1s.
        var rows = _sut.ComplexEntity
            .Select(e => new { e.Int })
            .Distinct()
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().Contain(r => r.Int == null);
        rows.Should().Contain(r => r.Int == 1);
    }

    [Fact]
    public void Distinct_OverJoin_ShouldRemoveDuplicateRows()
    {
        // simple_entity ids 1..3 join complex_entity ids 1..3 (the remaining simple rows have no match).
        // complex_entity booleans are true, false, false, so the projected join has duplicate rows that
        // DISTINCT has to collapse.
        var rows = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => new { p.Item2.Boolean })
            .Distinct()
            .ToList();

        rows.Should().HaveCount(2);
    }

    [Fact]
    public void Distinct_OverCrossJoin_ShouldRemoveDuplicateRows()
    {
        var all = _sut.SimpleEntity
            .CrossJoin(_sut.ComplexEntity)
            .Select(p => new { p.Item2.Id })
            .ToList();
        all.Should().HaveCount(30);

        var distinct = _sut.SimpleEntity
            .CrossJoin(_sut.ComplexEntity)
            .Select(p => new { p.Item2.Id })
            .Distinct()
            .ToList();
        distinct.Should().HaveCount(3);
    }

    [Fact]
    public void Distinct_WithPaging_ShouldPageAfterDistinct()
    {
        var rows = _sut.ComplexEntity
            .Select(e => new { e.Boolean })
            .Distinct()
            .ToList();
        rows.Should().HaveCount(2);

        var firstPage = _sut.ComplexEntity
            .Limit(1)
            .Select(e => new { e.Boolean })
            .Distinct()
            .ToList();
        firstPage.Should().HaveCount(1);
    }

    [Fact]
    public void Distinct_WithUnion_ShouldDeduplicateAcrossBranches()
    {
        var cmd = _sut.ComplexEntity.Select(it => it.Int)
            .Distinct()
            .Union(_sut.ComplexEntity.Select(it => it.Int));

        var cnt = _sut.From(cmd).Count();

        cnt.Should().Be(2);
    }

    [Fact]
    public void Distinct_WithUnionAll_ShouldNotDeduplicateAcrossBranches()
    {
        var cmd = _sut.ComplexEntity.Select(it => it.Int)
            .Distinct()
            .UnionAll(_sut.ComplexEntity.Select(it => it.Int));

        var cnt = _sut.From(cmd).Count();

        // Left branch is distinct ({null, 1}); UNION ALL keeps the right branch ({null, 1, 1}).
        cnt.Should().Be(5);
    }
}
