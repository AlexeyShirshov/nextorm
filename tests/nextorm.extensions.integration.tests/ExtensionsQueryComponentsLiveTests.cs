using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Live behaviour of the <c>nextorm.extensions</c> operators on every SQL provider, asserted against the
/// pinned <c>extrema_entity</c> fixture rather than against another implementation. The fixture is the same
/// on every provider:
/// <code>
/// id score category label
///  1  NULL  a        one
///  2     5  a        two
///  3     9  a        three
///  4     9  a        four
///  5     3  b        five
///  6     1  b        six
///  7  NULL  NULL     seven
///  8     7  NULL     eight
///  9     4  c        nine
/// 10     1  b        ten
/// 11  NULL  d        eleven
/// </code>
/// A NULL value key never wins an extreme, so ids 1, 7 and 11 are excluded from every value comparison.
/// </summary>
public abstract class ExtensionsQueryComponentsLiveTests : ProviderTestSuite
{
    private EntityBuilder<ExtremaEntity> Extrema => _sut.DataProvider.From<ExtremaEntity>();

    private static int[] Ids(IEnumerable<ExtremaEntity> rows) => rows.Select(e => e.Id).OrderBy(id => id).ToArray();

    [Fact]
    public void MinOne_Scalar_Global_KeepsOneOfTheTiedMinimumRows()
    {
        var rows = Extrema.SelectWhereMin(e => e.Score).ToList();

        rows.Should().ContainSingle();
        rows[0].Score.Should().Be(1);
        rows[0].Id.Should().BeOneOf(6, 10);
    }

    [Fact]
    public void MaxAll_Scalar_Global_KeepsEveryTiedMaximumRow()
    {
        var rows = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();

        Ids(rows).Should().Equal(3, 4);
        rows.Should().OnlyContain(e => e.Score == 9);
    }

    [Fact]
    public void GroupedScalar_All_KeepsEveryTiedWinnerPerGroup()
    {
        // Categories: a → 5 (id 2); b → 1 tied (6, 10); c → 4 (9); d → only a NULL score, excluded;
        // NULL → 7 (id 8, id 7 has a NULL score).
        var rows = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();

        Ids(rows).Should().Equal(2, 6, 8, 9, 10);
    }

    [Fact]
    public void GroupedScalar_One_KeepsOneRowPerGroup()
    {
        // a → 9 tied between 3 and 4; b → 3 (id 5); c → 4 (id 9); NULL → 7 (id 8); d has no non-NULL score.
        var rows = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.One, e => e.Category).ToList();

