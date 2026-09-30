using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// In-memory execution of <c>SelectWhereMax</c>/<c>SelectWhereMin</c>: the value selector defines the
/// extremum, ties either keep one row or every tied row, and an optional group key partitions the
/// result. Comparison nulls are ignored and an all-null partition yields no rows.
/// </summary>
public class ExtremeRowEntity
{
    public int Id { get; set; }
    public int? Score { get; set; }
    public string? Category { get; set; }
    public bool Flag { get; set; }
    public int? Payload { get; set; }
}

public class InMemoryExtremeRowTests
{
    private readonly InMemoryRepository _sut;

    public InMemoryExtremeRowTests(InMemoryRepository sut) => _sut = sut;

    private EntityBuilder<ExtremeRowEntity> Source(params ExtremeRowEntity[] rows)
    {
        var entity = _sut.DataProvider.From<ExtremeRowEntity>();
        entity.WithData(rows);
        return entity;
    }

    [Fact]
    public void SelectWhereMax_Global_One_ShouldReturnTheSingleExtremeRow()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 5 },
            new ExtremeRowEntity { Id = 2, Score = 9 },
            new ExtremeRowEntity { Id = 3, Score = 2 });

        var rows = source.SelectWhereMax(it => it.Score).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(2);
    }

    [Fact]
    public void SelectWhereMin_Global_One_ShouldReturnTheSingleExtremeRow()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 5 },
            new ExtremeRowEntity { Id = 2, Score = 9 },
            new ExtremeRowEntity { Id = 3, Score = 2 });

        var rows = source.SelectWhereMin(it => it.Score).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Fact]
    public void SelectWhereMax_Global_All_ShouldReturnEveryTiedRow()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 9 },
            new ExtremeRowEntity { Id = 2, Score = 1 },
            new ExtremeRowEntity { Id = 3, Score = 9 });

        var rows = source.SelectWhereMax(it => it.Score, ExtremeRowTies.All).ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Id).Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void SelectWhereMin_Global_All_ShouldReturnEveryTiedRow()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 3 },
            new ExtremeRowEntity { Id = 2, Score = 3 },
            new ExtremeRowEntity { Id = 3, Score = 8 });

        var rows = source.SelectWhereMin(it => it.Score, ExtremeRowTies.All).ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Id).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void SelectWhereMax_Projection_ShouldApplyProjectionToTheWinner()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 4, Payload = 10 },
            new ExtremeRowEntity { Id = 2, Score = 8, Payload = 20 });

        var rows = source.SelectWhereMax(it => it.Score, it => new { it.Id, it.Payload }).ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(2);
        rows[0].Payload.Should().Be(20);
    }

    [Fact]
    public void SelectWhereMin_Projection_All_ShouldApplyProjectionToEveryTiedRow()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 4, Payload = 10 },
            new ExtremeRowEntity { Id = 2, Score = 8, Payload = 20 },
            new ExtremeRowEntity { Id = 3, Score = 4, Payload = 30 });

        var rows = source.SelectWhereMin(it => it.Score, it => new { it.Id, it.Payload }, ExtremeRowTies.All).ToList();

        rows.Should().HaveCount(2);
        rows.Should().BeEquivalentTo([new { Id = 1, Payload = 10 }, new { Id = 3, Payload = 30 }]);
    }

    [Fact]
    public void SelectWhereMax_Grouped_One_ShouldReturnOneWinnerPerGroup()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 9, Category = "a" },
            new ExtremeRowEntity { Id = 2, Score = 9, Category = "a" },
            new ExtremeRowEntity { Id = 3, Score = 1, Category = "b" },
            new ExtremeRowEntity { Id = 4, Score = 7, Category = "b" });

        var rows = source.SelectWhereMax(it => it.Score, ExtremeRowTies.One, it => it.Category).ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Category).Should().BeEquivalentTo(["a", "b"]);
        rows.Single(r => r.Category == "a").Score.Should().Be(9);
        rows.Single(r => r.Category == "b").Id.Should().Be(4);
    }

    [Fact]
    public void SelectWhereMin_Grouped_All_ShouldReturnEveryTiedRowPerGroup()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 5, Category = "a" },
            new ExtremeRowEntity { Id = 2, Score = 1, Category = "a" },
            new ExtremeRowEntity { Id = 3, Score = 1, Category = "a" },
            new ExtremeRowEntity { Id = 4, Score = 8, Category = "b" },
            new ExtremeRowEntity { Id = 5, Score = 3, Category = "b" });

        var rows = source.SelectWhereMin(it => it.Score, ExtremeRowTies.All, it => it.Category).ToList();

        rows.Should().HaveCount(3);
        rows.Where(r => r.Category == "a").Select(r => r.Id).Should().BeEquivalentTo([2, 3]);
        rows.Single(r => r.Category == "b").Id.Should().Be(5);
    }

    [Fact]
    public void SelectWhereMax_Grouped_Projection_ShouldProjectPerGroupWinner()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 2, Category = "a", Payload = 10 },
            new ExtremeRowEntity { Id = 2, Score = 6, Category = "a", Payload = 20 },
            new ExtremeRowEntity { Id = 3, Score = 4, Category = "b", Payload = 30 });

        var rows = source.SelectWhereMax(it => it.Score, it => new { it.Id, it.Payload }, ExtremeRowTies.One, it => it.Category).ToList();

        rows.Should().HaveCount(2);
        rows.Should().BeEquivalentTo([new { Id = 2, Payload = 20 }, new { Id = 3, Payload = 30 }]);
    }

    [Fact]
    public void SelectWhereMax_Grouped_NullKey_ShouldBeItsOwnGroup()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 1, Category = null },
            new ExtremeRowEntity { Id = 2, Score = 9, Category = null },
            new ExtremeRowEntity { Id = 3, Score = 5, Category = "a" });

        var rows = source.SelectWhereMax(it => it.Score, ExtremeRowTies.All, it => it.Category).ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Id).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void SelectWhereMax_Grouped_CompositeKey_ShouldPartitionByEveryComponent()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 3, Category = "a", Flag = true },
            new ExtremeRowEntity { Id = 2, Score = 8, Category = "a", Flag = true },
            new ExtremeRowEntity { Id = 3, Score = 9, Category = "a", Flag = false },
            new ExtremeRowEntity { Id = 4, Score = 1, Category = "a", Flag = false });

        var rows = source.SelectWhereMax(it => it.Score, ExtremeRowTies.All, it => new { it.Category, it.Flag }).ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Id).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void SelectWhereMax_CompositeValueSelector_ShouldOrderComponentWise()
    {
        // The SQL lowering orders by every value column in declaration order, so the extremum is the
        // lexicographic maximum/minimum of the tuple, not the extremum of any single component.
        var source = Source(
            new ExtremeRowEntity { Id = 1, Category = "b", Score = 5 },
            new ExtremeRowEntity { Id = 2, Category = "a", Score = 9 },
            new ExtremeRowEntity { Id = 3, Category = "c", Score = 1 },
            new ExtremeRowEntity { Id = 4, Category = "a", Score = 1 });

        source.SelectWhereMax(it => new { it.Category, it.Score }).ToList()
            .Should().ContainSingle().Which.Id.Should().Be(3);

        source.SelectWhereMin(it => new { it.Category, it.Score }).ToList()
            .Should().ContainSingle().Which.Id.Should().Be(4);
    }

    [Fact]
    public void SelectWhereMax_CompositeValueSelector_NullComponent_ShouldExcludeRow()
    {
        // SQL renders `<comp> is not null` for every value column, so a tuple with any null component is
        // invisible to the fold (and an all-null partition yields nothing).
        var source = Source(
            new ExtremeRowEntity { Id = 1, Category = null, Score = 100 },
            new ExtremeRowEntity { Id = 2, Category = "a", Score = 5 },
            new ExtremeRowEntity { Id = 3, Category = "a", Score = null },
            new ExtremeRowEntity { Id = 4, Category = null, Score = 1 });

        source.SelectWhereMax(it => new { it.Category, it.Score }).ToList()
            .Should().ContainSingle().Which.Id.Should().Be(2);
        source.SelectWhereMin(it => new { it.Category, it.Score }).ToList()
            .Should().ContainSingle().Which.Id.Should().Be(2);
    }

    [Fact]
    public void SelectWhereMax_CompositeValueSelector_All_ShouldReturnEveryTiedTuple()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Category = "a", Score = 9 },
            new ExtremeRowEntity { Id = 2, Category = "a", Score = 9 },
            new ExtremeRowEntity { Id = 3, Category = "a", Score = 5 });

        var rows = source.SelectWhereMax(it => new { it.Category, it.Score }, ExtremeRowTies.All).ToList();

        rows.Select(r => r.Id).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void SelectWhereMax_SingleComponentAnonymousValueSelector_ShouldBehaveLikeTheScalar()
    {
        // `new { x.Score }` is a composite NewExpression for the SQL lowering (one value column plus the
        // IS NOT NULL filter), so it must not fall back to Comparer<anonymous type>, which throws.
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = null },
            new ExtremeRowEntity { Id = 2, Score = 4 },
            new ExtremeRowEntity { Id = 3, Score = 9 });

        source.SelectWhereMax(it => new { it.Score }).ToList().Should().ContainSingle().Which.Id.Should().Be(3);
        source.SelectWhereMin(it => new { it.Score }).ToList().Should().ContainSingle().Which.Id.Should().Be(2);
    }

    [Fact]
    public void SelectWhereMax_PreparedPlan_ShouldSeedDataAndResolverLikeTheNormalPath()
    {
        // The extreme path used to early-return before the normal path's cache bookkeeping, so a cached
        // plan had neither the resolved source nor the per-call resolver; both must be populated.
        var source = Source(new ExtremeRowEntity { Id = 1, Score = 7 });
        QueryCommand<ExtremeRowEntity> command = source.SelectWhereMax(it => it.Score);

        var prepared = _sut.DataProvider.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);

        var cacheEntry = prepared.Should().BeOfType<InMemoryPreparedQueryCommand<ExtremeRowEntity>>().Subject;
        cacheEntry.Data.Should().NotBeNull();
        cacheEntry.Resolver.Should().NotBeNull();
    }

    [Fact]
    public void SelectWhereMax_EmptySource_ShouldYieldNoRows()
    {
        var source = Source();

        source.SelectWhereMax(it => it.Score).ToList().Should().BeEmpty();
        source.SelectWhereMax(it => it.Score, ExtremeRowTies.All, it => it.Category).ToList().Should().BeEmpty();
    }

    [Fact]
    public void SelectWhereMin_Singleton_ShouldReturnTheOnlyRow()
    {
        var source = Source(new ExtremeRowEntity { Id = 1, Score = 7, Category = "a" });

        var rows = source.SelectWhereMin(it => it.Score).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public void SelectWhereMax_NullComparisonValues_ShouldBeIgnored()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = null },
            new ExtremeRowEntity { Id = 2, Score = 4 },
            new ExtremeRowEntity { Id = 3, Score = null });

        source.SelectWhereMax(it => it.Score).ToList().Should().ContainSingle().Which.Id.Should().Be(2);
        source.SelectWhereMax(it => it.Score, ExtremeRowTies.All).ToList().Should().ContainSingle().Which.Id.Should().Be(2);
    }

    [Fact]
    public void SelectWhereMax_GroupWithOnlyNullComparisonValues_ShouldYieldNoRowsForThatGroup()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = null, Category = "a" },
            new ExtremeRowEntity { Id = 2, Score = null, Category = "a" },
            new ExtremeRowEntity { Id = 3, Score = 5, Category = "b" });

        var rows = source.SelectWhereMax(it => it.Score, ExtremeRowTies.All, it => it.Category).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Fact]
    public void SelectWhereMax_Projection_NullablePayload_ShouldPreserveNull()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 3, Payload = null },
            new ExtremeRowEntity { Id = 2, Score = 9, Payload = 42 });

        var rows = source.SelectWhereMax(it => it.Score, it => it.Payload).ToList();

        rows.Should().ContainSingle().Which.Should().Be(42);

        var nullSource = Source(new ExtremeRowEntity { Id = 1, Score = 9, Payload = null });
        var nullRows = nullSource.SelectWhereMax(it => it.Score, it => it.Payload).ToList();
        nullRows.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    public void SelectWhereMax_WithWhere_ShouldApplyTheFilterBeforeSelectingTheExtreme()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 5 },
            new ExtremeRowEntity { Id = 2, Score = 9 },
            new ExtremeRowEntity { Id = 3, Score = 7 });

        var rows = source.Where(it => it.Id != 2).SelectWhereMax(it => it.Score).ToList();

        rows.Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Fact]
    public void SelectWhereMax_CombinedWithPaging_ShouldFailClosed()
    {
        var source = Source(
            new ExtremeRowEntity { Id = 1, Score = 5 },
            new ExtremeRowEntity { Id = 2, Score = 9 });

        var act = () => source.SelectWhereMax(it => it.Score).Page(1, 0).ToList();

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*paging*");
    }

    [Fact]
    public void SelectWhereMax_CombinedWithJoin_ShouldFailClosed()
    {
        _sut.SimpleEntity.WithData([new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 }]);
        var joined = _sut.SimpleEntity.Join(_sut.SimpleEntity, (a, b) => a.Id == b.Id);

        var act = () => joined.SelectWhereMax(p => p.Item1.Id, p => new { p.Item1.Id }).ToList();

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*joins*");
    }

    /// <summary>
    /// The in-memory provider fails closed on exactly the modifiers the SQL lowering rejects, so a
    /// query that runs in memory cannot silently disagree with the mapped providers. HAVING has no
    /// in-memory equivalent and is one of the C4 modifiers.
    /// </summary>
    [Fact]
    public void SelectWhereMax_CombinedWithHaving_ShouldFailClosedLikeTheSqlPath()
    {
        var source = Source(new ExtremeRowEntity { Id = 1, Score = 7 });

        var act = () => source.Having(it => it.Id > 0).SelectWhereMax(it => it.Score).ToList();

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*HAVING*");
    }

    [Fact]
    public void SelectWhereMax_CombinedWithWindowFunction_ShouldFailClosedLikeTheSqlPath()
    {
        var source = Source(new ExtremeRowEntity { Id = 1, Score = 7 });

        var act = () => source.SelectWhereMax(it => it.Score)
            .Select(it => new { R = SqlFunctions.Sql.row_number().Over(partitionBy: () => it.Score) })
            .ToList();

        act.Should().Throw<BuildSqlCommandException>().WithMessage("*window functions*");
    }

    // --- clone and plan-cache identity -------------------------------------------------------

    [Fact]
    public void Clone_ShouldPreserveTheExtremeClause()
    {
        var source = Source(new ExtremeRowEntity { Id = 1, Score = 7, Category = "a" });
        var command = source.SelectWhereMax(it => it.Score, ExtremeRowTies.All, it => it.Category).ToCommand();

        var clone = command.Clone();

        clone.ExtremeRow.Should().NotBeNull();
        clone.ExtremeRow!.Kind.Should().Be(command.ExtremeRow!.Kind);
        clone.ExtremeRow.Ties.Should().Be(command.ExtremeRow.Ties);
        ReferenceEquals(clone.ExtremeRow.ValueSelector, command.ExtremeRow.ValueSelector).Should().BeTrue();
        ReferenceEquals(clone.ExtremeRow.GroupBy, command.ExtremeRow.GroupBy).Should().BeTrue();
    }

    [Fact]
    public void SelectWhereMax_FreshlyBuiltSameShape_ShouldReuseTheCachedPlan()
    {
        var source = Source(new ExtremeRowEntity { Id = 1, Score = 7 });

        QueryCommand<ExtremeRowEntity> Build() => source.SelectWhereMax(it => it.Score, ExtremeRowTies.All, it => it.Category).ToCommand();

        var first = _sut.DataProvider.GetPreparedQueryCommand(Build(), false, true, TestContext.Current.CancellationToken);
        var second = _sut.DataProvider.GetPreparedQueryCommand(Build(), false, true, TestContext.Current.CancellationToken);

        ReferenceEquals(first, second).Should().BeTrue("an equal extreme-row shape must reuse the cached plan");
    }

    [Fact]
    public void SelectWhereMax_DifferentKindOrTiesOrSelectorOrGroup_ShouldNotShareTheCachedPlan()
    {
        var source = Source(new ExtremeRowEntity { Id = 1, Score = 7, Payload = 3, Category = "a" });

        IPreparedQueryCommand<T> Prepare<T>(QueryCommand<T> cmd) => _sut.DataProvider.GetPreparedQueryCommand(cmd, false, true, TestContext.Current.CancellationToken);

        var max = Prepare(source.SelectWhereMax(it => it.Score, ExtremeRowTies.All, it => it.Category).ToCommand());
        var min = Prepare(source.SelectWhereMin(it => it.Score, ExtremeRowTies.All, it => it.Category).ToCommand());
        var oneTie = Prepare(source.SelectWhereMax(it => it.Score, ExtremeRowTies.One, it => it.Category).ToCommand());
        var otherSelector = Prepare(source.SelectWhereMax(it => it.Payload, ExtremeRowTies.All, it => it.Category).ToCommand());
        var noGroup = Prepare(source.SelectWhereMax(it => it.Score, ExtremeRowTies.All).ToCommand());

        ReferenceEquals(max, min).Should().BeFalse("Max and Min are different extrema");
        ReferenceEquals(max, oneTie).Should().BeFalse("different tie semantics must not share a plan");
        ReferenceEquals(max, otherSelector).Should().BeFalse("different value selectors must not share a plan");
        ReferenceEquals(max, noGroup).Should().BeFalse("a grouped and an ungrouped shape must not share a plan");
    }

    [Fact]
    public void SelectWhereMax_DifferentProjection_ShouldNotShareTheCachedPlan()
    {
        var source = Source(new ExtremeRowEntity { Id = 1, Score = 7, Payload = 3 });

        IPreparedQueryCommand<T> Prepare<T>(QueryCommand<T> cmd) => _sut.DataProvider.GetPreparedQueryCommand(cmd, false, true, TestContext.Current.CancellationToken);

        var score = Prepare(source.SelectWhereMax(it => it.Score, it => new { V = it.Score }));
        var payload = Prepare(source.SelectWhereMax(it => it.Score, it => new { V = it.Payload }));

        ReferenceEquals(score, payload).Should().BeFalse("different projections must not share a plan");
    }
}
