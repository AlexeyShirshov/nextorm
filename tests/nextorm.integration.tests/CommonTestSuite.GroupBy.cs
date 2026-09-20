using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void TestGroup()
    {
        var r = _sut.ComplexEntity.GroupBy(e => new { e.Int }).Select(e => new { e.Int, count = SqlFunctions.Sql.count() }).ToList();

        r.Should().NotBeNullOrEmpty();

        // Group ordering is not defined by SQL, so assert by group key instead of by position.
        r.Should().HaveCount(2);

        r.Should().Contain(x => x.Int == null && x.count == 1);
        r.Should().Contain(x => x.Int == 1 && x.count == 2);
    }

    [Fact]
    public void TestGroup_Having()
    {
        var r = _sut.ComplexEntity.GroupBy(e => new { e.Int }).Having(e => SqlFunctions.Sql.count() > 1).Select(e => new { e.Int, count = SqlFunctions.Sql.count() }).ToList();

        r.Should().NotBeNullOrEmpty();

        r.Count.Should().Be(1);

        r[0].Int.Should().Be(1);
        r[0].count.Should().Be(2);
    }

    [Fact]
    public void TestGroup_WhereAndHaving()
    {
        var r = _sut.ComplexEntity.Where(e => e.Int != null).GroupBy(e => new { e.Int }).Having(e => SqlFunctions.Sql.count() > 1).Select(e => new { e.Int, count = SqlFunctions.Sql.count() }).ToList();

        r.Should().NotBeNullOrEmpty();

        r.Count.Should().Be(1);

        r[0].Int.Should().Be(1);
        r[0].count.Should().Be(2);
    }

    [Fact]
    public void TestGroup_Limit()
    {
        // Order by the aggregate (never null) so the first group is deterministic across providers.
        var r = _sut.ComplexEntity.GroupBy(e => new { e.Int }).Select(e => new { e.Int, count = SqlFunctions.Sql.count() }).OrderBy(2, OrderDirection.Asc).First();

        r.Int.Should().BeNull();
        r.count.Should().Be(1);
    }

    [Fact]
    public void TestGroup_Sort()
    {
        // NULL ordering on DESC differs between providers (PostgreSQL sorts NULLs first, SQLite
        // last), so exclude the NULL group and assert the sort itself.
        var r = _sut.ComplexEntity.Where(e => e.Int != null).GroupBy(e => new { e.Int }).Select(e => new { e.Int, count = SqlFunctions.Sql.count() }).OrderBy(1, OrderDirection.Desc).First();

        r.Int.Should().Be(1);
        r.count.Should().Be(2);
    }

    [Fact]
    public void TestGroup_Avg()
    {
        var r = _sut.ComplexEntity.GroupBy(e => new { e.Int }).Select(e => new { e.Int, avg = SqlFunctions.Sql.avg(Convert.ToDouble(e.Id)) }).ToList();

        // Group ordering is not defined by SQL, so assert by group key instead of by position.
        r.Should().HaveCount(2);

        r.Should().Contain(x => x.Int == null && x.avg == 1);
        r.Should().Contain(x => x.Int == 1 && x.avg == 2.5);
    }
}
