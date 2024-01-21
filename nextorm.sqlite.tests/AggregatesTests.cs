using FluentAssertions;
using Microsoft.Extensions.Logging;
using nextorm.core;

namespace nextorm.sqlite.tests;

public class AggregatesTests(TestDataRepository sut, ILogger<SqlCommandTests> logger)
{
    private readonly TestDataRepository _sut = sut;
    private readonly ILogger<SqlCommandTests> _logger = logger;
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
        var cnt = _sut.ComplexEntity.Select(e => NORM.SQL.distinct_count(e.Int)).First();

        cnt.Should().Be(1);
    }
}