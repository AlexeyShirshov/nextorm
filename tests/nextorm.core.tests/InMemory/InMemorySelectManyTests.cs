using FluentAssertions;

namespace NextORM.Core.Tests;

public sealed class JoinOuter
{
    public int Id { get; set; }
}

public sealed class JoinInner
{
    public int OuterId { get; set; }
    public string Name { get; set; } = "";
}

public class InMemorySelectManyTests
{
    private readonly InMemoryRepository _sut;

    public InMemorySelectManyTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }

    [Fact]
    public void SelectMany_ShouldFlatten()
    {
        var items = new[] { 10, 20 };

        var r = _sut.SimpleEntity.SelectMany(_ => items).Select(x => x).ToList();

        r.Should().Equal(10, 20, 10, 20);
    }

    [Fact]
    public void SelectMany_Correlated_ShouldFlattenPerRow()
    {
        var r = _sut.SimpleEntity.SelectMany(x => Enumerable.Range(0, x.Id)).Select(x => x).ToList();

        // Outer 1 -> [0]; outer 2 -> [0, 1].
        r.Should().Equal(0, 0, 1);
    }

    [Fact]
    public void SelectMany_WithResultSelector_ShouldProjectPairs()
    {
        var r = _sut.SimpleEntity
            .SelectMany(x => Enumerable.Range(0, x.Id), (x, n) => x.Id * 10 + n)
            .Select(x => x)
            .ToList();

        r.Should().Equal(10, 20, 21);
    }

    [Fact]
    public void SelectMany_WhereAndSelect_ShouldApplyToFlattenedRows()
    {
        var r = _sut.SimpleEntity
            .SelectMany(x => Enumerable.Range(0, x.Id))
            .Where(n => n > 0)
            .Select(n => new { N = n })
            .ToList();

        r.Should().ContainSingle().Which.N.Should().Be(1);
    }

    [Fact]
    public async Task SelectMany_ToListAsync_ShouldFlatten()
    {
        var r = await _sut.SimpleEntity
            .SelectMany(x => Enumerable.Range(0, x.Id))
            .Select(x => x)
            .ToListAsync();

        r.Should().Equal(0, 0, 1);
    }

    [Fact]
    public void GroupJoin_ShouldGroupInnerRows()
    {
        var outer = _sut.DataProvider.From<JoinOuter>();
        outer.WithData(new[] { new JoinOuter { Id = 1 }, new JoinOuter { Id = 2 }, new JoinOuter { Id = 3 } });

        var inner = _sut.DataProvider.From<JoinInner>();
        inner.WithData(new[]
        {
            new JoinInner { OuterId = 1, Name = "a" },
            new JoinInner { OuterId = 1, Name = "b" },
            new JoinInner { OuterId = 2, Name = "c" },
        });

        var r = outer
            .GroupJoin(inner, o => o.Id, i => i.OuterId, (o, group) => new { o.Id, Names = group.Select(x => x.Name).ToList() })
            .Select(x => x)
            .ToList();

        r.Should().HaveCount(3);
        r.Single(x => x.Id == 1).Names.Should().Equal("a", "b");
        r.Single(x => x.Id == 2).Names.Should().Equal("c");
        r.Single(x => x.Id == 3).Names.Should().BeEmpty();
    }
}
