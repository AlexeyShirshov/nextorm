using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public async Task SelectJoin_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.RequiredString }).ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
            row.RequiredString.Should().NotBeNullOrEmpty();
        }

        idx.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelectJoinWithWhere_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Where(p => p.Item2.Boolean ?? false)
            .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
            .ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
            row.RequiredString.Should().NotBeNullOrEmpty();
        }

        idx.Should().Be(1);
    }

    [Fact]
    public async Task SelectWhereJoin_ShouldReturnData()
    {
        long idx = 2;
        await foreach (var row in _sut.SimpleEntity
            .Where(it => it.Id > 2)
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Where(p => p.Item2.RequiredString == "34mfs")
            .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
            .ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
            row.RequiredString.Should().NotBeNullOrEmpty();
        }

        idx.Should().Be(3);
    }

    [Fact]
    public async Task SelectJoinSubquery_ShouldReturnData()
    {
        var subQuery = _sut.ComplexEntity.Where(it => it.Id == 3).Select(it => new { it.Id, it.RequiredString, it.Boolean });
        long idx = 2;
        await foreach (var row in _sut.SimpleEntity.Join(subQuery, (s, c) => s.Id == c.Id).Select(p => new { p.Item1.Id, p.Item2.RequiredString }).ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
            row.RequiredString.Should().NotBeNullOrEmpty();
        }

        idx.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelectJoinSubqueryWithJoin_ShouldReturnData()
    {
        var subQuery = _sut.ComplexEntity.Join(_sut.ComplexEntity, (c1, c2) => c1.RequiredString == c2.String).Select(p => new { Id1 = p.Item1.Id, Id2 = p.Item2.Id, Double = p.Item1.Double + p.Item2.Double });
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Join(subQuery, (s, c) => s.Id == c.Id1 + c.Id2).Select(p => new { p.Item1.Id, p.Item2.Double }).ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
        }

        idx.Should().Be(0);
    }

    [Fact]
    public async Task SelectJoinEntity_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.ComplexEntity.Join(_sut.SimpleEntity, (c, s) => c.Id == s.Id).Select(p => new Cls { Id = p.Item2.Id, Str = p.Item1.RequiredString }).ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
            row.Str.Should().NotBeNullOrEmpty();
        }

        idx.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TestJoinsTables()
    {
        var r = _sut.From("simple_entity").Join(_sut.From("complex_entity"), (s, c) => s["id"] == c["id"]).Select(p => new { Id = p.Item1["id"].AsInt, Str = p.Item2["someString"].AsString }).ToList();

        r.Should().NotBeNullOrEmpty();

        r.Count.Should().Be(3);
    }

    [Fact]
    public void TestJoinTableWithEntity()
    {
        var r = _sut.From("simple_entity").Join(_sut.ComplexEntity, (s, c) => s["id"].AsInt == c.Id).Select(p => new { Id = p.Item1["id"].AsInt, Str = p.Item2.String }).ToList();

        r.Should().NotBeNullOrEmpty();

        r.Count.Should().Be(3);
    }

    // Regression: a cached join query with a parameter is re-prepared on cache-hit to re-extract the
    // captured value. In parameter-extraction mode the columns provider is not populated, so resolving
    // table aliases used to throw InvalidOperationException on the second execution.
    [Fact]
    public void SelectJoinWithCapturedParam_RepeatedExecution_ShouldReturnData()
    {
        for (var i = 1; i <= 3; i++)
        {
            var id = i;
            var rows = _sut.SimpleEntity
                .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
                .Where(p => p.Item2.Id == id)
                .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
                .ToList();

            rows.Should().OnlyContain(r => r.Id == id);
            foreach (var row in rows)
                row.RequiredString.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void LeftJoin_ShouldReturnUnmatchedRowsWithNullRight()
    {
        var rows = _sut.SimpleEntity
            .LeftJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => new { LeftId = p.Item1.Id, RightString = p.Item2.RequiredString })
            .ToList();

        rows.Should().HaveCount(10);
        rows.Count(r => r.RightString is null).Should().Be(7);
        rows.Count(r => r.RightString is not null).Should().Be(3);
    }

    [Fact]
    public void RightJoin_ShouldReturnUnmatchedRowsWithNullLeft()
    {
        var rows = _sut.ComplexEntity
            .RightJoin(_sut.SimpleEntity, (c, s) => c.Id == s.Id)
            .Select(p => new { LeftString = p.Item1.RequiredString, RightId = p.Item2.Id })
            .ToList();

        rows.Should().HaveCount(10);
        rows.Count(r => r.LeftString is null).Should().Be(7);
        rows.Count(r => r.LeftString is not null).Should().Be(3);
    }

    [Fact]
    public void FullJoin_ShouldReturnBothSides()
    {
        Assert.SkipUnless(Provider.SupportsFullJoin, "This provider does not support FULL JOIN.");

        var rows = _sut.SimpleEntity
            .FullJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => new { LeftId = p.Item1.Id, RightString = p.Item2.RequiredString })
            .ToList();

        rows.Should().HaveCount(10);
        rows.Count(r => r.RightString is null).Should().Be(7);
    }

    [Fact]
    public void CrossJoin_ShouldReturnCartesianProduct()
    {
        var count = _sut.SimpleEntity.CrossJoin(_sut.ComplexEntity).Count();

        count.Should().Be(30);
    }

    [Fact]
    public void Join4Tables_ShouldReturnProjectedValues()
    {
        var rows = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Join(_sut.SimpleEntity, (p, s) => p.Item2.Id == s.Id)
            .Join(_sut.ComplexEntity, (p, c) => p.Item3.Id == c.Id)
            .Select(p => new { A = p.Item1.Id, B = p.Item2.RequiredString, C = p.Item3.Id, D = p.Item4.RequiredString })
            .ToList();

        // simple_entity has ids 1..10 and complex_entity 1..3, so the chain matches ids 1..3.
        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(r => r.A == r.C);
        rows.Should().OnlyContain(r => r.B == r.D);
        rows.Should().OnlyContain(r => !string.IsNullOrEmpty(r.B));
    }

    [Fact]
    public void DerivedSourceThenJoin_ShouldReturnData()
    {
        var derived = _sut.ComplexEntity.Select(c => new { c.Id, c.RequiredString });

        var rows = _sut.From(derived)
            .Where(d => d.Id > 1)
            .Join(_sut.SimpleEntity, (d, s) => d.Id == s.Id)
            .Select(p => new { LeftId = p.Item1.Id, SId = p.Item2.Id, p.Item1.RequiredString })
            .ToList();

        // complex_entity has ids 1..3; the filter keeps 2..3.
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.LeftId == r.SId && r.LeftId > 1);
        rows.Should().OnlyContain(r => !string.IsNullOrEmpty(r.RequiredString));
    }

    [Fact]
    public void DerivedSourceWithJoinThenJoin_ShouldReturnData()
    {
        // Regression: a derived source that itself contains a join used to resolve the outer join's
        // entity to the *inner* query's table alias (an out-of-scope alias), producing SQL that the
        // database rejects ("no such column"). The projected members themselves resolved correctly.
        var derived = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.RequiredString });

        var rows = _sut.From(derived)
            .Where(d => d.CustomerName != null)
            .Join(_sut.ComplexEntity, (d, c2) => d.OrderId == c2.Id)
            .Select(p => new { p.Item1.OrderId, p.Item1.CustomerName, Third = p.Item2.RequiredString })
            .ToList();

        // simple_entity has ids 1..10 and complex_entity 1..3, so the chain matches ids 1..3.
        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(r => r.Third == r.CustomerName);
    }
}
