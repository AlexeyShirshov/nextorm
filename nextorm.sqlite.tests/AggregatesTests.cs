using FluentAssertions;
using Microsoft.Extensions.Logging;
using nextorm.core;

namespace nextorm.sqlite.tests;

public class AggregatesTests(TestDataRepository sut)
{
    private readonly TestDataRepository _sut = sut;
    [Fact]
    public void Count_ShouldReturn10()
    {
        var cnt = _sut.SimpleEntity.Select(e => NORM.SQL.count()).First();

        cnt.Should().Be(10);
    }
    [Fact]
    public void CountOnEntity_ShouldReturn10()
    {
        var cnt = _sut.SimpleEntity.Count();

        cnt.Should().Be(10);
    }
    [Fact]
    public void CountComplex_ShouldReturn()
    {
        var cnt = _sut.ComplexEntity.Select(e => NORM.SQL.count(e.Int)).First();

        cnt.Should().Be(2);

        cnt = _sut.ComplexEntity.Select(e => NORM.SQL.count(e.Int) + 2).First();

        cnt.Should().Be(4);
    }

    [Fact]
    public void CountDistinct_ShouldReturn1()
    {
        var cnt = _sut.ComplexEntity.Select(e => NORM.SQL.count_distinct(e.Int)).First();

        cnt.Should().Be(1);
    }
    [Fact]
    public void Min_ShouldReturn1()
    {
        var cnt = _sut.SimpleEntity.Select(e => NORM.SQL.min(e.Id)).First();

        cnt.Should().Be(1);

        _sut.SimpleEntity.Min(e => e.Id).Should().Be(cnt);
    }
    [Fact]
    public void Max_ShouldReturn10()
    {
        var cnt = _sut.SimpleEntity.Select(e => NORM.SQL.max(e.Id)).First();

        cnt.Should().Be(10);

        _sut.SimpleEntity.Max(e => e.Id).Should().Be(cnt);
    }
    [Fact]
    public void Avg_ShouldReturn6()
    {
        var cnt = _sut.SimpleEntity.Select(e => NORM.SQL.avg(e.Id)).First();

        cnt.Should().Be(6);

        _sut.SimpleEntity.Avg(e => e.Id).Should().Be(cnt);
    }
    [Fact]
    public void Sum_ShouldReturn55()
    {
        var cnt = _sut.SimpleEntity.Select(e => NORM.SQL.sum(e.Id)).First();

        cnt.Should().Be(55);

        _sut.SimpleEntity.Sum(e => e.Id).Should().Be(cnt);
    }
    [Fact]
    public void Stdev_ShouldReturn3()
    {
        var cnt = _sut.SimpleEntity.Select(e => NORM.SQL.stdev(e.Id)).First();

        cnt.Should().Be(3);

        _sut.SimpleEntity.Stdev(e => e.Id).Should().Be(cnt);
    }
}