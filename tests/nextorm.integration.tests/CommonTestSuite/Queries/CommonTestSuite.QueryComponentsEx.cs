using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    private EntityBuilder<ExtremaEntity> ExExtrema => _sut.DataProvider.From<ExtremaEntity>();

    private static void SameExIds(IEnumerable<ExtremaEntity> builtIn, IEnumerable<ExtremaEntity> external)
        => external.Select(e => e.Id).OrderBy(id => id).Should().Equal(builtIn.Select(e => e.Id).OrderBy(id => id));

    [Fact]
    public void QueryComponentsEx_MinOne_ScalarGlobal_Parity()
    {
        var built = Extrema.SelectWhereMin(e => e.Score).ToList();
        var ex = ExExtrema.SelectWhereMinEx(e => e.Score).ToList();

        // Global minimum (score 1) is tied between ids 6 and 10; row_number() over distinct physical
        // plans may pick either tied row, so tolerate the tie exactly as the legacy tests do.
        built.Should().ContainSingle();
        ex.Should().ContainSingle();
        built[0].Score.Should().Be(1);
        ex[0].Score.Should().Be(1);
        built[0].Id.Should().BeOneOf(6, 10);
        ex[0].Id.Should().BeOneOf(6, 10);
    }

    [Fact]
    public void QueryComponentsEx_MaxAll_ScalarGlobal_Parity()
    {
        var built = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        var ex = ExExtrema.SelectWhereMaxEx(e => e.Score, ExtremeRowTies.All).ToList();

        ex.Select(e => e.Id).OrderBy(x => x).Should().Equal(built.Select(e => e.Id).OrderBy(x => x));
    }

    [Fact]
    public void QueryComponentsEx_GroupedScalar_Parity()
    {
        var builtMin = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        var exMin = ExExtrema.SelectWhereMinEx(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        SameExIds(builtMin, exMin);

        var builtMax = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.One, e => e.Category).ToList();
        var exMax = ExExtrema.SelectWhereMaxEx(e => e.Score, ExtremeRowTies.One, e => e.Category).ToList();
        // Group 'a' has a tied maximum (ids 3 and 4); row_number() may pick either tied row, so the
        // winner for 'a' is tolerated as BeOneOf exactly as the legacy tests do.
        exMax.Should().HaveCount(builtMax.Count);
        exMax.Select(e => e.Category).OrderBy(x => x).Should().Equal(builtMax.Select(e => e.Category).OrderBy(x => x));
        foreach (var g in exMax.Where(e => e.Category != "a"))
        {
            var b = builtMax.Single(e => e.Category == g.Category);
            g.Id.Should().Be(b.Id);
            g.Score.Should().Be(b.Score);
        }
        exMax.Single(e => e.Category == "a").Score.Should().Be(9);
        exMax.Single(e => e.Category == "a").Id.Should().BeOneOf(3, 4);
        builtMax.Single(e => e.Category == "a").Id.Should().BeOneOf(3, 4);
    }

    [Fact]
    public void QueryComponentsEx_CompositeAnonymous_GlobalAndGrouped_Parity()
    {
        var builtG = Extrema.SelectWhereMin(e => new { e.Category, e.Score }).ToList();
        var exG = ExExtrema.SelectWhereMinEx(e => new { e.Category, e.Score }).ToList();
        SameExIds(builtG, exG);

        var builtMax = Extrema.SelectWhereMax(e => new { e.Category, e.Score }, ExtremeRowTies.All, e => e.Category).ToList();
        var exMax = ExExtrema.SelectWhereMaxEx(e => new { e.Category, e.Score }, ExtremeRowTies.All, e => e.Category).ToList();
        SameExIds(builtMax, exMax);
    }

    [Fact]
    public void QueryComponentsEx_CompositeValueTuple_GlobalAndGrouped_Parity()
    {
        var builtG = Extrema.SelectWhereMin(e => new ValueTuple<int?, string?>(e.Score, e.Label)).ToList();
        var exG = ExExtrema.SelectWhereMinEx(e => new ValueTuple<int?, string?>(e.Score, e.Label)).ToList();
        SameExIds(builtG, exG);

        var builtMax = Extrema.SelectWhereMax(e => new ValueTuple<int?, string?>(e.Score, e.Label), ExtremeRowTies.All, e => new { e.Category, e.Label }).ToList();
        var exMax = ExExtrema.SelectWhereMaxEx(e => new ValueTuple<int?, string?>(e.Score, e.Label), ExtremeRowTies.All, e => new { e.Category, e.Label }).ToList();
        SameExIds(builtMax, exMax);
    }

    [Fact]
    public void QueryComponentsEx_NullValueKey_Excluded_Parity()
    {
        var built = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        var ex = ExExtrema.SelectWhereMaxEx(e => e.Score, ExtremeRowTies.All).ToList();

        ex.Should().NotContain(e => e.Score == null);
        SameExIds(built, ex);
    }

    [Fact]
    public void QueryComponentsEx_NullGroupKey_Parity()
    {
        var built = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        var ex = ExExtrema.SelectWhereMaxEx(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();

        SameExIds(built, ex);
        ex.Should().Contain(e => e.Category == null);
    }

    [Fact]
    public void QueryComponentsEx_CapturedFilter_BeforeExtremum_Parity()
    {
        int threshold = 3;
        var built = Extrema.Where(e => e.Score > threshold).SelectWhereMin(e => e.Score).ToList();
        var ex = ExExtrema.Where(e => e.Score > threshold).SelectWhereMinEx(e => e.Score).ToList();

        ex.Select(e => e.Id).Should().Equal(built.Select(e => e.Id));
        ex.Should().ContainSingle();
        ex[0].Score.Should().Be(4);

        threshold = 8;
        var built2 = Extrema.Where(e => e.Score > threshold).SelectWhereMin(e => e.Score).ToList();
        var ex2 = ExExtrema.Where(e => e.Score > threshold).SelectWhereMinEx(e => e.Score).ToList();
        // score 9 is tied between ids 3 and 4 after the filter; tolerate the tie as the legacy tests do.
        built2.Should().ContainSingle();
        ex2.Should().ContainSingle();
        built2[0].Score.Should().Be(9);
        ex2[0].Score.Should().Be(9);
        built2[0].Id.Should().BeOneOf(3, 4);
        ex2[0].Id.Should().BeOneOf(3, 4);
    }

    [Fact]
    public void QueryComponentsEx_ScalarProjection_Parity()
    {
        var built = Extrema.SelectWhereMin(e => e.Score, e => e.Score, ExtremeRowTies.All).ToList();
        var ex = ExExtrema.SelectWhereMinEx(e => e.Score, e => e.Score, ExtremeRowTies.All).ToList();

        ex.OrderBy(x => x).Should().Equal(built.OrderBy(x => x));
    }

    [Fact]
    public void QueryComponentsEx_DtoProjection_Parity()
    {
        var built = Extrema.SelectWhereMin(e => e.Score, e => new ExtremaProjectionDto { Id = e.Id, Label = e.Label }, ExtremeRowTies.All).ToList();
        var ex = ExExtrema.SelectWhereMinEx(e => e.Score, e => new ExtremaProjectionDto { Id = e.Id, Label = e.Label }, ExtremeRowTies.All).ToList();

        ex.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id).Should().Equal(built.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id));
    }

    [Fact]
    public void QueryComponentsEx_ChainedSelect_Parity()
    {
        var built = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All).Select(e => new { e.Id, e.Label }).ToList();
        var ex = ExExtrema.SelectWhereMinEx(e => e.Score, ExtremeRowTies.All).Select(e => new { e.Id, e.Label }).ToList();

        ex.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id).Should().Equal(built.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id));
    }

    [Fact]
    public void QueryComponentsEx_OuterOrderBy_Parity()
    {
        var built = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All).OrderBy(e => e.Id).ThenBy(e => e.Label).ToList();
        var ex = ExExtrema.SelectWhereMinEx(e => e.Score, ExtremeRowTies.All).OrderBy(e => e.Id).ThenBy(e => e.Label).ToList();

        ex.Select(e => e.Id).Should().Equal(built.Select(e => e.Id));
    }

    [Fact]
    public void QueryComponentsEx_PagingRejection_Parity()
    {
        var built = Record.Exception(() => Extrema.SelectWhereMin(e => e.Score).Limit(1).ToList());
        var ex = Record.Exception(() => ExExtrema.SelectWhereMinEx(e => e.Score).Limit(1).ToList());

        built.Should().NotBeNull();
        ex.Should().NotBeNull();
        ex!.GetType().Should().Be(built!.GetType());
    }

    [Fact]
    public void QueryComponentsEx_DistinctRejection_Parity()
    {
        Exception? builtError = null;
        Exception? exError = null;
        try { Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All).Distinct().Select(e => e.Id).ToList(); }
        catch (Exception e) { builtError = e; }
        try { ExExtrema.SelectWhereMinEx(e => e.Score, ExtremeRowTies.All).Distinct().Select(e => e.Id).ToList(); }
        catch (Exception e) { exError = e; }

        (exError is null).Should().Be(builtError is null);
        if (builtError is not null)
            exError!.GetType().Should().Be(builtError.GetType());
        else
        {
            // both succeeded; sanity: non-empty
        }
    }

    [Fact]
    public void QueryComponentsEx_SlotReplacement_MinToMax_Parity()
    {
        var replaced = ExExtrema
            .SelectWhereMinEx(e => e.Score)
            .SelectWhereMaxEx(e => e.Score, ExtremeRowTies.All)
            .ToList();
        var built = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();

        SameExIds(built, replaced);
    }
}
