using FluentAssertions;
using nextorm.sqlite.tests;

namespace nextorm.core;

public class LinqExtensionsTests
{
    private readonly TestDataRepository _sut;
    public LinqExtensionsTests(TestDataRepository sut)
    {
        _sut = sut;
    }

    [Fact]
    public async Task First_ShouldReturnEntity()
    {
        var e = await _sut.ComplexEntity.Select(it => new { it.Id, it.TinyInt, it.SmallInt }).FirstAsync();
        e.Should().NotBeNull();
    }
    [Fact]
    public async Task First_ShouldNotReturnEntity()
    {
        var e = await _sut.ComplexEntity
            .Where(it => it.Id < 0)
            .Select(it => new { it.Id, it.TinyInt, it.SmallInt }).FirstOrDefaultAsync();
        e.Should().BeNull();
    }
    [Fact]
    public async Task Single_ShouldReturnEntity()
    {
        var e = await _sut.ComplexEntity
            .Where(it => it.Id == 1)
            .Select(it => new { it.Id, it.TinyInt, it.SmallInt }).SingleAsync();
        e.Should().NotBeNull();
    }
    [Fact]
    public async Task Single_ShouldNotReturnEntity()
    {
        var e = await _sut.ComplexEntity
            .Where(it => it.Id < 0)
            .Select(it => new { it.Id, it.TinyInt, it.SmallInt }).SingleOrDefaultAsync();
        e.Should().BeNull();
    }
    [Fact]
    public async Task Any_ShouldReturnTrue()
    {
        var e = await _sut.ComplexEntity.Select(it => new { it.Id, it.TinyInt, it.SmallInt }).AnyAsync();
        e.Should().BeTrue();
    }
    [Fact]
    public async Task Single_ShouldReturnFalse()
    {
        var e = await _sut.ComplexEntity
            .Where(it => it.Id < 0)
            .Select(it => new { it.Id, it.TinyInt, it.SmallInt }).AnyAsync();
        e.Should().BeFalse();
    }
}