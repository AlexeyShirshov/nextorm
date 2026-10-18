using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Plan-cache shape coverage executed against a live SQLite database. Every fact builds two real
/// queries through the inherited <see cref="ProviderTestSuite"/> repository, prepares and executes both
/// and asserts the returned rows, so a plan-cache key that aliases two differently shaped queries would
/// surface as wrong data (or a stale closure binding) rather than as a silent cache hit. Cache reuse is
/// read from the planner itself: a hit returns the very same prepared-command instance that owns the
/// stored plan. The structural comparers (<see cref="QueryCommand.GetQueryPlanEqualityComparer"/>,
/// <see cref="QueryCommand.GetExpressionPlanEqualityComparer"/>) and the raw descriptor
/// (<see cref="QueryCommand.RawShape"/>) are asserted next to the live results.
/// </summary>
[Collection("Sqlite")]
public sealed class SqlitePlanCacheShapeCoverageTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Drops every cached plan on this thread so the fact observes its own store entries only.</summary>
    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>Prepares through the cached path (<c>storeInCache: true</c>), returning the plan holder.</summary>
    private IPreparedQueryCommand<TResult> Prepare<TResult>(QueryCommand<TResult> command)
        => ((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: true,
            CancellationToken.None);

    /// <summary>Executes an already prepared command against the live database.</summary>
    private List<TResult> Rows<TResult>(IPreparedQueryCommand<TResult> prepared) => Context.ToList(prepared);

    /// <summary>Prepares and executes a command in one step.</summary>
    private List<TResult> Run<TResult>(QueryCommand<TResult> command) => Rows(Prepare(command));

    // Same closure site (one helper), different captured values: the value is not part of the plan key.
    private static QueryCommand<long> ComplexAbove(EntityBuilder<IComplexEntity> entities, long floor)
        => entities.Where(x => x.Id > floor).Select(x => x.Id);

    // Two textual call sites compile into two distinct display classes, which the plan key includes.
    private static QueryCommand<long> ComplexAboveAtSiteA(EntityBuilder<IComplexEntity> entities)
    {
        var floor = 1L;
        return entities.Where(x => x.Id > floor).Select(x => x.Id);
    }

    private static QueryCommand<long> ComplexAboveAtSiteB(EntityBuilder<IComplexEntity> entities)
    {
        var floor = 1L;
        return entities.Where(x => x.Id > floor).Select(x => x.Id);
    }

    private static QueryCommand<long> ComplexByString(EntityBuilder<IComplexEntity> entities, string? name)
        => entities.Where(x => x.String == name).Select(x => x.Id);

    private static QueryCommand<long> ComplexByNullableInt(EntityBuilder<IComplexEntity> entities, int? value)
        => entities.Where(x => x.Int == value).Select(x => x.Id);

    private static QueryCommand<int> Page(EntityBuilder<SimpleEntity> entities, int limit, int offset)
        => entities.OrderBy(x => x.Id).Limit(limit).Offset(offset).Select(x => x.Id);

    [Fact]
    public void SameClosureSite_DifferentValues_ShouldReusePlanWithoutStaleBinding()
    {
        ResetPlanCache();

        var first = Prepare(ComplexAbove(_sut.ComplexEntity, 0));
        Rows(first).Should().Equal(1L, 2L, 3L);

        var second = Prepare(ComplexAbove(_sut.ComplexEntity, 1));
        second.Should().BeSameAs(first, "a captured value is not part of the plan key");
        Rows(second).Should().Equal(2L, 3L);
    }

    [Fact]
    public void DistinctClosureSites_SameShape_ShouldBuildDistinctKeys()
    {
        ResetPlanCache();

        var siteA = ComplexAboveAtSiteA(_sut.ComplexEntity);
        var siteB = ComplexAboveAtSiteB(_sut.ComplexEntity);

        var preparedA = Prepare(siteA);
        Rows(preparedA).Should().Equal(2L, 3L);

        var preparedB = Prepare(siteB);
        preparedB.Should().NotBeSameAs(preparedA,
            "each closure site owns a distinct display-class type in the key");

        siteA.GetQueryPlanEqualityComparer().Equals(siteA, siteB).Should()
            .BeFalse("distinct closure sites are distinct plans even when the shape is identical");

        Rows(preparedB).Should().Equal(2L, 3L);
    }

    [Fact]
    public void ChangedPredicateShape_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        var first = Prepare(_sut.ComplexEntity.Where(x => x.Id > 1).Select(x => x.Id));
        Rows(first).Should().Equal(2L, 3L);

        var changed = Prepare(_sut.ComplexEntity.Where(x => x.Id >= 2 && x.Id <= 3).Select(x => x.Id));
        changed.Should().NotBeSameAs(first, "a different predicate shape is a different plan");
        Rows(changed).Should().Equal(2L, 3L);
    }

    [Fact]
    public void ChangedProjectionShape_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        var first = Prepare(_sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Id));
        Rows(first).Should().Equal(1L, 2L, 3L);

        var projected = Prepare(_sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id, x.Int }));
        projected.Should().NotBeSameAs(first, "a scalar projection and an anonymous projection are different plans");

        var rows = Rows(projected);
        rows.Should().HaveCount(3);
        rows.Select(r => r.Int).Should().Equal(null, 1, 1);
    }

    [Fact]
    public void ChangedProjectionValueType_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        var first = Prepare(_sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Int));
        Rows(first).Should().Equal(null, 1, 1);

        var coalesced = Prepare(_sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Int ?? -1));
        coalesced.Should().NotBeSameAs(first, "a nullable and a non-nullable projection are different plans");
        Rows(coalesced).Should().Equal(-1, 1, 1);
    }

    [Fact]
    public void ChangedOrderBy_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        var first = Prepare(_sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Id));
        Rows(first).Should().Equal(1L, 2L, 3L);

        var descending = Prepare(_sut.ComplexEntity.OrderByDescending(x => x.Id).Select(x => x.Id));
        descending.Should().NotBeSameAs(first, "the sort direction is part of the plan");
        Rows(descending).Should().Equal(3L, 2L, 1L);
    }

    [Fact]
    public void ChangedSecondaryOrdering_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        var first = Prepare(_sut.ComplexEntity.OrderBy(x => x.Int).OrderBy(x => x.Id).Select(x => x.Id));
        Rows(first).Should().Equal(1L, 2L, 3L);

        var reversed = Prepare(_sut.ComplexEntity.OrderBy(x => x.Int).OrderByDescending(x => x.Id).Select(x => x.Id));
        reversed.Should().NotBeSameAs(first, "the appended ordering key participates in the plan");
        Rows(reversed).Should().Equal(1L, 3L, 2L);
    }

    [Fact]
    public void ChangedJoinKind_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        var inner = Prepare(_sut.SimpleEntity
            .Where(s => s.Id <= 3)
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => p.Item2.Id));
        Rows(inner).OrderBy(x => x).Should().Equal(1L, 2L, 3L);

        var left = Prepare(_sut.SimpleEntity
            .Where(s => s.Id <= 3)
            .LeftJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => p.Item2.Id));
        left.Should().NotBeSameAs(inner, "the join kind is part of the plan");
        Rows(left).OrderBy(x => x).Should().Equal(1L, 2L, 3L);
    }

    [Fact]
    public void ChangedSubqueryShape_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        var inclusive = Prepare(_sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Cnt = _sut.SimpleEntity.Where(s => s.Id <= x.Id).Count() }));
        Rows(inclusive).Select(r => r.Cnt).Should().Equal(1, 2, 3);

        var shifted = Prepare(_sut.ComplexEntity
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Cnt = _sut.SimpleEntity.Where(s => s.Id < x.Id + 1).Count() }));
        shifted.Should().NotBeSameAs(inclusive, "the correlated subquery predicate is part of the plan");
        Rows(shifted).Select(r => r.Cnt).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ChangedGroupingShape_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        var scalar = Prepare(_sut.ComplexEntity
            .GroupBy(x => x.Int)
            .Select(g => new { g.Int, Count = SqlFunctions.Sql.count() }));
        var scalarRows = Rows(scalar);
        scalarRows.Should().HaveCount(2);
        scalarRows.Single(r => r.Int == null).Count.Should().Be(1);
        scalarRows.Single(r => r.Int == 1).Count.Should().Be(2);

        var distinctKeys = Prepare(_sut.ComplexEntity
            .GroupBy(x => new { x.Int, x.String })
            .Select(g => new { g.Int, Count = SqlFunctions.Sql.count() }));
        distinctKeys.Should().NotBeSameAs(scalar, "a grouping key with an extra member is a different plan");
        var distinctRows = Rows(distinctKeys);
        distinctRows.Should().HaveCount(3);
        distinctRows.Should().OnlyContain(r => r.Count == 1);
    }

    [Fact]
    public void ChangedAggregateShape_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        var counted = Prepare(_sut.ComplexEntity
            .GroupBy(x => x.Int)
            .Select(g => new { g.Int, Value = (long?)SqlFunctions.Sql.count() }));
        Rows(counted).Single(r => r.Int == 1).Value.Should().Be(2L);

        var summed = Prepare(_sut.ComplexEntity
            .GroupBy(x => x.Int)
            .Select(g => new { g.Int, Value = SqlFunctions.Sql.sum(g.Id) }));
        summed.Should().NotBeSameAs(counted, "a different aggregate is a different plan");
        var rows = Rows(summed);
        rows.Should().HaveCount(2);
        rows.Single(r => r.Int == null).Value.Should().Be(1L);
        rows.Single(r => r.Int == 1).Value.Should().Be(5L);
    }

    [Fact]
    public void NullToNonNullClosureOnNullableReference_ShouldRebind()
    {
        ResetPlanCache();

        var first = Prepare(ComplexByString(_sut.ComplexEntity, null));
        Rows(first).Should().BeEmpty();

        var matched = Prepare(ComplexByString(_sut.ComplexEntity, "xxx"));
        matched.Should().BeSameAs(first, "a null-to-value closure change keeps the same plan shape");
        Rows(matched).Should().Equal(2L);

        var backToNull = Prepare(ComplexByString(_sut.ComplexEntity, null));
        backToNull.Should().BeSameAs(first);
        Rows(backToNull).Should().BeEmpty();
    }

    [Fact]
    public void NullToNonNullClosureOnNullableValue_ShouldRebind()
    {
        ResetPlanCache();

        var first = Prepare(ComplexByNullableInt(_sut.ComplexEntity, null));
        Rows(first).Should().BeEmpty();

        var matched = Prepare(ComplexByNullableInt(_sut.ComplexEntity, 1));
        matched.Should().BeSameAs(first, "a null-to-value closure change keeps the same plan shape");
        Rows(matched).Should().Equal(2L, 3L);

        var other = Prepare(ComplexByNullableInt(_sut.ComplexEntity, 2));
        other.Should().BeSameAs(first);
        Rows(other).Should().BeEmpty();
    }

    [Fact]
    public void NullableJoinKey_ShouldMatchOnlyEqualNonNullKeys()
    {
        ResetPlanCache();

        var rows = _sut.AliasOrder
            .OrderBy(o => o.Id)
            .LeftJoin(Context.From<ExtremaEntity>(), (o, e) => o.Id == e.Score)
            .Select(p => new { p.Item1.Id, p.Item2.Score })
            .ToList();

        rows.Should().HaveCount(3, "order 1 matches the two score-1 rows and order 2 matches none");

        var matched = rows.Where(r => r.Id == 1).ToList();
        matched.Should().HaveCount(2);
        matched.Should().OnlyContain(r => r.Score == 1);

        var unmatched = rows.Where(r => r.Id == 2).ToList();
        unmatched.Should().ContainSingle();
        unmatched[0].Score.Should().BeNull("a null join key never matches and the outer row keeps nulls");
    }

    [Fact]
    public void NullableReferenceProjectionThroughOuterJoin_ShouldMaterializeNulls()
    {
        ResetPlanCache();

        var noMatches = Prepare(_sut.AliasOrder
            .OrderBy(o => o.Id)
            .LeftJoin(_sut.AliasPerson, (o, p) => o.BuyerId + 100 == p.Id)
            .Select(j => new { j.Item1.Id, j.Item2.Name }));
        var missing = Rows(noMatches);
        missing.Should().HaveCount(2);
        missing.Should().OnlyContain(r => r.Name == null);

        var matches = Prepare(_sut.AliasOrder
            .OrderBy(o => o.Id)
            .LeftJoin(_sut.AliasPerson, (o, p) => o.BuyerId == p.Id)
            .Select(j => new { j.Item1.Id, j.Item2.Name }));
        matches.Should().NotBeSameAs(noMatches, "a different join condition is a different plan");

        var names = Rows(matches).Select(r => r.Name).ToList();
        names.Should().Equal("Buyer", "Approver");
    }

    [Fact]
    public void ChangedPaging_ShouldNotShareThePlan()
    {
        ResetPlanCache();

        var first = Prepare(Page(_sut.SimpleEntityAsClass, 2, 0));
        Rows(first).Should().Equal(1, 2);

        var secondPage = Prepare(Page(_sut.SimpleEntityAsClass, 2, 2));
        secondPage.Should().NotBeSameAs(first, "limit and offset are part of the plan key");
        Rows(secondPage).Should().Equal(3, 4);
    }

    [Fact]
    public void InValuesShapeChange_ShouldNotReuseStalePlan()
    {
        ResetPlanCache();

        var ids = new long[] { 2, 3 };
        var first = Prepare(_sut.ComplexEntity
            .Where(x => SqlFunctions.Sql.@in(x.Id, ids))
            .OrderBy(x => x.Id)
            .Select(x => x.Id));
        Rows(first).Should().Equal(2L, 3L);

        ids = [1, 2, 3];
        var grown = Prepare(_sut.ComplexEntity
            .Where(x => SqlFunctions.Sql.@in(x.Id, ids))
            .OrderBy(x => x.Id)
            .Select(x => x.Id));
        grown.Should().NotBeSameAs(first, "a changed IN cardinality is a different plan shape");
        Rows(grown).Should().Equal(1L, 2L, 3L);
    }

    [Fact]
    public void InValuesSameShape_ShouldReusePlanAndRebind()
    {
        ResetPlanCache();

        var ids = new long[] { 1 };
        var first = Prepare(_sut.ComplexEntity.Where(x => SqlFunctions.Sql.@in(x.Id, ids)).Select(x => x.Id));
        Rows(first).Should().Equal(1L);

        ids[0] = 3;
        var rebound = Prepare(_sut.ComplexEntity.Where(x => SqlFunctions.Sql.@in(x.Id, ids)).Select(x => x.Id));
        rebound.Should().BeSameAs(first, "an unchanged IN cardinality is the same plan shape");
        Rows(rebound).Should().Equal(3L);
    }

    [Fact]
    public void QueryPlanComparer_SameShapeDifferentValues_ShouldBeEqual()
    {
        ResetPlanCache();

        var first = ComplexAbove(_sut.ComplexEntity, 0);
        var second = ComplexAbove(_sut.ComplexEntity, 2);

        var preparedFirst = Prepare(first);
        Rows(preparedFirst).Should().Equal(1L, 2L, 3L);

        var preparedSecond = Prepare(second);
        preparedSecond.Should().BeSameAs(preparedFirst);
        Rows(preparedSecond).Should().Equal(3L);

        var comparer = first.GetQueryPlanEqualityComparer();
        comparer.Equals(first, second).Should().BeTrue("the captured value is excluded from the plan key");
        comparer.GetHashCode(first).Should().Be(comparer.GetHashCode(second), "equal plans must hash equally");
    }

    [Fact]
    public void QueryPlanComparer_ChangedProjection_ShouldDiffer()
    {
        ResetPlanCache();

        var scalar = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Id);
        var anonymous = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id });

        var preparedScalar = Prepare(scalar);
        Rows(preparedScalar).Should().Equal(1L, 2L, 3L);

        var preparedAnonymous = Prepare(anonymous);
        preparedAnonymous.Should().NotBeSameAs(preparedScalar);
        Rows(preparedAnonymous).Should().HaveCount(3);

        anonymous.GetQueryPlanEqualityComparer().Equals(anonymous, scalar).Should()
            .BeFalse("a changed projection shape is a different plan");
    }

    [Fact]
    public void ExpressionComparer_ShouldAgreeOnEqualTreesAndDifferOnChangedPredicate()
    {
        ResetPlanCache();

        Expression<Func<IComplexEntity, bool>> same1 = x => x.Int > 1;
        Expression<Func<IComplexEntity, bool>> same2 = x => x.Int > 1;
        Expression<Func<IComplexEntity, bool>> changed = x => x.Int >= 1;

        var probe = _sut.ComplexEntity.Where(same1).Select(x => x.Id);
        var comparer = probe.GetExpressionPlanEqualityComparer();

        comparer.Equals(same1, same2).Should().BeTrue("equivalent trees share a plan expression");
        comparer.GetHashCode(same1).Should().Be(comparer.GetHashCode(same2));
        comparer.Equals(same1, changed).Should().BeFalse("a changed comparison operand is a different expression");

        Run(probe).Should().BeEmpty("no seeded Int value is greater than 1");
        Run(_sut.ComplexEntity.Where(changed).Select(x => x.Id)).Should().Equal(2L, 3L);
    }

    [Fact]
    public void RawShape_LiteralShape_ShouldMatchAndServeLiveCandidate()
    {
        ResetPlanCache();

        var first = _sut.SimpleEntity.Where(x => x.Id > 5).Select(x => x.Id);
        var firstShape = QueryCommand.CaptureRawShape(first);
        firstShape.Should().NotBeNull("a literal predicate over a plain table has a value-free raw shape");

        var second = _sut.SimpleEntity.Where(x => x.Id > 5).Select(x => x.Id);
        var secondShape = QueryCommand.CaptureRawShape(second);
        secondShape.Should().NotBeNull();
        secondShape!.Matches(firstShape!, second).Should().BeTrue("the identical literal graph is the same descriptor");

        Run(first).Should().Equal(6, 7, 8, 9, 10);
        QueryPlanStore.TryGetRaw(Context.GetType(), secondShape!, second, out _).Should()
            .BeTrue("the prepared plan publishes a live candidate for the identical shape");

        var changed = _sut.SimpleEntity.Where(x => x.Id > 6).Select(x => x.Id);
        var changedShape = QueryCommand.CaptureRawShape(changed);
        changedShape.Should().NotBeNull();
        changedShape!.Matches(firstShape!, changed).Should()
            .BeFalse("the literal bound is part of the raw expression shape");
        QueryPlanStore.TryGetRaw(Context.GetType(), changedShape!, changed, out _).Should().BeFalse();

        Run(changed).Should().Equal(7, 8, 9, 10);
    }

    [Fact]
    public void SharedAnyCommand_AlternatingPredicates_ShouldNotGoStale()
    {
        ResetPlanCache();

        var floor = 5;
        _sut.SimpleEntity.Where(x => x.Id > floor).Any().Should().BeTrue();

        floor = 50;
        _sut.SimpleEntity.Where(x => x.Id > floor).Any().Should().BeFalse(
            "the shared Any command must re-bind the current closure value");

        floor = 0;
        _sut.SimpleEntity.Where(x => x.Id > floor).Any().Should().BeTrue();
        _sut.SimpleEntity.Where(x => x.Id > floor).Count().Should().Be(10);
    }
}
