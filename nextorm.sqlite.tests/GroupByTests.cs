using FluentAssertions;
using nextorm.core;

namespace nextorm.sqlite.tests;

public class GroupByTests(TestDataRepository sut)
{
    private readonly TestDataRepository _sut = sut;

    [Fact]
    public void TestGroup()
    {
        var r = _sut.ComplexEntity.GroupBy(e => new { e.Int }).Select(e => new { e.Int, count = NORM.SQL.count() }).ToList();

        r.Should().NotBeNullOrEmpty();

        r.Count.Should().Be(2);

        r[0].Int.Should().BeNull();
        r[0].count.Should().Be(1);

        r[1].Int.Should().Be(1);
        r[1].count.Should().Be(2);
    }
    [Fact]
    public void TestGroup_Having()
    {
        var r = _sut.ComplexEntity.GroupBy(e => new { e.Int }).Having(e => NORM.SQL.count() > 1).Select(e => new { e.Int, count = NORM.SQL.count() }).ToList();

        r.Should().NotBeNullOrEmpty();

        r.Count.Should().Be(1);

        r[0].Int.Should().Be(1);
        r[0].count.Should().Be(2);
    }
    [Fact]
    public void TestGroup_WhereAndHaving()
    {
        var r = _sut.ComplexEntity.Where(e => e.Int == null).GroupBy(e => new { e.Int }).Having(e => NORM.SQL.count() > 1).Select(e => new { e.Int, count = NORM.SQL.count() }).ToList();

        r.Should().NotBeNullOrEmpty();

        r.Count.Should().Be(1);

        r[0].Int.Should().Be(1);
        r[0].count.Should().Be(2);
    }
    [Fact]
    public void TestGroup_Limit()
    {
        var r = _sut.ComplexEntity.GroupBy(e => new { e.Int }).Select(e => new { e.Int, count = NORM.SQL.count() }).First();

        r.Int.Should().BeNull();
        r.count.Should().Be(1);
    }
    [Fact]
    public void TestGroup_Sort()
    {
        var r = _sut.ComplexEntity.GroupBy(e => new { e.Int }).Select(e => new { e.Int, count = NORM.SQL.count() }).OrderBy(1, OrderDirection.Desc).First();

        r.Int.Should().Be(1);
        r.count.Should().Be(2);
    }
    [Fact]
    public void TestGroup_Avg()
    {
        var r = _sut.ComplexEntity.GroupBy(e => new { e.Int }).Select(e => new { e.Int, avg = NORM.SQL.avg(Convert.ToDouble(e.Id)) }).ToList();

        r[0].Int.Should().BeNull();
        r[0].avg.Should().Be(1);

        r[1].Int.Should().Be(1);
        r[1].avg.Should().Be(2.5);
    }
}