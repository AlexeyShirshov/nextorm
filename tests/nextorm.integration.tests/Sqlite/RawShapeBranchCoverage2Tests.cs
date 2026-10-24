using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Differential coverage of the pre-preparation raw descriptor
/// (<c>src/nextorm.core/Query/QueryCommand.QueryPreparer.cs</c>: <c>RawShape</c> 3625-3888,
/// <c>RawShape.Matches</c> 3702-3763, <c>CaptureRawShape</c> 3889-4030, <c>CtesEqual</c> 3766-3789,
/// <c>FilterScopeEqual</c> 3839-3840) executed against a live SQLite database.
///
/// <para>
/// Every fact builds two (or three) structurally similar-but-different commands over the inherited
/// <see cref="ProviderTestSuite"/> repository, captures both raw descriptors and drives the differential
/// arms of <see cref="QueryCommand.RawShape.Matches"/> in both directions, then <b>executes</b> the
/// commands and asserts the returned rows: a descriptor that aliased two differently shaped graphs would
/// surface as a wrong row set (or a stale plan) rather than as a silent cache hit.
/// </para>
///
/// <para>
/// Shapes that have no public entry point (and therefore no live counterpart) are recorded in the
/// "unreachable" note at the bottom of this file instead of being fabricated with synthetic expression
/// trees.
/// </para>
/// </summary>
[Collection("Query plan cache")]
public sealed class RawShapeBranchCoverage2Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

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

    /// <summary>Drops every cached plan (and the raw candidate index) on this thread.</summary>
    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>Captures the value-free raw descriptor of a fresh command, or <see langword="null"/>.</summary>
    private static QueryCommand.RawShape? Shape<TResult>(QueryCommand<TResult> command)
        => QueryCommand.CaptureRawShape(command);

    // ---------------------------------------------------------------------------------------------
    // 1. Reference/absence short circuits, candidate publication and the purge cycle
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_SelfAndNullDifferential_ShouldShortCircuitAndRepublishAfterPurge()
    {
        ResetPlanCache();

        var command = _sut.ComplexEntity.Where(x => x.Id >= 2).OrderBy(x => x.Id).Select(x => x.Id);
        var shape = Shape(command);

        shape.Should().NotBeNull("a literal predicate over a plain table has a value-free raw shape");
        shape!.Matches(shape, command).Should().BeTrue("the identical instance is trivially its own descriptor");
        shape.Matches(null!, command).Should().BeFalse("a missing counterpart never matches");

        Run(command).Should().Equal(2L, 3L);

        var probe = _sut.ComplexEntity.Where(x => x.Id >= 2).OrderBy(x => x.Id).Select(x => x.Id);
        QueryPlanStore.TryGetRaw(Context.GetType(), Shape(probe)!, probe, out _).Should()
            .BeTrue("the prepared plan publishes a live candidate for the identical shape");

        ResetPlanCache();
        QueryPlanStore.TryGetRaw(Context.GetType(), Shape(probe)!, probe, out _).Should()
            .BeFalse("a purge drops the raw candidate index with the plan cache");

        Run(probe).Should().Equal(new[] { 2L, 3L }, "the shape still reads correctly after the purge");

        var republished = _sut.ComplexEntity.Where(x => x.Id >= 2).OrderBy(x => x.Id).Select(x => x.Id);
        QueryPlanStore.TryGetRaw(Context.GetType(), Shape(republished)!, republished, out _).Should()
            .BeTrue("a re-run republishes the candidate after the purge");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Entity / result / projection type arms
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_DifferentEntityAndProjectionTypes_ShouldNotMatch()
    {
        ResetPlanCache();

        var simple = _sut.SimpleEntity.Where(x => x.Id >= 8).Select(x => x.Id);
        var complex = _sut.ComplexEntity.Where(x => x.Id >= 2).Select(x => x.Id);
        var otherColumn = _sut.ComplexEntity.Where(x => x.Id >= 2).Select(x => x.Int);

        var simpleShape = Shape(simple)!;
        var complexShape = Shape(complex)!;
        var otherColumnShape = Shape(otherColumn)!;

        complexShape.Matches(simpleShape, complex).Should()
            .BeFalse("a different source entity type is a different descriptor");
        otherColumnShape.Matches(complexShape, otherColumn).Should()
            .BeFalse("a different projected column is a different descriptor");

        Run(simple).Should().Equal(8, 9, 10);
        Run(complex).Should().Equal(2L, 3L);
        Run(otherColumn).Should().Equal(1, 1);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Column-list arm (same arity, different columns)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_DifferentColumnListSameArity_ShouldNotMatch()
    {
        ResetPlanCache();

        var byId = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Id });
        var byInt = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => new { x.Int });

        var idShape = Shape(byId)!;
        var intShape = Shape(byInt)!;

        intShape.Matches(idShape, byInt).Should()
            .BeFalse("two single-column projections of different columns are different descriptors");

        var idRows = Run(byId);
        idRows.Select(r => r.Id).Should().Equal(1L, 2L, 3L);

        var intRows = Run(byInt);
        intRows.Select(r => r.Int).Should().Equal(null, 1, 1);
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Parameterised (closure) shape: ineligible descriptor, live rebinding
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_ClosureValue_ShouldBeIneligibleAndRebindLiveRows()
    {
        ResetPlanCache();

        static QueryCommand<int> Above(EntityBuilder<ISimpleEntity> entities, int floor)
            => entities.Where(x => x.Id > floor).Select(x => x.Id);

        var first = Above(_sut.SimpleEntity, 5);
        Shape(first).Should().BeNull("a captured (closure) value makes the shape ineligible for reuse");

        var firstPrepared = Prepare(first);
        Rows(firstPrepared).Should().Equal(6, 7, 8, 9, 10);

        var second = Above(_sut.SimpleEntity, 8);
        Shape(second).Should().BeNull("the parameterised shape is ineligible on every call");

        var secondPrepared = Prepare(second);
        secondPrepared.Should().BeSameAs(firstPrepared, "a captured value is not part of the plan key");
        Rows(secondPrepared).Should().Equal(new[] { 9, 10 }, "the reused plan re-binds the current closure value");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Literal vs closure: the same SQL text, different eligibility
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_LiteralVersusClosure_ShouldDifferInEligibilityOnly()
    {
        ResetPlanCache();

        var floor = 5;
        var literal = _sut.SimpleEntity.Where(x => x.Id > 5).Select(x => x.Id);
        var closure = _sut.SimpleEntity.Where(x => x.Id > floor).Select(x => x.Id);

        Shape(literal).Should().NotBeNull("an inlined literal keeps the descriptor value-free");
        Shape(closure).Should().BeNull("the same SQL text with a captured bound is ineligible");

        Run(literal).Should().Equal(6, 7, 8, 9, 10);
        Run(closure).Should().Equal(new[] { 6, 7, 8, 9, 10 }, "both forms read the same rows from the database");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. Paging arm
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_PagingDifferential_ShouldNotMatch()
    {
        ResetPlanCache();

        var firstPage = _sut.SimpleEntity.OrderBy(x => x.Id).Limit(2).Select(x => x.Id);
        var secondPage = _sut.SimpleEntity.OrderBy(x => x.Id).Limit(2).Offset(2).Select(x => x.Id);
        var widerPage = _sut.SimpleEntity.OrderBy(x => x.Id).Limit(3).Select(x => x.Id);

        var firstShape = Shape(firstPage)!;
        var secondShape = Shape(secondPage)!;
        var widerShape = Shape(widerPage)!;

        secondShape.Matches(firstShape, secondPage).Should().BeFalse("the offset is part of the descriptor");
        widerShape.Matches(firstShape, widerPage).Should().BeFalse("the limit is part of the descriptor");

        Run(firstPage).Should().Equal(1, 2);
        Run(secondPage).Should().Equal(3, 4);
        Run(widerPage).Should().Equal(1, 2, 3);
    }

    // ---------------------------------------------------------------------------------------------
    // 7. DISTINCT and hint arms
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_DistinctAndHintDifferential_ShouldNotMatch()
    {
        ResetPlanCache();

        var plain = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Int);
        var distinct = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Int).Distinct();
        var hinted = _sut.ComplexEntity.OrderBy(x => x.Id).Select(x => x.Int).Hint("d1_hint_probe");

        Shape(plain).Should().NotBeNull();
        Shape(distinct)!.Matches(Shape(plain)!, distinct).Should()
            .BeFalse("DISTINCT is part of the descriptor");
        Shape(hinted)!.Matches(Shape(plain)!, hinted).Should()
            .BeFalse("a rendered hint list is part of the descriptor");

        Run(plain).Should().Equal(null, 1, 1);
        Run(distinct).Should().HaveCount(2, "DISTINCT collapses the two equal values");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Sorting arm
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_SortingDifferential_ShouldNotMatch()
    {
        ResetPlanCache();

        var ascending = _sut.SimpleEntity.OrderBy(x => x.Id).Select(x => x.Id);
        var descending = _sut.SimpleEntity.OrderByDescending(x => x.Id).Select(x => x.Id);
        var twoKeys = _sut.SimpleEntity.OrderBy(x => x.Id).OrderByDescending(x => x.Id).Select(x => x.Id);

        var ascendingShape = Shape(ascending)!;

        Shape(descending)!.Matches(ascendingShape, descending).Should()
            .BeFalse("the sort direction is part of the descriptor");
        Shape(twoKeys)!.Matches(ascendingShape, twoKeys).Should()
            .BeFalse("the length of the sort key list is part of the descriptor");

        Run(ascending).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        Run(descending).Should().Equal(10, 9, 8, 7, 6, 5, 4, 3, 2, 1);
    }

    // ---------------------------------------------------------------------------------------------
    // 9. Join arm
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_JoinDifferential_ShouldNotMatch()
    {
        ResetPlanCache();

        // The filter is applied AFTER the join: a Where placed before a Join lowers the left source to a
        // derived source, which makes the whole graph ineligible (see the reachability note below).
        var inner = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Where(p => p.Item2.Id >= 2)
            .Select(p => p.Item2.Id);
        var left = _sut.SimpleEntity
            .LeftJoin(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Where(p => p.Item2.Id >= 2)
            .Select(p => p.Item2.Id);
        var noJoin = _sut.SimpleEntity.Where(s => s.Id >= 2).Select(s => (long)s.Id);

        Shape(inner).Should().NotBeNull("an inner join over plain table sources is a describable graph");
        Shape(left).Should().NotBeNull("an outer join over plain table sources is a describable graph");
        Shape(noJoin).Should().NotBeNull("a filter-only graph is a describable graph");

        var innerShape = Shape(inner)!;

        Shape(left)!.Matches(innerShape, left).Should().BeFalse("the join kind is part of the descriptor");
        Shape(noJoin)!.Matches(innerShape, noJoin).Should().BeFalse("an absent join list is a different descriptor");

        Run(inner).OrderBy(x => x).Should().Equal(2L, 3L);
        Run(left).OrderBy(x => x).Should().Equal(2L, 3L);
        Run(noJoin).Should().Equal(2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L, 10L);
    }

    // ---------------------------------------------------------------------------------------------
    // 10. Tag and tables-in-scope hint arms
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_TagAndScopeHintDifferential_ShouldNotMatch()
    {
        ResetPlanCache();

        var plain = _sut.SimpleEntity.Where(x => x.Id <= 2).Select(x => x.Id);
        var taggedA = _sut.SimpleEntity.Where(x => x.Id <= 2).Select(x => x.Id).WithTag("d1_tag_a");
        var taggedB = _sut.SimpleEntity.Where(x => x.Id <= 2).Select(x => x.Id).WithTag("d1_tag_b");
        var scoped = _sut.SimpleEntity.WithTablesInScopeHint("simple_entity").Where(x => x.Id <= 2).Select(x => x.Id);

        var plainShape = Shape(plain)!;

        Shape(taggedA)!.Matches(plainShape, taggedA).Should().BeFalse("the tag text is part of the descriptor");
        Shape(taggedB)!.Matches(Shape(taggedA)!, taggedB).Should().BeFalse("two different tags never match");
        Shape(scoped)!.Matches(plainShape, scoped).Should()
            .BeFalse("a tables-in-scope hint list is part of the descriptor");

        Run(plain).Should().Equal(1, 2);
        Run(taggedA).Should().Equal(new[] { 1, 2 }, "the tagged query is a live query, not a rejection");
    }

    // ---------------------------------------------------------------------------------------------
    // 11. Grouping / HAVING arms
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_GroupingAndHavingDifferential_ShouldNotMatch()
    {
        ResetPlanCache();

        var byInt = _sut.ComplexEntity
            .GroupBy(x => x.Int)
            .Select(g => new { g.Int, Count = SqlFunctions.Sql.count() });
        var byId = _sut.ComplexEntity
            .GroupBy(x => x.Id)
            .Select(g => new { g.Id, Count = SqlFunctions.Sql.count() });
        var withHaving = _sut.ComplexEntity
            .GroupBy(x => x.Int)
            .Having(g => SqlFunctions.Sql.count() > 1)
            .Select(g => new { g.Int, Count = SqlFunctions.Sql.count() });

        var byIntShape = Shape(byInt)!;

        Shape(byId)!.Matches(byIntShape, byId).Should().BeFalse("a different grouping key is a different descriptor");
        Shape(withHaving)!.Matches(byIntShape, withHaving).Should()
            .BeFalse("a HAVING clause is part of the descriptor");

        var byIntRows = Run(byInt);
        byIntRows.Should().HaveCount(2);
        byIntRows.Single(r => r.Int == null).Count.Should().Be(1);

        var havingRows = Run(withHaving);
        havingRows.Should().ContainSingle();
        havingRows[0].Int.Should().Be(1);
        havingRows[0].Count.Should().Be(2);
    }

    // ---------------------------------------------------------------------------------------------
    // 12. Grouping-type arm (CUBE)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_GroupingTypeDifferential_ShouldNotMatch()
    {
        ResetPlanCache();

        var plain = _sut.ComplexEntity
            .GroupBy(x => x.Int)
            .Select(g => new { g.Int, Count = SqlFunctions.Sql.count() });
        var cube = _sut.ComplexEntity
            .GroupByCube(x => x.Int)
            .Select(g => new { g.Int, Count = SqlFunctions.Sql.count() });

        Shape(cube)!.Matches(Shape(plain)!, cube).Should()
            .BeFalse("the grouping type (GROUP BY vs GROUP BY CUBE) is part of the descriptor");

        Run(plain).Should().HaveCount(2);
        // Live counterpart of the same grouping family: SQLite renders no GROUP BY CUBE (the cube form
        // fails with "no such function: cube"), so only its descriptor is compared here.
        Run(_sut.ComplexEntity
                .GroupBy(x => x.Int)
                .Select(g => new { g.Int, Count = SqlFunctions.Sql.count() }))
            .Should().HaveCount(2);
    }

    // ---------------------------------------------------------------------------------------------
    // 13. CTE list arm: same list matches, a changed body does not
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_CteListDifferential_ShouldMatchSameAndRejectChangedBody()
    {
        ResetPlanCache();

        QueryCommand<int> Same()
        {
            var body = _sut.ComplexEntity.Where(c => c.Id >= 2).Select(c => new { c.Id, c.Int });
            return Context.With("d1_cte_list", body)
                .From("d1_cte_list")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        QueryCommand<int> Changed()
        {
            var body = _sut.ComplexEntity.Where(c => c.Id >= 3).Select(c => new { c.Id, c.Int });
            return Context.With("d1_cte_list", body)
                .From("d1_cte_list")
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        var sameA = Same();
        var sameB = Same();
        var changed = Changed();

        var sameAShape = Shape(sameA)!;
        Shape(sameB)!.Matches(sameAShape, sameB).Should().BeTrue("an identical CTE list is the same descriptor");
        Shape(changed)!.Matches(sameAShape, changed).Should()
            .BeFalse("a changed CTE body is a different descriptor (CtesEqual body arm)");

        Run(sameA).Should().Equal(2, 3);
        Run(changed).Should().Equal(3);
    }

    // ---------------------------------------------------------------------------------------------
    // 14. CTE identity arm: same body under a different name
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_CteNameDifferential_ShouldNotMatch()
    {
        ResetPlanCache();

        QueryCommand<int> Under(string name)
        {
            var body = _sut.ComplexEntity.Where(c => c.Id >= 2).Select(c => new { c.Id });
            return Context.With(name, body)
                .From(name)
                .OrderBy(t => t["id"].AsInt)
                .Select(t => t["id"].AsInt);
        }

        var left = Under("d1_cte_name_left");
        var right = Under("d1_cte_name_right");

        Shape(right)!.Matches(Shape(left)!, right).Should()
            .BeFalse("CTE declarations are paired by name, never by position (CtesEqual name arm)");

        Run(left).Should().Equal(2, 3);
        Run(right).Should().Equal(2, 3);
    }

    // ---------------------------------------------------------------------------------------------
    // 15. Union arm
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_UnionDifferential_ShouldNotMatch()
    {
        ResetPlanCache();

        QueryCommand<int> UnionAll()
        {
            var left = _sut.SimpleEntity.Where(x => x.Id <= 2).Select(x => x.Id);
            var right = _sut.SimpleEntity.Where(x => x.Id >= 9).Select(x => x.Id);
            return left.UnionAll(right);
        }

        QueryCommand<int> Union()
        {
            var left = _sut.SimpleEntity.Where(x => x.Id <= 2).Select(x => x.Id);
            var right = _sut.SimpleEntity.Where(x => x.Id >= 2).Select(x => x.Id);
            return left.Union(right);
        }

        var plain = _sut.SimpleEntity.Where(x => x.Id <= 2).Select(x => x.Id);

        var unionAll = UnionAll();
        var union = Union();

        var unionAllShape = Shape(unionAll)!;
        Shape(union)!.Matches(unionAllShape, union).Should()
            .BeFalse("a different union type is a different descriptor");
        Shape(plain)!.Matches(unionAllShape, plain).Should()
            .BeFalse("an absent union is a different descriptor");

        Run(unionAll).OrderBy(x => x).Should().Equal(1, 2, 9, 10);
        Run(union).OrderBy(x => x).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    }

    // ---------------------------------------------------------------------------------------------
    // 16. Source identity arm: raw SQL vs derived command vs bound named source
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RawShape_RawSqlAndDerivedSources_ShouldBeIneligibleWhileBoundNameMatches()
    {
        ResetPlanCache();

        var fromRawSql = Context.FromSql("select id from simple_entity where id >= 9").Select(t => t["id"].AsInt);
        Shape(fromRawSql).Should().BeNull("a raw-SQL FROM source is not a plain table source");

        var derivedBody = _sut.SimpleEntity.Where(x => x.Id >= 9).Select(x => new { x.Id });
        var fromDerived = Context.From(derivedBody).Select(x => x.Id);
        Shape(fromDerived).Should().BeNull("a derived-command FROM source is not a plain table source");

        var unbound = Context.From("simple_entity").Where(t => t["id"].AsInt >= 9).Select(t => t["id"].AsInt);
        var bound = Context.From("simple_entity")
            .BindEntity<SimpleEntity>(new[] { "id" })
            .Where(x => x.Id >= 9)
            .Select(x => x.Id);

        var unboundShape = Shape(unbound);
        unboundShape.Should().NotBeNull("a named table source is a plain table source");
        Shape(bound)!.Matches(unboundShape!, bound).Should()
            .BeFalse("a bound entity source carries a source binding the unbound one lacks (FromBinding arm)");

        Run(fromRawSql).Should().Equal(9, 10);
        Run(fromDerived).Should().Equal(9, 10);
        Run(unbound).Should().Equal(9, 10);
        Run(bound).Should().Equal(9, 10);
    }

    // ---------------------------------------------------------------------------------------------
    // Unreachable targets (recorded instead of fabricated)
    //
    // No public entry point exists for the following RawShape members, so their Matches arms have no
    // reachable site from the integration suite and are NOT counted as covered:
    //   * Settings (:3640, Matches :3719) - no public settings setter on the builder or the command;
    //   * GroupingSets (:3635, :3715) - only a CUBE/ROLLUP grouping type is public, never an explicit set;
    //   * SampleRatio/SampleOffset (:3638-3639, :3718) - FromOptions.Sample is internal;
    //   * IndexHints/IndexHintKind/TableHints (:3642-3643, :3721-3723) - FromOptions.WithTableHint/
    //     WithIndex are only reachable through From(string, options), whose clauses SQLite does not render
    //     (a live counterpart would have to be a rejection), so the arm is left to the provider-specific F;
    //   * ForJsonClause/ForXmlClause (:3644-3645, :3724) - the clauses are settable but no SQLite query can
    //     render them, so there is no live counterpart;
    //   * TableSample/Temporal (:3646-3647, :3725) - no public SQLite-renderable setter;
    //   * LimitBy/DistinctOn (:3649-3650, :3727) - internal builder state;
    // Reachability notes for shapes that DO have a public entry point but only in a lowered form:
    //   * a `Where` placed BEFORE a `Join`/`LeftJoin` lowers the filtered left source to a derived
    //     source, so `IsPlainTableSource` rejects the graph and `CaptureRawShape` returns null; the join
    //     arm is therefore driven with the filter after the join (RawShape_JoinDifferential fact);
    //   * `GROUP BY CUBE` is capturable but not renderable by SQLite ("no such function: cube"), so its
    //     descriptor is compared and the live counterpart is the same grouping family without the cube.
    //
    //   * PerParentPaging/PerParentPagingEmbedded/DefaultOnEmpty/SingleScalar/HasPerParentEagerTopN/
    //     IdentitySlotAliases/BindArrayJoinElement/DocumentMode/IgnoreColumns/Final (:3630-3633, :3708-3713)
    //     - internal preparation state with no builder entry point.
    // The remaining arms of Matches are covered by the sixteen facts above.
    // ---------------------------------------------------------------------------------------------
}
