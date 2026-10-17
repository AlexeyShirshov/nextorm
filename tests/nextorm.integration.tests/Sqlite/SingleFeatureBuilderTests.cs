using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Single-feature builder sweep against SQLite. The composition-state checks are short-circuiting
/// OR chains, so a query that sets many features at once only exercises the first arm; every test
/// below builds a query with exactly one feature enabled, which is what walks the remaining arms
/// of the state, source-resolution and render-mode checks.
/// </summary>
[Collection("Sqlite")]
public sealed class SingleFeatureBuilderTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void SingleFeatureQueries2_ShouldExecute()
    {
        _sut.ComplexEntity.OrderBy(x => x.Id).Offset(1).Select(x => x.Id).First().Should().Be(2);
        _sut.ComplexEntity.OrderBy(x => x.Id).Page(1, 1).Select(x => x.Id).First().Should().Be(2);
        _sut.ComplexEntity.Select(x => new { x.Id }).ToList().Should().HaveCount(3);
        _sut.ComplexEntity.Select(x => x.Id).ToList().Should().HaveCount(3);
        _sut.SimpleEntityAsClass.ToList().Should().HaveCount(10);
    }

    [Fact]
    public void SingleFeatureQueries_ShouldExecute()
    {
        _sut.ComplexEntity.Where(x => x.Id == 1).Select(x => x.Id).First().Should().Be(1);
        _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Id).First().Should().Be(1);
        _sut.ComplexEntity.OrderByDescending(x => x.Id).Select(x => x.Id).First().Should().Be(3);
        _sut.ComplexEntity.GroupBy(x => x.Int).Select(g => new { g.Int, C = SqlFunctions.Sql.count() }).ToList().Should().NotBeEmpty();
        _sut.ComplexEntity.Select(x => x.Int).Distinct().ToList().Should().NotBeEmpty();
        _sut.ComplexEntity.OrderBy(x => x.Id).Limit(1).Select(x => x.Id).First().Should().Be(1);
    }

    [Fact]
    public void JoinOnlyQueries_ShouldExecute()
    {
        var joined = _sut.ComplexEntity
            .Join(_sut.SimpleEntity, (c, s) => c.Id == s.Id)
            .Select(x => new { x.Item1.Id, Sid = x.Item2.Id })
            .ToList();
        joined.Should().HaveCount(3);

        var left = _sut.SimpleEntity
            .LeftJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(x => new { Sid = x.Item1.Id, Cid = (long?)x.Item2.Id })
            .ToList();
        left.Should().HaveCount(10);

        var cross = _sut.SimpleEntity
            .CrossJoin(_sut.ComplexEntity)
            .Select(x => new { Outer = x.Item1.Id, Inner = x.Item2.Id })
            .ToList();
        cross.Should().HaveCount(30);
    }

    [Fact]
    public void SourceOverrideQueries_ShouldExecute()
    {
        _sut.SimpleEntity.WithTag("only-tag").Select(x => x.Id).ToList().Should().HaveCount(10);
        _sut.SimpleEntity.WithCommandTimeout(1).Select(x => x.Id).ToList().Should().HaveCount(10);
        _sut.SimpleEntity.WithQuotedIdentifiers(true).Select(x => x.Id).ToList().Should().HaveCount(10);
        _sut.SimpleEntity.WithKeywordCase(KeywordCase.Upper).Select(x => x.Id).ToList().Should().HaveCount(10);
        _sut.SimpleEntity.WithUppercaseKeywords(false).Select(x => x.Id).ToList().Should().HaveCount(10);
        _sut.SimpleEntity.WithTableName("simple_entity").Select(x => x.Id).ToList().Should().HaveCount(10);
        _sut.ComplexEntity.IgnoreFilters().Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    [Fact]
    public void SetOperationOnlyQueries_ShouldExecute()
    {
        var union = _sut.SimpleEntity.Select(x => x.Id).Union(_sut.SimpleEntity.Where(x => x.Id <= 4).Select(x => x.Id));
        _sut.From(union).Count().Should().Be(10);

        var except = _sut.SimpleEntity.Select(x => x.Id).Except(_sut.ComplexEntity.Select(x => (int)x.Id));
        _sut.From(except).Count().Should().Be(7);

        var intersect = _sut.SimpleEntity.Select(x => x.Id).Intersect(_sut.ComplexEntity.Select(x => (int)x.Id));
        _sut.From(intersect).Count().Should().Be(3);
    }

    [Fact]
    public void CteOnlyQuery_ShouldExecute()
    {
        var ctx = _sut.DataProvider;
        var cte = ctx.From<IComplexEntity>().Select(x => new { x.Id }).AsCte("only_cte");

        ctx.From(cte).Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    [Fact]
    public void ExtremeRowQueries_ShouldExecute()
    {
        var max = _sut.ComplexEntity.SelectWhereMax(x => x.Int, x => x.Id).ToList().First();
        max.Should().BeGreaterThan(0);

        var min = _sut.ComplexEntity.SelectWhereMin(x => x.Int, x => x.Id).ToList().First();
        min.Should().BeGreaterThan(0);
    }
}
