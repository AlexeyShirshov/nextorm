using FluentAssertions;
using NextORM.Core;
using System.Linq.Expressions;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void TableAliasBuilder_ShouldJoinAndChain()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");

        var rows = simple
            .Where(x => x["id"].AsInt > 0)
            .Where(x => x["id"].AsInt <= 10)
            .Join(complex, (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { A = p.Item1["id"].AsInt, B = p.Item2["id"].AsInt })
            .ToList();

        rows.Should().HaveCount(3);

        simple.LeftJoin(complex, (s, c) => s["id"].AsInt == c["id"].AsInt)
            .Select(p => new { A = p.Item1["id"].AsInt })
            .ToList()
            .Should().HaveCount(10);

        simple.CrossJoin(complex)
            .Select(p => new { A = p.Item1["id"].AsInt })
            .ToList()
            .Should().HaveCount(30);
    }

    [Fact]
    public void TableAliasBuilder_ShouldSupportJoinVariants()
    {
        var simple = _sut.From("simple_entity");
        var complex = _sut.From("complex_entity");
        Expression<Func<TableAlias, TableAlias, bool>> on = (s, c) => s["id"].AsInt == c["id"].AsInt;

        _ = simple.RightJoin(complex, on).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.FullJoin(complex, on).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.CrossApply(complex).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.OuterApply(complex).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.SemiJoin(complex, on).Select(x => new { A = x["id"].AsInt });
        _ = simple.AntiJoin(complex, on).Select(x => new { A = x["id"].AsInt });
        _ = simple.PasteJoin(complex).Select(p => new { A = p.Item1["id"].AsInt });

        var complexEntity = _sut.ComplexEntity;

        _ = simple.Join(complexEntity, (s, c) => s["id"].AsInt == c.Id).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.LeftJoin(complexEntity, (s, c) => s["id"].AsInt == c.Id).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.RightJoin(complexEntity, (s, c) => s["id"].AsInt == c.Id).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.FullJoin(complexEntity, (s, c) => s["id"].AsInt == c.Id).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.CrossJoin(complexEntity).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.CrossApply(complexEntity).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.OuterApply(complexEntity).Select(p => new { A = p.Item1["id"].AsInt });
        _ = simple.SemiJoin(complexEntity, (s, c) => s["id"].AsInt == c.Id).Select(x => new { A = x["id"].AsInt });
        _ = simple.AntiJoin(complexEntity, (s, c) => s["id"].AsInt == c.Id).Select(x => new { A = x["id"].AsInt });
        _ = simple.PasteJoin(complexEntity).Select(p => new { A = p.Item1["id"].AsInt });
    }

    [Fact]
    public void TableAliasBuilder_ShouldApplyQueryOverrides()
    {
        var rows = _sut.From("simple_entity")
            .WithQuotedIdentifiers()
            .WithUppercaseKeywords()
            .WithKeywordCase(KeywordCase.Upper)
            .WithNamingConvention(SnakeCaseNamingConvention.Instance)
            .Where(x => x["id"].AsInt > 0)
            .Select(x => new { A = x["id"].AsInt })
            .ToList();

        rows.Should().HaveCount(10);
    }
}
