using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    public sealed class CteNumberRow
    {
        public int n { get; set; }
    }

    [Fact]
    public void Cte_NonRecursive_FilteredAndJoinedWithTable_ShouldReturnData()
    {
        var complex = _sut.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });

        var rows = _sut.DataProvider
            .With("recent", complex)
            .From("recent")
            .Join(_sut.SimpleEntity, (c, s) => c["id"].AsInt == s.Id)
            .Where(p => p.Item2.Id > 0)
            .Select(p => new { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id })
            .ToList();

        // complex_entity has ids 1..3; filtering id > 1 leaves 2 and 3, which match simple_entity.
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Id == 2 || r.Id == 3);
        rows.Should().OnlyContain(r => r.SimpleId == r.Id);
    }

    [Fact]
    public void Cte_Recursive_ShouldProduceNumberSeries()
    {
        var ctx = _sut.DataProvider;

        var anchor = _sut.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.UnionAll(step);

        var rows = ctx
            .WithRecursive("nums", body)
            .From("nums")
            .Select(t => new CteNumberRow { n = t["n"].AsInt })
            .ToList();

        rows.Select(r => r.n).OrderBy(n => n).Should().Equal(1, 2, 3, 4, 5);
    }

    /// <summary>
    /// A CTE whose body is itself a query carrying a CTE (a query built as <c>With(...).From(...)</c>) must
    /// be hoisted into one top-level <c>WITH</c> and execute against every provider, including SQL Server,
    /// whose T-SQL forbids a <c>WITH</c> nested inside a derived table.
    /// </summary>
    [Fact]
    public void Cte_Nested_ShouldHoistAndReturnData()
    {
        var ctx = _sut.DataProvider;

        var inner = ctx.With("i", _sut.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id }))
            .From("i")
            .Select(t => new { id = t["id"].AsInt });

        var rows = ctx
            .With("o", inner)
            .From("o")
            .Select(t => new { id = t["id"].AsInt })
            .ToList();

        // complex_entity has ids 1..3; the inner CTE keeps 2 and 3 and the outer one passes them through.
        rows.Select(r => r.id).OrderBy(id => id).Should().Equal(2, 3);
    }
}
