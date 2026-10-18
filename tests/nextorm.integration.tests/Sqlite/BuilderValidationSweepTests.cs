using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Builder-level validation against SQLite: paging/ordering guard clauses, the provider clauses the
/// dialect cannot render (<c>WITH TIES</c>, extreme-row selection, TABLESAMPLE) and the harmless
/// statement-level overrides (identifier quoting, keyword case, command timeout) executed for real.
/// </summary>
[Collection("Sqlite")]
public sealed class BuilderValidationSweepTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void PagingAndOrderingGuards_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.Where(x => x.Id > 0).Limit(-1).Select(x => x.Id).ToList())
            .Should().Throw<ArgumentOutOfRangeException>();

        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.Where(x => x.Id > 0).Offset(-1).Select(x => x.Id).ToList())
            .Should().Throw<ArgumentOutOfRangeException>();

        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.Select(x => x.Id).OrderBy(0).ToList())
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithTies_ShouldBeRejectedBySqlite()
    {
        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass
                .OrderBy(x => x.Id)
                .Limit(2)
                .WithTies()
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ExtremeRow_ShouldSelectTheMaxRow()
    {
        var ids = _sut.ComplexEntity
            .SelectWhereMax(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(3L);
    }

    [Fact]
    public void TableSample_ShouldBeRejectedBySqlite()
    {
        var ctx = _sut.DataProvider;

        FluentActions
            .Invoking(() => ctx.From("simple_entity", o => o.TableSample(50))
                .Select(t => t["id"].AsInt)
                .ToList())
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void StatementOverrides_ShouldExecute()
    {
        var ids = _sut.SimpleEntityAsClass
            .WithQuotedIdentifiers()
            .WithKeywordCase(KeywordCase.Lower)
            .WithCommandTimeout(5)
            .Where(x => x.Id > 8)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(9, 10);
    }
}
