using FluentAssertions;
using Microsoft.VisualBasic;

namespace nextorm.core.tests;

public class InMemoryJoinTests
{
    private readonly InMemoryDataContext _sut;
    public InMemoryJoinTests(InMemoryDataContext sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }
    [Fact]
    public async void TestJoin()
    {
        var query = _sut.SimpleEntity.Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1).Select(p => new { FirstId = p.t1.Id, SecondId = p.t2.Id });

        var idx = 0;
        await foreach (var row in query)
        {
            idx++;
            row.FirstId.Should().Be(row.SecondId + 1);
        }

        idx.Should().BeGreaterThan(0);
    }
    [Fact]
    public async void TestJoin2()
    {
        var query = _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1)
            .Join(_sut.SimpleEntity, (p, t3) => p.t2.Id == t3.Id + 1)
            .Select(p => new { FirstId = p.t1.Id, SecondId = p.t2.Id, ThirdId = p.t3.Id });

        var idx = 0;
        await foreach (var row in query)
        {
            idx++;
            row.FirstId.Should().Be(row.SecondId + 1);
            row.SecondId.Should().Be(row.ThirdId + 1);
        }

        idx.Should().BeGreaterThan(0);
    }
}