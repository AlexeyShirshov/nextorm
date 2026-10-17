using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Behavioural coverage for issue #115 <c>SelectWhereMax</c>/<c>SelectWhereMin</c> against every shared
/// provider. The seeded <c>extrema_entity</c> is deterministic:
/// <list type="bullet">
/// <item><c>a</c>: scores null, 5, 9, 9 (tie on the maximum);</item>
/// <item><c>b</c>: scores 3, 1, 1 (tie on the minimum);</item>
/// <item><c>null</c>: scores null, 7 (the null key is its own group);</item>
/// <item><c>c</c>: score 4 (singleton);</item>
/// <item><c>d</c>: score null (a group whose comparison value is always null, so it yields no row).</item>
/// </list>
/// Null comparison values are ignored and a group with only null comparison values contributes no rows.
/// </summary>
[SqlTable("extrema_entity")]
public sealed class ExtremaEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>The nullable value whose extremum selects the row.</summary>
    [Column("score")]
    public int? Score { get; set; }

    /// <summary>The nullable grouping key; <c>null</c> is its own group.</summary>
    [Column("category")]
    public string? Category { get; set; }

    [Column("label")]
    public string Label { get; set; } = string.Empty;
}

public abstract partial class CommonTestSuite
{
    private EntityBuilder<ExtremaEntity> Extrema => _sut.DataProvider.From<ExtremaEntity>();

    // --- global extremum, whole row ---------------------------------------------------------

    [Fact]
    public void SelectWhereExtrema_Max_GlobalOne_ShouldReturnTheWholeExtremeRow()
    {
        var rows = Extrema.SelectWhereMax(e => e.Score).ToList();

        rows.Should().ContainSingle();
        rows[0].Score.Should().Be(9);
        rows[0].Id.Should().BeOneOf(3, 4);
        (rows[0].Id, rows[0].Label).Should().BeOneOf((3, "three"), (4, "four"));
    }

