using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace nextorm.core.tests;

public class InMemoryTests
{
    private readonly InMemoryDataContext _sut;

    public InMemoryTests(InMemoryDataContext sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }
    [Fact]
    public async void TestWhere()
    {
        var r = await _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => new { it.Id }).SingleOrDefaultAsync();

        r.Should().NotBeNull();
        r.Id.Should().Be(1);
    }
    [Fact]
    public async void TestWhere_Subquery()
    {
        var subQuery = _sut.SimpleEntity.Where(it => it.Id >= 1).Select(it => new { it.Id });
        var r = await _sut.From(subQuery).Where(it => it.Id == 2).Select(it => new { it.Id }).SingleOrDefaultAsync();

        r.Should().NotBeNull();
        r.Id.Should().Be(2);
    }
    [Fact]
    public async void TestWhereOnEmpty()
    {
        _sut.SimpleEntity.WithData(null);
        var r = await _sut.SimpleEntity.Select(it => new { it.Id }).SingleOrDefaultAsync();

        r.Should().BeNull();
    }
    [Fact]
    public async void TestAsync()
    {
        _sut.SimpleEntity.WithAsyncData(GetData());
        var r = await _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => new { it.Id }).SingleOrDefaultAsync();

        r.Should().NotBeNull();
        r.Id.Should().Be(1);

        static async IAsyncEnumerable<SimpleEntity> GetData()
        {
            yield return new SimpleEntity { Id = 1 };
            await Task.Delay(0);
            yield return new SimpleEntity { Id = 2 };
        }
    }
    [Fact]
    public async void TestNarrowingCastToQueryCommand()
    {
        var r = await _sut.SimpleEntity.Where(it => it.Id == 1).SingleOrDefaultAsync();

        r.Should().NotBeNull();
        r.Id.Should().Be(1);
    }
}