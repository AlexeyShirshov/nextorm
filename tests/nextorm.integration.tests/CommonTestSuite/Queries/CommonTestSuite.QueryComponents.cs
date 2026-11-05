using FluentAssertions;
using NextORM.Core;
using NextORM.QueryComponents;

namespace NextORM.Integration.Tests;

public sealed class ExtremaProjectionDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public abstract partial class CommonTestSuite
{
    private EntityBuilder<ExtremaEntity> ExternalExtrema => _sut.DataProvider.From<ExtremaEntity>();

    private static void SameIds(IEnumerable<ExtremaEntity> builtIn, IEnumerable<ExtremaEntity> external)
        => external.Select(e => e.Id).OrderBy(id => id).Should().Equal(builtIn.Select(e => e.Id).OrderBy(id => id));

    [Fact]
    public void QueryComponents_MinOne_MaxAll_ShouldMatchBuiltIn()
    {
        var builtMin = ExternalExtrema.SelectWhereMinOld(e => e.Score).ToList();
        var externalMin = ExternalExtrema.ExternalSelectWhereMin(e => e.Score).ToList();
        externalMin.Select(e => e.Score).Should().Equal(builtMin.Select(e => e.Score));

        var builtMax = ExternalExtrema.SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All).ToList();
        var externalMax = ExternalExtrema.ExternalSelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        SameIds(builtMax, externalMax);
    }

    [Fact]
    public void QueryComponents_CompositeValue_ShouldMatchBuiltIn()
    {
        var built = ExternalExtrema.SelectWhereMinOld(e => new { e.Category, e.Score }).ToList();
        var external = ExternalExtrema.ExternalSelectWhereMin(e => new { e.Category, e.Score }).ToList();
        SameIds(built, external);

        var builtMax = ExternalExtrema.SelectWhereMaxOld(e => new { e.Category, e.Score }, ExtremeRowTies.All).ToList();
        var externalMax = ExternalExtrema.ExternalSelectWhereMax(e => new { e.Category, e.Score }, ExtremeRowTies.All).ToList();
        SameIds(builtMax, externalMax);
    }

    [Fact]
    public void QueryComponents_GroupedSingleAndComposite_ShouldMatchBuiltIn()
    {
        var built = ExternalExtrema.SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        var external = ExternalExtrema.ExternalSelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        SameIds(built, external);

        var builtComposite = ExternalExtrema.SelectWhereMinOld(e => e.Score, ExtremeRowTies.One, e => new { e.Category, e.Label }).ToList();
        var externalComposite = ExternalExtrema.ExternalSelectWhereMin(e => e.Score, ExtremeRowTies.One, e => new { e.Category, e.Label }).ToList();
        SameIds(builtComposite, externalComposite);
    }

    [Fact]
    public void QueryComponents_NullValueAndNullGroup_ShouldMatchBuiltIn()
    {
        var built = ExternalExtrema.SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        var external = ExternalExtrema.ExternalSelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        SameIds(built, external);
        external.Should().NotContain(e => e.Category == "d");
        external.Should().Contain(e => e.Category == null);
    }

    [Fact]
    public void QueryComponents_CapturedFilter_ShouldMatchBuiltInAndApplyBeforeRanking()
    {
        int threshold = 3;
        var built = ExternalExtrema.Where(e => e.Score > threshold).SelectWhereMinOld(e => e.Score).ToList();
        var external = ExternalExtrema.Where(e => e.Score > threshold).ExternalSelectWhereMin(e => e.Score).ToList();
        SameIds(built, external);
        external.Should().ContainSingle();
        external[0].Score.Should().Be(4);
    }

    [Fact]
    public void QueryComponents_Projection_ShouldMatchBuiltInAndHideRank()
    {
        var built = ExternalExtrema.SelectWhereMinOld(e => e.Score, e => new ExtremaProjectionDto { Id = e.Id, Label = e.Label }, ExtremeRowTies.All).ToList();
        var external = ExternalExtrema.ExternalSelectWhereMin(e => e.Score, e => new ExtremaProjectionDto { Id = e.Id, Label = e.Label }, ExtremeRowTies.All).ToList();
        external.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id).Should().Equal(built.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id));
    }

    [Fact]
    public void QueryComponents_OuterOrder_ShouldMatchBuiltIn()
    {
        var built = ExternalExtrema.SelectWhereMinOld(e => e.Score, ExtremeRowTies.All).OrderBy(e => e.Id).ThenBy(e => e.Label).ToList();
        var external = ExternalExtrema.ExternalSelectWhereMin(e => e.Score, ExtremeRowTies.All).OrderBy(e => e.Id).ThenBy(e => e.Label).ToList();
        external.Select(e => e.Id).Should().Equal(built.Select(e => e.Id));
    }

    [Fact]
    public void QueryComponents_Paging_ShouldMatchBuiltInRejection()
    {
        var built = Record.Exception(() => ExternalExtrema.SelectWhereMinOld(e => e.Score).Limit(1).ToList());
        var external = Record.Exception(() => ExternalExtrema.ExternalSelectWhereMin(e => e.Score).Limit(1).ToList());
        built.Should().NotBeNull();
        external.Should().NotBeNull();
        external!.GetType().Should().Be(built!.GetType());
    }

    [Fact]
    public void QueryComponents_SlotReplacement_AndIncompatibility()
    {
        var replaced = ExternalExtrema
            .ExternalSelectWhereMin(e => e.Score)
            .ExternalSelectWhereMax(e => e.Score, ExtremeRowTies.All)
            .ToList();
        var built = ExternalExtrema.SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All).ToList();
        SameIds(built, replaced);

        var conflicting = ExternalExtrema
            .WithComponent(new ExternalSelectWhereComponent<ExtremaEntity>(
                ExternalExtremeKind.Min,
                (System.Linq.Expressions.Expression<Func<ExtremaEntity, object?>>)(e => e.Score),
                ExtremeRowTies.One,
                null))
            .WithComponent(new ConflictingReshapeComponent<ExtremaEntity>());

        FluentActions.Invoking(() => conflicting.ToList())
            .Should().Throw<Exception>()
            .WithMessage("*exclusive row-source reshape*");
    }

    [Fact]
    public void QueryComponents_WarmCache_CapturedAndPositional_ShouldRebind()
    {
        int threshold = 3;
        var cmd = (QueryCommand<ExtremaEntity>)ExternalExtrema
            .Where(e => e.Score > threshold)
            .ExternalSelectWhereMin(e => e.Score);

        var first = _sut.DataProvider.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        _sut.DataProvider.ToList(first).Should().ContainSingle(e => e.Score == 4);

        threshold = 8;
        var second = _sut.DataProvider.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        _sut.DataProvider.ToList(second).Should().ContainSingle(e => e.Score == 9);

        var positional = (QueryCommand<ExtremaEntity>)ExternalExtrema
            .Where(e => e.Score > SqlFunctions.Parameter<int>(0))
            .ExternalSelectWhereMin(e => e.Score);
        var prepared = _sut.DataProvider.GetPreparedQueryCommand(positional, false, true, CancellationToken.None);
        ((DataContext)_sut.DataProvider).ToList(prepared, new object[] { 3 })
            .Should().ContainSingle(e => e.Score == 4);
        ((DataContext)_sut.DataProvider).ToList(prepared, new object[] { 8 })
            .Should().ContainSingle(e => e.Score == 9);
    }

    [Fact]
    public void QueryComponents_NoComponents_Control_ShouldNotUseRankArtefacts()
    {
        var prepared = (DbPreparedQueryCommand<ExtremaEntity>)_sut.DataProvider.GetPreparedQueryCommand(
            (QueryCommand<ExtremaEntity>)ExternalExtrema.Where(e => e.Category == "a"),
            false,
            true,
            CancellationToken.None);
        var sql = prepared.SqlStmt!;

        sql.Should().NotContainEquivalentOf("row_number");
        sql.Should().NotContainEquivalentOf("__nextorm_ext_rn");
        var rows = _sut.DataProvider.ToList(prepared);
        rows.Should().NotBeEmpty();
    }
}