    [Fact]
    public void SelectWhereExtrema_Max_GlobalAll_ShouldReturnEveryTiedRow()
    {
        var rows = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Id).Should().BeEquivalentTo([3, 4]);
        rows.Should().OnlyContain(r => r.Score == 9);
    }

    [Fact]
    public void SelectWhereExtrema_Min_GlobalOne_ShouldReturnTheWholeExtremeRow()
    {
        var rows = Extrema.SelectWhereMin(e => e.Score).ToList();

        rows.Should().ContainSingle();
        rows[0].Score.Should().Be(1);
        rows[0].Id.Should().BeOneOf(6, 10);
        (rows[0].Id, rows[0].Label).Should().BeOneOf((6, "six"), (10, "ten"));
    }

    [Fact]
    public void SelectWhereExtrema_Min_GlobalAll_ShouldReturnEveryTiedRow()
    {
        var rows = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All).ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Id).Should().BeEquivalentTo([6, 10]);
        rows.Should().OnlyContain(r => r.Score == 1);
    }

    // --- grouped extremum -------------------------------------------------------------------

    [Fact]
    public void SelectWhereExtrema_Max_GroupedOne_ShouldReturnOneWinnerPerGroup()
    {
        var rows = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.One, e => e.Category).ToList();

        // a, b, null and c each contribute a winner; the all-null group d contributes none.
        rows.Should().HaveCount(4);
        rows.Should().NotContain(r => r.Category == "d");

        rows.Single(r => r.Category == "b").Id.Should().Be(5);
        rows.Single(r => r.Category == "c").Id.Should().Be(9);
        rows.Single(r => r.Category == null).Id.Should().Be(8);

        var winnerA = rows.Single(r => r.Category == "a");
        winnerA.Score.Should().Be(9);
        winnerA.Id.Should().BeOneOf(3, 4);
    }

    [Fact]
    public void SelectWhereExtrema_Max_GroupedAll_ShouldReturnEveryTiedRowPerGroup()
    {
        var rows = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();

        rows.Should().HaveCount(5);
        rows.Should().NotContain(r => r.Category == "d");

        rows.Where(r => r.Category == "a").Select(r => r.Id).Should().BeEquivalentTo([3, 4]);
        rows.Where(r => r.Category == "b").Select(r => r.Id).Should().BeEquivalentTo([5]);
        rows.Where(r => r.Category == "c").Select(r => r.Id).Should().BeEquivalentTo([9]);
        rows.Where(r => r.Category == null).Select(r => r.Id).Should().BeEquivalentTo([8]);
    }

    [Fact]
    public void SelectWhereExtrema_Min_GroupedAll_ShouldReturnEveryTiedRowPerGroup()
    {
        var rows = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();

        // a keeps its minimum non-null row, b its two tied minima, and the null and c groups one each;
        // the all-null group d contributes none.
        rows.Should().HaveCount(5);
        rows.Should().NotContain(r => r.Category == "d");

        rows.Where(r => r.Category == "a").Select(r => r.Id).Should().BeEquivalentTo([2]);
        rows.Where(r => r.Category == "b").Select(r => r.Id).Should().BeEquivalentTo([6, 10]);
        rows.Where(r => r.Category == "c").Select(r => r.Id).Should().BeEquivalentTo([9]);
        rows.Where(r => r.Category == null).Select(r => r.Id).Should().BeEquivalentTo([8]);
    }

    // --- projection form --------------------------------------------------------------------

    [Fact]
    public void SelectWhereExtrema_Max_Projection_ShouldProjectTheExtremeRow()
    {
        var rows = Extrema.SelectWhereMax(e => e.Score, e => new { e.Id, e.Label }).ToList();

        rows.Should().ContainSingle();
        (rows[0].Id, rows[0].Label).Should().BeOneOf((3, "three"), (4, "four"));
    }

    [Fact]
    public void SelectWhereExtrema_Min_Projection_ShouldProjectTheExtremeRow()
    {
        var rows = Extrema.SelectWhereMin(e => e.Score, e => new { e.Id, e.Label }).ToList();

        rows.Should().ContainSingle();
        (rows[0].Id, rows[0].Label).Should().BeOneOf((6, "six"), (10, "ten"));
    }

    // --- composite value selector ------------------------------------------------------------

    [Fact]
    public void SelectWhereExtrema_CompositeValueSelector_ShouldOrderComponentWise()
    {
        // The value is the tuple (category, score): the SQL lowering filters every component IS NOT NULL
        // and orders by category then score, so the maximum is (c, 4) and the minimum is (a, 5). Ordering
        // by score alone would give 9 and 1 instead, and any null component would exclude the row.
        var max = Extrema.SelectWhereMax(e => new { e.Category, e.Score }).ToList();
        max.Should().ContainSingle().Which.Id.Should().Be(9);

        var min = Extrema.SelectWhereMin(e => new { e.Category, e.Score }).ToList();
        min.Should().ContainSingle().Which.Id.Should().Be(2);
    }

    [Fact]
    public void SelectWhereExtrema_CompositeValueSelector_All_ShouldBreakTiesComponentWise()
    {
        // score ties (9 for ids 3 and 4, 1 for ids 6 and 10); the secondary label component decides the
        // extremum, so All keeps exactly one row rather than both.
        var max = Extrema.SelectWhereMax(e => new { e.Score, e.Label }, ExtremeRowTies.All).ToList();
        max.Should().ContainSingle().Which.Id.Should().Be(3);

        var min = Extrema.SelectWhereMin(e => new { e.Score, e.Label }, ExtremeRowTies.All).ToList();
        min.Should().ContainSingle().Which.Id.Should().Be(6);
    }

    // --- null semantics ---------------------------------------------------------------------

    [Fact]
    public void SelectWhereExtrema_Max_NullComparisonValues_ShouldBeIgnored()
    {
        var global = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        global.Select(r => r.Id).Should().BeEquivalentTo([3, 4]);

        // Group d has only null comparison values, so it must not surface in the grouped result.
        var grouped = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();
        grouped.Should().NotContain(r => r.Category == "d");
    }

    [Fact]
    public void SelectWhereExtrema_Min_NullComparisonValues_ShouldBeIgnored()
    {
        var global = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All).ToList();
        global.Select(r => r.Id).Should().BeEquivalentTo([6, 10]);
    }

    [Fact]
    public void SelectWhereExtrema_Max_NullGroupKey_ShouldBeItsOwnGroup()
    {
        var rows = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();

        // The null key groups ids 7 (null score, ignored) and 8 (score 7).
        rows.Where(r => r.Category == null).Select(r => r.Id).Should().Equal(8);
        rows.Where(r => r.Category == "a").Should().HaveCount(2);
    }

    // --- empty source -----------------------------------------------------------------------

    [Fact]
    public void SelectWhereExtrema_EmptySource_ShouldYieldNoRows()
    {
        var global = Extrema.Where(e => e.Category == "no-such").SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();
        global.Should().BeEmpty();

        var grouped = Extrema.Where(e => e.Category == "no-such")
            .SelectWhereMax(e => e.Score, ExtremeRowTies.One, e => e.Category)
            .ToList();
        grouped.Should().BeEmpty();
    }
}