        rows.Should().HaveCount(4);
        Ids(rows.Where(e => e.Category != "a")).Should().Equal(5, 8, 9);
        rows.Single(e => e.Category == "a").Id.Should().BeOneOf(3, 4);
        rows.Single(e => e.Category == "a").Score.Should().Be(9);
    }

    [Fact]
    public void CompositeKey_Global_ComparesLexicographically()
    {
        // (1,'six') beats (1,'ten') on the label; (9,'three') beats (9,'four') on the maximum side.
        var min = Extrema.SelectWhereMin(e => new { e.Score, e.Label }).ToList();
        min.Should().ContainSingle();
        min[0].Id.Should().Be(6);
        min[0].Label.Should().Be("six");

        var max = Extrema.SelectWhereMax(e => new { e.Score, e.Label }).ToList();
        max.Should().ContainSingle();
        max[0].Id.Should().Be(3);
        max[0].Label.Should().Be("three");
    }

    [Fact]
    public void CompositeKey_Grouped_ComparesLexicographicallyPerGroup()
    {
        var rows = Extrema
            .SelectWhereMax(e => new { e.Score, e.Label }, ExtremeRowTies.All, e => e.Category).ToList();

        // a → (9,'three') beats (9,'four'); b → (3,'five'); c → (4,'nine'); NULL → (7,'eight');
        // d has only a NULL score and never wins.
        Ids(rows).Should().Equal(3, 5, 8, 9);
    }

    [Fact]
    public void ValueTupleKey_Global_ComparesLexicographically()
    {
        var min = Extrema
            .SelectWhereMin(e => new ValueTuple<int?, string?>(e.Score, e.Label)).ToList();
        min.Should().ContainSingle().Which.Id.Should().Be(6);

        var max = Extrema
            .SelectWhereMax(e => new ValueTuple<int?, string?>(e.Score, e.Label)).ToList();
        max.Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Fact]
    public void NullValueKeys_AreExcluded()
    {
        var rows = Extrema.SelectWhereMax(e => e.Score, ExtremeRowTies.All).ToList();

        rows.Should().NotContain(e => e.Score == null);
        Ids(rows).Should().Equal(3, 4);
    }

    [Fact]
    public void NullGroupKey_IsItsOwnGroup()
    {
        var rows = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All, e => e.Category).ToList();

        rows.Should().Contain(e => e.Category == null);
        Ids(rows.Where(e => e.Category == null)).Should().Equal(8);
    }

    [Fact]
    public void CapturedFilter_IsAppliedBeforeTheExtremum()
    {
        int threshold = 3;
        var first = Extrema.Where(e => e.Score > threshold).SelectWhereMin(e => e.Score).ToList();
        first.Should().ContainSingle();
        first[0].Score.Should().Be(4);
        first[0].Id.Should().Be(9);

        threshold = 8;
        var second = Extrema.Where(e => e.Score > threshold).SelectWhereMin(e => e.Score).ToList();
        second.Should().ContainSingle();
        second[0].Score.Should().Be(9);
        second[0].Id.Should().BeOneOf(3, 4);
    }

    [Fact]
    public void ScalarProjection_ReturnsTheWinningValues()
    {
        var scores = Extrema.SelectWhereMin(e => e.Score, e => e.Score, ExtremeRowTies.All).ToList();

        scores.OrderBy(x => x).Should().Equal(1, 1);
    }

    [Fact]
    public void DtoProjection_ReturnsTheWinningRows()
    {
        var rows = Extrema
            .SelectWhereMin(e => e.Score, e => new ExtremaProjectionDto { Id = e.Id, Label = e.Label }, ExtremeRowTies.All)
            .ToList();

        rows.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id).Should().Equal((6, "six"), (10, "ten"));
    }

    [Fact]
    public void ChainedSelect_KeepsTheWinningRows()
    {
        var rows = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All)
            .Select(e => new { e.Id, e.Label }).ToList();

        rows.Select(x => (x.Id, x.Label)).OrderBy(x => x.Id).Should().Equal((6, "six"), (10, "ten"));
    }

    [Fact]
    public void OuterOrdering_OrdersTheSurvivingRows()
    {
        var rows = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All)
            .OrderBy(e => e.Id).ThenBy(e => e.Label).ToList();

        Ids(rows).Should().Equal(6, 10);
    }

    [Fact]
    public void Paging_IsRejected()
    {
        Action act = () => _ = Extrema.SelectWhereMin(e => e.Score).Limit(1).ToList();

        act.Should().Throw<BuildSqlCommandException>();
    }

    [Fact]
    public void DistinctOverTheReshape_KeepsTheWinningRows()
    {
        // DISTINCT is expressible on top of the reshape: the derived source already yields the winning
        // rows, so the outer DISTINCT cannot change the set.
        var rows = Extrema.SelectWhereMin(e => e.Score, ExtremeRowTies.All)
            .Distinct().Select(e => e.Id).ToList();

        rows.OrderBy(x => x).Should().Equal(6, 10);
    }

    [Fact]
    public void SlotReplacement_MinToMax_ReplacesInPlace()
    {
        var rows = Extrema
            .SelectWhereMin(e => e.Score)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All)
            .ToList();

        Ids(rows).Should().Equal(3, 4);
    }
}

/// <summary>Runs the extensions live suite against PostgreSQL.</summary>
[Collection("Query plan cache")]
public sealed class PostgresExtensionsQueryComponentsTests : ExtensionsQueryComponentsLiveTests
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;
}

/// <summary>Runs the extensions live suite against SQL Server.</summary>
[Collection("Query plan cache")]
public sealed class SqlServerExtensionsQueryComponentsTests : ExtensionsQueryComponentsLiveTests
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;
}

/// <summary>Runs the extensions live suite against MySQL.</summary>
[Collection("Query plan cache")]
public sealed class MySqlExtensionsQueryComponentsTests : ExtensionsQueryComponentsLiveTests
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;
}

/// <summary>Runs the extensions live suite against SQLite.</summary>
[Collection("Query plan cache")]
public sealed class SqliteExtensionsQueryComponentsTests : ExtensionsQueryComponentsLiveTests
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;
}
