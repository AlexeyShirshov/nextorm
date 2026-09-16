using FluentAssertions;

namespace nextorm.core.tests;

public class InMemoryJoinTests
{
    private readonly InMemoryRepository _sut;
    public InMemoryJoinTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }
    [Fact]
    public async Task TestJoin()
    {
        var query = _sut.SimpleEntity.Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1).Select(p => new { FirstId = p.t1.Id, SecondId = p.t2.Id });

        var idx = 0;
        await foreach (var row in query.ToAsyncEnumerable())
        {
            idx++;
            row.FirstId.Should().Be(row.SecondId + 1);
        }

        idx.Should().BeGreaterThan(0);
    }
    [Fact]
    public async Task TestJoin2()
    {
        var query = _sut.SimpleEntity
            .Join(_sut.SimpleEntity, (t1, t2) => t1.Id == t2.Id + 1)
            .Join(_sut.SimpleEntity, (p, t3) => p.t2.Id == t3.Id - 1)
            .Select(p => new { FirstId = p.t1.Id, SecondId = p.t2.Id, ThirdId = p.t3.Id });

        var idx = 0;
        await foreach (var row in query.ToAsyncEnumerable())
        {
            idx++;
            row.FirstId.Should().Be(row.SecondId + 1);
            row.FirstId.Should().Be(row.ThirdId);
        }

        idx.Should().BeGreaterThan(0);
    }
}