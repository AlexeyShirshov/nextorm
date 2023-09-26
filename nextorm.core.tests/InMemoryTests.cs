using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace nextorm.core.tests;

public class InMemoryTests
{
    private readonly InMemoryDataContext _sut;

    public InMemoryTests(InMemoryDataContext sut)
    {
        _sut = sut;
        _sut.SimpleEntity.AddRange(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }
    [Fact]
    public async void TestWhere()
    {
        var r = await _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => new { it.Id }).FirstOrDefaultAsync();

        r.Should().NotBeNull();
        r.Id.Should().Be(1);
    }
}