using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Validation surface around two SQL-builder features that are only reachable through their guard
/// clauses: query tags rendered as block comments (the tag text must not be able to open a nested
/// comment or an executable comment) and the extreme-row source, which rejects every modifier its
/// derived-table lowering cannot express. Both run against a real SQLite database: the tag case
/// executes, the extreme-row cases are rejected before a command is sent.
/// </summary>
[Collection("Sqlite")]
public sealed class SqlBuilderGuardTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void Tag_WithLineBreaksAndCommentDelimiters_ShouldRenderOneLineComment()
    {
        _sut.SimpleEntity
            .WithTag("first\r\nsecond /* inner */ third\rfourth */ fifth")
            .Where(x => x.Id == 1)
            .Select(x => x.Id)
            .First()
            .Should().Be(1);
    }

    [Fact]
    public void Tag_WithExecutableCommentPrefix_ShouldStayAClientComment()
    {
        _sut.SimpleEntity
            .WithTag("! hint */")
            .Where(x => x.Id == 2)
            .Select(x => x.Id)
            .First()
            .Should().Be(2);
    }

    [Fact]
    public void ExtremeRow_WithPaging_ShouldThrow()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity.SelectWhereMax(x => x.Int, x => x.Id).Limit(1).ToList())
            .Should().Throw<BuildSqlCommandException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity.SelectWhereMin(x => x.Int, x => x.Id).Offset(1).ToList())
            .Should().Throw<BuildSqlCommandException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity.SelectWhereMax(x => x.Int, x => x.Id).Page(1, 1).ToList())
            .Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void ExtremeRow_WithGroupingOrHaving_ShouldThrow()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .GroupBy(x => x.Int)
                .SelectWhereMax(x => x.Int, x => x.Id)
                .ToList())
            .Should().Throw<BuildSqlCommandException>();

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .GroupBy(x => x.Int)
                .Having(g => SqlFunctions.Sql.count() > 0)
                .SelectWhereMax(x => x.Int, x => x.Id)
                .ToList())
            .Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void ExtremeRow_WithJoin_ShouldThrow()
    {
        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Join(_sut.SimpleEntity, (c, s) => c.Id == s.Id)
                .SelectWhereMax(x => x.Item1.Int, x => x.Item1.Id)
                .ToList())
            .Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void ExtremeRow_OverSubquerySource_ShouldThrow()
    {
        var ctx = _sut.DataProvider;
        var cte = ctx.From<IComplexEntity>().Select(x => new { x.Id, x.Int }).AsCte("extreme_source");

        FluentActions
            .Invoking(() => ctx.From(cte).SelectWhereMax(x => x.Int, x => x.Id).ToList())
            .Should().Throw<BuildSqlCommandException>();
    }
}
